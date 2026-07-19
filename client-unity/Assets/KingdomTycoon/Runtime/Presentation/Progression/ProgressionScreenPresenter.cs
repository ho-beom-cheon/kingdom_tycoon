using System;
using System.Collections;
using KingdomTycoon.Application.Progression;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Progression;
using KingdomTycoon.Infrastructure.Save;
using UnityEngine;

namespace KingdomTycoon.Presentation.Progression
{
    public sealed class ProgressionScreenPresenter : MonoBehaviour
    {
        [SerializeField] private ProgressionScreenView view;
        private ProgressionGameService service;
        private ProgressionOverviewDto overview;
        private int selectedIndex;
        private bool bound;
        private bool busy;
        public void Configure(ProgressionScreenView value) => view = value;
        private void Awake() { if (view == null) view = GetComponentInChildren<ProgressionScreenView>(true); view.Bind(Close, () => Select(-1), () => Select(1), Primary); view.HideToast(); }
        public void Open() { gameObject.SetActive(true); view.ShowLoading(); EnsureBound(); if (service == null) return; try { Refresh(); StartCoroutine(FadeIn()); } catch (Exception exception) { view.ShowError(exception.Message); } }
        public void Close() { if (!busy) gameObject.SetActive(false); }
        private void EnsureBound()
        {
            if (bound) return; if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) { view.ShowError("P11_APP_NOT_READY"); return; }
            try { service = AppRoot.Instance.Services.Get<ProgressionGameService>(); service.Changed += OnChanged; bound = true; } catch (Exception exception) { view.ShowError(exception.Message); }
        }
        private void Refresh() { overview = service.GetOverview(); selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, overview.Mercenaries.Count - 1)); view.Render(overview, selectedIndex); }
        private void Select(int delta) { if (busy || overview == null || overview.Mercenaries.Count == 0) return; selectedIndex = (selectedIndex + delta + overview.Mercenaries.Count) % overview.Mercenaries.Count; view.Render(overview, selectedIndex); }
        private void Primary()
        {
            if (busy || overview == null || overview.Mercenaries.Count == 0) return; busy = true;
            try
            {
                PromotionMercenaryDto row = overview.Mercenaries[selectedIndex]; Guid id = Guid.Parse(UuidV7.NewString(DateTimeOffset.UtcNow)); PromotionOperationResult result;
                if (row.Status == "COMPLETED_PENDING_APPLY")
                {
                    var draft = new ApplyPromotionCommand(id, overview.Revision, null, row.Id); string hash = new ProgressionRequestHasher().Compute(draft);
                    result = service.ApplyPromotion(new ApplyPromotionCommand(id, overview.Revision, hash, row.Id));
                }
                else
                {
                    var draft = new StartPromotionReviewCommand(id, overview.Revision, null, row.Id); string hash = new ProgressionRequestHasher().Compute(draft);
                    result = service.StartReview(new StartPromotionReviewCommand(id, overview.Revision, hash, row.Id));
                }
                Refresh(); view.ShowToast("처리 완료 · " + result.ResultCode);
            }
            catch (Exception exception) { view.ShowToast("처리 실패 · " + exception.Message); }
            finally { busy = false; }
        }
        private IEnumerator FadeIn() { const float duration = .22f; float elapsed = 0; view.CanvasGroup.alpha = 0; while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / duration); view.CanvasGroup.alpha = 1f - Mathf.Pow(1f - t, 3f); yield return null; } view.CanvasGroup.alpha = 1; }
        private void OnChanged(object sender, ProgressionOverviewDto value) { overview = value; selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, value.Mercenaries.Count - 1)); view.Render(value, selectedIndex); }
        private void OnDestroy() { if (service != null) service.Changed -= OnChanged; }
    }
}
