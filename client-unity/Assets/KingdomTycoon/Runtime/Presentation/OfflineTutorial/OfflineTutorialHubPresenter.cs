using System;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Domain.OfflineTutorial;
using KingdomTycoon.Infrastructure.OfflineTutorial;
using KingdomTycoon.Presentation.Navigation;
using KingdomTycoon.Services;
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
            try
            {
                service ??= AppRoot.Instance?.Services.Get<OfflineTutorialGameService>() ?? throw new InvalidOperationException("P15_APP_ROOT_MISSING");
                Bind(); service.RefreshTutorialProgress(); Render();
            }
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
        private void Advance()
        {
            try
            {
                var current = service.GetOverview().CurrentStep;
                if (current == null)
                {
                    Close();
                    UnifiedNavigationMenu completedNavigation = FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include);
                    completedNavigation?.CloseAll();
                    AppRoot.Instance.Services.Get<SceneFlowService>().LoadSceneAsync("Kingdom");
                    return;
                }
                if (current.ActionType == "VIEW_KINGDOM") service.CompleteObservedKingdomView();
                Close();
                UnifiedNavigationMenu navigation = FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include);
                switch (current.ActionType)
                {
                    case "VIEW_KINGDOM":
                    case "RETURN_AND_SELL":
                    case "WAIT_FOR_MERCENARY_BUY_AND_EQUIP":
                        navigation?.CloseAll();
                        AppRoot.Instance.Services.Get<SceneFlowService>().LoadSceneAsync("Kingdom");
                        break;
                    case "RECRUIT_FROM_POOL": navigation?.OpenRecruitment(); break;
                    case "SET_REGION_PERMISSION":
                    case "OBSERVE_AUTONOMY_HUNT":
                    case "UNLOCK_REGION": navigation?.OpenRegions(); break;
                    case "BUILD_FACILITY_AND_CRAFT_RECIPE":
                        if (current.TargetId == "REC_POT_HEAL_SMALL") navigation?.OpenProduction();
                        else
                        {
                            navigation?.CloseAll();
                            AppRoot.Instance.Services.Get<SceneFlowService>().LoadSceneAsync("Kingdom");
                        }
                        break;
                    case "COMPLETE_PROMOTION": navigation?.OpenProgression(); break;
                }
            }
            catch (Exception error) { view.ShowError(Code(error)); }
        }
        private void Skip() => Execute("SKIP");
        private void SkipAll() { try { var result = service.Execute(service.CreateSkipAllCommand()); Render(); view.ShowResult(result.ResultCode); } catch (Exception error) { view.ShowError(Code(error)); } }
        private void Execute(string mode) { try { var result = service.Execute(service.CreateCurrentActionCommand(mode)); Render(); view.ShowResult(result.ResultCode); } catch (Exception error) { view.ShowError(Code(error)); } }
        private void Render() => view.Render(service.GetOverview());
        private static string Code(Exception error) => error is OfflineTutorialDomainException domain ? domain.Code : error.GetBaseException().Message;
    }
}
