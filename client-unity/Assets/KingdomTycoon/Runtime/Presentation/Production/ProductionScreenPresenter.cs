using System;
using System.Collections;
using KingdomTycoon.Application.Production;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Production;
using KingdomTycoon.Infrastructure.Save;
using UnityEngine;

namespace KingdomTycoon.Presentation.Production
{
    public sealed class ProductionScreenPresenter : MonoBehaviour
    {
        [SerializeField] private ProductionScreenView view;
        private ProductionGameService service;
        private ProductionOverviewDto overview;
        private int selectedIndex;
        private bool bound;
        private bool busy;

        public bool IsOpen => gameObject.activeSelf;
        public ProductionScreenView View => view;
        public void Configure(ProductionScreenView value) => view = value;

        private void Awake()
        {
            if (view == null) view = GetComponentInChildren<ProductionScreenView>(true);
            view.Bind(Close, RunAutomation, Advance, () => ChangeTarget(-1), () => ChangeTarget(1), Select); view.HideToast();
        }

        public void Open()
        {
            gameObject.SetActive(true); view.ShowLoading(); EnsureBound(); if (service == null) return;
            try { Refresh(); StartCoroutine(FadeIn()); } catch (Exception exception) { view.ShowError(exception.Message); }
        }

        public void Close() { if (!busy) gameObject.SetActive(false); }
        public void Select(int index) { selectedIndex = Mathf.Clamp(index, 0, Math.Max(0, overview?.Targets.Count - 1 ?? 0)); if (overview != null) view.Render(overview, selectedIndex); }

        private void EnsureBound()
        {
            if (bound) return; if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) { view.ShowError("P09_APP_NOT_READY"); return; }
            try { service = AppRoot.Instance.Services.Get<ProductionGameService>(); service.Changed += OnChanged; bound = true; }
            catch (Exception exception) { view.ShowError(exception.Message); }
        }

        private void Refresh() { overview = service.GetOverview(); selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, overview.Targets.Count - 1)); view.Render(overview, selectedIndex); }
        private void RunAutomation() => Execute("자동 보충을 계산했습니다.", (id, hash) => new RunProductionAutomationCommand(id, overview.Revision, hash));
        private void Advance() => Execute("생산 tick을 진행했습니다.", (id, hash) => new AdvanceProductionTicksCommand(id, overview.Revision, hash, 10));

        private void ChangeTarget(int delta)
        {
            if (overview == null || overview.Targets.Count == 0 || busy) return; StockTargetDto target = overview.Targets[selectedIndex]; int next = Mathf.Max(0, target.Target + delta);
            Execute("재고 목표를 변경했습니다.", (id, hash) => new SetStockTargetCommand(id, overview.Revision, hash, target.Id, next, target.Priority, target.Enabled));
        }

        private void Execute(string success, Func<Guid, string, ProductionCommand> factory)
        {
            if (busy || service == null || overview == null) return; busy = true;
            try
            {
                Guid id = Guid.Parse(UuidV7.NewString(DateTimeOffset.UtcNow)); ProductionCommand draft = factory(id, null); string hash = new ProductionRequestHasher().Compute(draft); ProductionCommand command = factory(id, hash);
                ProductionOperationResult result = command switch
                {
                    RunProductionAutomationCommand value => service.RunAutomation(value), AdvanceProductionTicksCommand value => service.Advance(value), SetStockTargetCommand value => service.SetStockTarget(value), _ => throw new InvalidOperationException("P09_COMMAND_INVALID")
                };
                Refresh(); view.ShowToast(success + (result.Completed > 0 ? $" · 완료 {result.Completed}" : string.Empty));
            }
            catch (Exception exception) { view.ShowToast("처리 실패 · " + exception.Message); }
            finally { busy = false; }
        }

        private IEnumerator FadeIn()
        {
            const float duration = .22f; float elapsed = 0; view.CanvasGroup.alpha = 0;
            while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / duration); view.CanvasGroup.alpha = 1f - Mathf.Pow(1f - t, 3f); yield return null; }
            view.CanvasGroup.alpha = 1;
        }
        private void OnChanged(object sender, ProductionOverviewDto value) { overview = value; view.Render(value, selectedIndex); }
        private void OnDestroy() { if (service != null) service.Changed -= OnChanged; }
    }

}
