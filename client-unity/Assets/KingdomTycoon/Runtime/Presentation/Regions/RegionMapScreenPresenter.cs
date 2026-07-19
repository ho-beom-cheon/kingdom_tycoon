using System;
using System.Collections;
using System.Linq;
using KingdomTycoon.Application.Combat;
using KingdomTycoon.Application.Regions;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Infrastructure.Regions;
using KingdomTycoon.Infrastructure.Save;
using UnityEngine;

namespace KingdomTycoon.Presentation.Regions
{
    public sealed class RegionMapScreenPresenter : MonoBehaviour
    {
        [SerializeField] private RegionMapScreenView view;
        private RegionGameService regions;
        private CombatGameService combat;
        private RegionOverviewDto overview;
        private int selectedIndex;
        private bool bound;
        private bool busy;
        public void Configure(RegionMapScreenView value) => view = value;
        private void Awake() { if (view == null) view = GetComponentInChildren<RegionMapScreenView>(true); view.Bind(Close, Select, TogglePolicy, StartHunt); view.HideToast(); }
        public void Open() { gameObject.SetActive(true); view.ShowLoading(); EnsureBound(); if (regions == null) return; try { Refresh(); StartCoroutine(FadeIn()); } catch (Exception exception) { view.ShowError(exception.Message); } }
        public void Close() { if (!busy) gameObject.SetActive(false); }
        private void EnsureBound()
        {
            if (bound) return; if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) { view.ShowError("P12_APP_NOT_READY"); return; }
            try { regions = AppRoot.Instance.Services.Get<RegionGameService>(); combat = AppRoot.Instance.Services.Get<CombatGameService>(); regions.Changed += OnChanged; bound = true; }
            catch (Exception exception) { view.ShowError(exception.Message); }
        }
        private void Refresh() { overview = regions.GetOverview(); selectedIndex = Mathf.Clamp(selectedIndex, 0, overview.Regions.Count - 1); view.Render(overview, selectedIndex); }
        private void Select(int index) { if (busy || overview == null) return; selectedIndex = Mathf.Clamp(index, 0, overview.Regions.Count - 1); view.Render(overview, selectedIndex); }
        private void TogglePolicy()
        {
            if (busy || overview == null) return; busy = true;
            try
            {
                RegionSummaryDto row = overview.Regions[selectedIndex]; Guid operationId = Guid.Parse(UuidV7.NewString(DateTimeOffset.UtcNow));
                var draft = new SetRegionAccessPolicyCommand(operationId, overview.Revision, null, row.Id, !row.Allowed); string hash = new RegionRequestHasher().Compute(draft);
                RegionPolicyOperationResult result = regions.SetAccessPolicy(new SetRegionAccessPolicyCommand(operationId, overview.Revision, hash, row.Id, !row.Allowed));
                Refresh(); view.ShowToast("파견 정책을 반영했습니다.");
            }
            catch (Exception exception) { Debug.LogWarning(exception); view.ShowToast("파견 정책을 변경하지 못했습니다."); }
            finally { busy = false; }
        }
        private void StartHunt()
        {
            if (busy || overview == null) return; busy = true;
            try
            {
                RegionSummaryDto row = overview.Regions[selectedIndex]; string[] party = overview.Mercenaries
                    .Where(value => value.Active && value.AutonomyState == "IDLE_TOWN" && value.CurrentRegionId == null && value.RankOrder >= row.MinimumRankOrder)
                    .OrderByDescending(value => value.RankOrder).ThenBy(value => value.Id, StringComparer.Ordinal).Take(Mathf.Clamp(row.RecommendedPartySize, 1, 4)).Select(value => value.Id).ToArray();
                DateTimeOffset now = DateTimeOffset.UtcNow; Guid operationId = Guid.Parse(UuidV7.NewString(now)); Guid huntId = Guid.Parse(UuidV7.NewString(now.AddMilliseconds(1)));
                var draft = new StartHuntCommand(operationId, huntId, combat.Revision, null, row.Id, party.Select(Guid.Parse)); string hash = new CombatRequestHasher().ComputeHash(draft);
                StartHuntResult result = combat.StartHunt(new StartHuntCommand(operationId, huntId, combat.Revision, hash, row.Id, party.Select(Guid.Parse)));
                Refresh(); view.ShowToast($"{row.Name} 파견 시작  ·  {result.PartyMercenaryInstanceIds.Count}명");
            }
            catch (Exception exception) { Debug.LogWarning(exception); view.ShowToast("사냥을 시작하지 못했습니다. 파견 조건을 확인해 주세요."); }
            finally { busy = false; }
        }
        private IEnumerator FadeIn() { const float duration = .22f; float elapsed = 0; view.CanvasGroup.alpha = 0; while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / duration); view.CanvasGroup.alpha = 1f - Mathf.Pow(1f - t, 3f); yield return null; } view.CanvasGroup.alpha = 1; }
        private void OnChanged(object sender, RegionOverviewDto value) { overview = value; selectedIndex = Mathf.Clamp(selectedIndex, 0, value.Regions.Count - 1); view.Render(value, selectedIndex); }
        private void OnDestroy() { if (regions != null) regions.Changed -= OnChanged; }
    }
}
