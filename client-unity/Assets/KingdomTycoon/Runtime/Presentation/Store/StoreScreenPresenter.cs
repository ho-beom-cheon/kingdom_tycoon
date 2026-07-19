using System;
using System.Collections;
using KingdomTycoon.Application.Economy;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Economy;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Presentation.Kingdom.Views;
using KingdomTycoon.Presentation.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Store
{
    public sealed class StoreScreenPresenter : MonoBehaviour
    {
        [SerializeField] private StoreScreenView view;
        private EconomyGameService service;
        private StorefrontDto storefront;
        private int selectedIndex;
        private bool bound;
        private bool busy;

        public StoreScreenView View => view;
        public bool IsOpen => gameObject.activeSelf;
        public void Configure(StoreScreenView value) => view = value;

        private void Awake()
        {
            if (view == null) view = GetComponentInChildren<StoreScreenView>(true);
            view.Bind(Close, () => Refresh(), () => view.ShowToast("판매할 전리품을 선택하세요."), () => view.ShowToast("최근 거래 장부를 표시합니다."), ShowConfirm, CyclePolicy);
            view.ProductList.Selected += Select;
            view.Modal.Bind(ConfirmPurchase, view.Modal.Hide);
            view.HideToast(); view.Modal.Hide();
        }

        public void Open()
        {
            FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include)?.SetNavigationVisible(false);
            gameObject.SetActive(true); view.ShowState(StoreUiState.Loading); EnsureBound();
            if (service == null) return;
            try
            {
                storefront = service.GetStorefront();
                if (storefront.Availability == "OPEN" && storefront.Products.Count == 0) Supply();
                Refresh(); StartCoroutine(FadeIn());
            }
            catch (Exception exception) { view.ShowState(StoreUiState.Error, exception.Message); }
        }

        public void Close()
        {
            if (busy) return;
            view.Modal.Hide();
            gameObject.SetActive(false);
            FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include)?.SetNavigationVisible(true);
        }

        private void EnsureBound()
        {
            if (bound) return;
            if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) { view.ShowState(StoreUiState.Content); return; }
            try { service = AppRoot.Instance.Services.Get<EconomyGameService>(); service.Changed += OnChanged; bound = true; }
            catch (Exception exception) { view.ShowState(StoreUiState.Error, exception.Message); }
        }

        private void Supply()
        {
            service.EnsureSystemSupply();
        }

        private void Refresh()
        {
            if (service == null) return; storefront = service.GetStorefront(storefront?.MercenaryId); selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, storefront.Products.Count - 1)); view.Render(storefront, selectedIndex);
        }

        private void Select(int index) { selectedIndex = index; view.Render(storefront, selectedIndex); }
        private void ShowConfirm() { StoreProductDto product = view.ProductAt(selectedIndex); if (product != null) view.Modal.Show(product); }

        private void ConfirmPurchase()
        {
            if (busy || service == null) return; StoreProductDto product = view.ProductAt(selectedIndex); if (product == null) return;
            busy = true; view.Modal.Hide();
            try
            {
                var line = new StoreCommandLine(product.Kind, product.Id, 1, product.UnitPrice); string quote = service.GetQuoteContextHash(storefront.MercenaryId, new[] { line });
                Guid id = Guid.Parse(UuidV7.NewString(DateTimeOffset.UtcNow)); var hasher = new EconomyRequestHasher();
                var draft = new BuyFromStoreCommand(id, storefront.Revision, null, quote, storefront.MercenaryId, product.Kind == "EQUIPMENT", new[] { line });
                EconomyOperationResult result = service.BuyFromStore(new BuyFromStoreCommand(id, storefront.Revision, hasher.Compute(draft), quote, storefront.MercenaryId, product.Kind == "EQUIPMENT", new[] { line }));
                service.EnsureSystemSupply();
                view.ShowToast($"거래 완료 · 개인 {result.PersonalGoldDelta:N0} · 왕국 +{result.KingdomGoldDelta:N0}"); Refresh();
            }
            catch (Exception exception) { view.ShowToast("거래 실패 · " + exception.Message); }
            finally { busy = false; }
        }

        private void CyclePolicy()
        {
            if (busy || storefront == null || storefront.Level < 2) return; string next = storefront.PolicyId switch { "LOW" => "STANDARD", "STANDARD" => "HIGH", _ => "LOW" };
            try
            {
                Guid id = Guid.Parse(UuidV7.NewString(DateTimeOffset.UtcNow)); var hasher = new EconomyRequestHasher(); var draft = new SetPricingPolicyCommand(id, storefront.Revision, null, next);
                service.SetPricingPolicy(new SetPricingPolicyCommand(id, storefront.Revision, hasher.Compute(draft), next)); Refresh(); view.ShowToast("가격 정책을 " + next + "으로 변경했습니다.");
            }
            catch (Exception exception) { view.ShowToast("정책 변경 실패 · " + exception.Message); }
        }

        private IEnumerator FadeIn()
        {
            const float duration = .22f; float elapsed = 0; view.CanvasGroup.alpha = 0;
            while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; float t = Mathf.Clamp01(elapsed / duration); view.CanvasGroup.alpha = 1f - Mathf.Pow(1f - t, 3f); yield return null; }
            view.CanvasGroup.alpha = 1;
        }
        private void OnChanged(object sender, StorefrontDto value) { storefront = value; view.Render(value, selectedIndex); }
        private void OnDestroy() { if (service != null) service.Changed -= OnChanged; if (view != null) view.ProductList.Selected -= Select; }
    }

}
