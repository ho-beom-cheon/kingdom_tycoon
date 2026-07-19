using System;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Recruitment;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Domain.Recruitment;
using KingdomTycoon.Infrastructure.Recruitment;
using UnityEngine;

namespace KingdomTycoon.Presentation.Recruitment
{
    public sealed class RecruitmentScreenPresenter : MonoBehaviour
    {
        [SerializeField] private RecruitmentScreenView view;
        private RecruitmentGameService service;

        public void Configure(RecruitmentScreenView value) => view = value;

        public void Open()
        {
            gameObject.SetActive(true);
            try
            {
                service ??= AppRoot.Instance?.Services.Get<RecruitmentGameService>() ?? throw new InvalidOperationException("P13_APP_ROOT_MISSING");
                Bind();
                view.SetVisible(true);
                Refresh();
            }
            catch (Exception error)
            {
                view.SetVisible(true);
                view.ShowState("모집소를 열 수 없습니다", Code(error));
            }
        }

        private void Bind()
        {
            view.CloseRequested -= Close; view.CloseRequested += Close;
            view.RefreshRequested -= RefreshCandidates; view.RefreshRequested += RefreshCandidates;
            view.LockRequested -= Lock; view.LockRequested += Lock;
            view.HireRequested -= Hire; view.HireRequested += Hire;
            view.SpecialRequested -= Special; view.SpecialRequested += Special;
            view.HistoryRequested -= RefreshHistory; view.HistoryRequested += RefreshHistory;
        }

        private void Close()
        {
            view.SetVisible(false);
            gameObject.SetActive(false);
        }
        private void Refresh() => view.Render(service.GetOverview(), service.GetHistoryLines());
        private void RefreshHistory() => view.Render(service.GetOverview(), service.GetHistoryLines());

        private void RefreshCandidates()
        {
            RecruitmentOverviewDto overview = service.GetOverview();
            bool free = !overview.NextFreeRefreshAt.HasValue || DateTimeOffset.UtcNow >= overview.NextFreeRefreshAt.Value;
            Run(() => service.Refresh(service.CreateRefreshCommand(free)), "후보 명단을 갱신했습니다.");
        }

        private void Lock(string id, bool locked) =>
            Run(() => service.SetCandidateLock(service.CreateLockCommand(id, locked)), locked ? "후보를 잠갔습니다." : "후보 잠금을 해제했습니다.");

        private void Hire(string id) =>
            Run(() => service.Hire(service.CreateHireCommand(id)), "새 용병이 로스터에 합류했습니다.");

        private async void Special()
        {
            RecruitmentOverviewDto state = service.GetOverview();
            string pool = state.Tickets > 0 ? "SPECIAL_STANDARD_TICKET" : "SPECIAL_STANDARD_FREE_PREMIUM";
            string payment = state.Tickets > 0 ? "TICKET" : "FREE_PREMIUM";
            await RunAsync(
                () => service.RecruitSpecialAsync(service.CreateSpecialCommand(pool, payment), CancellationToken.None),
                "특별 모집을 완료했습니다.");
        }

        private async Task RunAsync(Func<Task<RecruitmentOperationResult>> action, string success)
        {
            try
            {
                RecruitmentOperationResult result = await action();
                Refresh();
                view.ShowToast(result.Replayed ? "이미 처리된 요청입니다." : success);
            }
            catch (Exception error)
            {
                view.ShowToast(Code(error), true);
            }
        }

        private void Run(Func<RecruitmentOperationResult> action, string success)
        {
            try
            {
                RecruitmentOperationResult result = action();
                Refresh();
                view.ShowToast(result.Replayed ? "이미 처리된 요청입니다." : success);
            }
            catch (Exception error)
            {
                view.ShowToast(Code(error), true);
            }
        }

        private static string Code(Exception error) =>
            error is RecruitmentDomainException domain ? domain.Code : error.GetBaseException().Message;
    }
}
