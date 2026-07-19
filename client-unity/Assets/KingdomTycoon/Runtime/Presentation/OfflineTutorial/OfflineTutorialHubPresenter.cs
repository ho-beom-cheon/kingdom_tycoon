using System;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Domain.OfflineTutorial;
using KingdomTycoon.Infrastructure.OfflineTutorial;
using UnityEngine;

namespace KingdomTycoon.Presentation.OfflineTutorial
{
    public sealed class OfflineTutorialHubPresenter : MonoBehaviour
    {
        [SerializeField] private OfflineTutorialHubView view;
        private OfflineTutorialGameService service;

        public void Configure(OfflineTutorialHubView value) => view = value;
        public void Open()
        {
            gameObject.SetActive(true); view.SetVisible(true);
            try { service ??= AppRoot.Instance?.Services.Get<OfflineTutorialGameService>() ?? throw new InvalidOperationException("P15_APP_ROOT_MISSING"); Bind(); Render(); }
            catch (Exception error) { view.ShowError(Code(error)); }
        }

        private void Bind()
        {
            view.CloseRequested -= Close; view.CloseRequested += Close;
            view.AdvanceRequested -= Advance; view.AdvanceRequested += Advance;
            view.SkipRequested -= Skip; view.SkipRequested += Skip;
            view.SkipAllRequested -= SkipAll; view.SkipAllRequested += SkipAll;
        }
        private void Close() { view.SetVisible(false); gameObject.SetActive(false); }
        private void Advance() => Execute("COMPLETE");
        private void Skip() => Execute("SKIP");
        private void SkipAll() { try { var result = service.Execute(service.CreateSkipAllCommand()); Render(); view.ShowResult(result.ResultCode); } catch (Exception error) { view.ShowError(Code(error)); } }
        private void Execute(string mode) { try { var result = service.Execute(service.CreateCurrentActionCommand(mode)); Render(); view.ShowResult(result.ResultCode); } catch (Exception error) { view.ShowError(Code(error)); } }
        private void Render() => view.Render(service.GetOverview());
        private static string Code(Exception error) => error is OfflineTutorialDomainException domain ? domain.Code : error.GetBaseException().Message;
    }
}
