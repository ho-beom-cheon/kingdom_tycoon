using System;
using System.Collections;
using KingdomTycoon.Application.EquipmentGrowth;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.EquipmentGrowth;
using KingdomTycoon.Infrastructure.Save;
using UnityEngine;

namespace KingdomTycoon.Presentation.EquipmentGrowth
{
    public sealed class EquipmentGrowthScreenPresenter : MonoBehaviour
    {
        private static readonly string[] RefineOptions = { "REF_ATK_POWER", "REF_CRIT", "REF_DEF", "REF_HP", "REF_MATERIAL", "REF_RARE_FIND", "REF_FIRE", "REF_FROST", "REF_POISON", "REF_BOSS", "REF_PART" };
        [SerializeField] private EquipmentGrowthScreenView view;
        private EquipmentGrowthGameService service;
        private EquipmentGrowthOverviewDto overview;
        private int equipmentIndex;
        private int refineIndex;
        private bool bound;
        private bool busy;

        public void Configure(EquipmentGrowthScreenView value) => view = value;
        private void Awake()
        {
            if (view == null) view = GetComponentInChildren<EquipmentGrowthScreenView>(true);
            view.Bind(Close, () => SelectEquipment(-1), () => SelectEquipment(1), Enhance, () => SelectRefine(-1), () => SelectRefine(1), RollRefine, () => Resolve(true), () => Resolve(false), Dismantle);
            view.HideToast();
        }
        public void Open() { gameObject.SetActive(true); view.ShowLoading(); EnsureBound(); if (service == null) return; try { Refresh(); StartCoroutine(FadeIn()); } catch (Exception exception) { view.ShowError(exception.Message); } }
        public void Close() { if (!busy) gameObject.SetActive(false); }
        private void EnsureBound()
        {
            if (bound) return;
            if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) { view.ShowError("P10_APP_NOT_READY"); return; }
            try { service = AppRoot.Instance.Services.Get<EquipmentGrowthGameService>(); service.Changed += OnChanged; bound = true; }
            catch (Exception exception) { view.ShowError(exception.Message); }
        }
        private void Refresh() { overview = service.GetOverview(); ClampSelection(); view.Render(overview, equipmentIndex, RefineOptions[refineIndex]); }
        private void ClampSelection() => equipmentIndex = Mathf.Clamp(equipmentIndex, 0, Math.Max(0, overview?.Equipment.Count - 1 ?? 0));
        private void SelectEquipment(int delta) { if (overview == null || overview.Equipment.Count == 0 || busy) return; equipmentIndex = (equipmentIndex + delta + overview.Equipment.Count) % overview.Equipment.Count; view.Render(overview, equipmentIndex, RefineOptions[refineIndex]); }
        private void SelectRefine(int delta) { if (busy) return; refineIndex = (refineIndex + delta + RefineOptions.Length) % RefineOptions.Length; if (overview != null) view.Render(overview, equipmentIndex, RefineOptions[refineIndex]); }
        private void Enhance() => Execute((id, hash, item) => new EnhanceEquipmentCommand(id, overview.Revision, hash, overview.ActorId, item.Id), "강화 결과를 저장했습니다.");
        private void RollRefine() => Execute((id, hash, item) => new RollRefineOptionCommand(id, overview.Revision, hash, overview.ActorId, item.Id, RefineOptions[refineIndex]), "새 재련 후보를 생성했습니다.");
        private void Resolve(bool accept) => Execute((id, hash, item) => new ResolveRefineOptionCommand(id, overview.Revision, hash, overview.ActorId, item.Id, accept), accept ? "새 옵션을 적용했습니다." : "기존 옵션을 유지했습니다.");
        private void Dismantle() => Execute((id, hash, item) => new DismantleEquipmentCommand(id, overview.Revision, hash, overview.ActorId, new[] { item.Id }, true), "장비를 분해하고 재료를 회수했습니다.");

        private void Execute(Func<Guid, string, GrowthEquipmentDto, EquipmentGrowthCommand> factory, string success)
        {
            if (busy || service == null || overview == null || overview.Equipment.Count == 0) return; busy = true;
            try
            {
                GrowthEquipmentDto item = overview.Equipment[equipmentIndex]; Guid id = Guid.Parse(UuidV7.NewString(DateTimeOffset.UtcNow)); EquipmentGrowthCommand draft = factory(id, null, item);
                string hash = new EquipmentGrowthRequestHasher().Compute(draft); EquipmentGrowthCommand command = factory(id, hash, item);
                EquipmentGrowthOperationResult result = command switch
                {
                    EnhanceEquipmentCommand value => service.Enhance(value), RollRefineOptionCommand value => service.RollRefine(value),
                    ResolveRefineOptionCommand value => service.ResolveRefine(value), DismantleEquipmentCommand value => service.Dismantle(value),
                    _ => throw new InvalidOperationException("P10_COMMAND_INVALID")
                };
                Refresh(); view.ShowToast(success);
            }
            catch (Exception exception) { Debug.LogWarning(exception); view.ShowToast("처리하지 못했습니다. 조건을 확인해 주세요."); }
            finally { busy = false; }
        }
        private IEnumerator FadeIn() { const float duration = .22f; float elapsed = 0; view.CanvasGroup.alpha = 0; while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / duration); view.CanvasGroup.alpha = 1f - Mathf.Pow(1f - t, 3f); yield return null; } view.CanvasGroup.alpha = 1; }
        private void OnChanged(object sender, EquipmentGrowthOverviewDto value) { overview = value; ClampSelection(); view.Render(value, equipmentIndex, RefineOptions[refineIndex]); }
        private void OnDestroy() { if (service != null) service.Changed -= OnChanged; }
    }
}
