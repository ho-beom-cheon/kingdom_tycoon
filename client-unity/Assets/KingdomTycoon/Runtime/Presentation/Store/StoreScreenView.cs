using System;
using System.Collections.Generic;
using KingdomTycoon.Application.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Store
{
    public enum StoreUiState { Loading, Content, Empty, Error, Locked, Offline }

    public sealed class StoreScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject content;
        [SerializeField] private GameObject statePanel;
        [SerializeField] private TMP_Text stateTitle;
        [SerializeField] private TMP_Text stateBody;
        [SerializeField] private TMP_Text merchantStatus;
        [SerializeField] private TMP_Text mercenaryLabel;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private TMP_Text activity;
        [SerializeField] private TMP_Text policy;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private StoreWalletRenderer wallet;
        [SerializeField] private StoreProductVirtualList productList;
        [SerializeField] private StoreTransactionModal modal;
        [SerializeField] private Button back;
        [SerializeField] private Button buyTab;
        [SerializeField] private Button sellTab;
        [SerializeField] private Button historyTab;
        [SerializeField] private Button action;
        [SerializeField] private Button policyButton;
        [SerializeField] private GameObject toast;
        [SerializeField] private TMP_Text toastText;
        private IReadOnlyList<StoreProductDto> products = Array.Empty<StoreProductDto>();

        public CanvasGroup CanvasGroup => canvasGroup;
        public StoreTransactionModal Modal => modal;
        public StoreProductVirtualList ProductList => productList;

        public void Configure(CanvasGroup group, GameObject contentRoot, GameObject stateRoot, TMP_Text stateHeading, TMP_Text stateMessage,
            TMP_Text merchant, TMP_Text mercenary, TMP_Text detailHeading, TMP_Text detail, TMP_Text recent, TMP_Text policyText, TMP_Text primaryLabel,
            StoreWalletRenderer walletRenderer, StoreProductVirtualList list, StoreTransactionModal transactionModal, Button backButton, Button buy,
            Button sell, Button history, Button primary, Button pricing, GameObject toastRoot, TMP_Text result)
        {
            canvasGroup = group; content = contentRoot; statePanel = stateRoot; stateTitle = stateHeading; stateBody = stateMessage; merchantStatus = merchant;
            mercenaryLabel = mercenary; detailTitle = detailHeading; detailBody = detail; activity = recent; policy = policyText; actionLabel = primaryLabel;
            wallet = walletRenderer; productList = list; modal = transactionModal; back = backButton; buyTab = buy; sellTab = sell; historyTab = history;
            action = primary; policyButton = pricing; toast = toastRoot; toastText = result;
        }

        public void Bind(Action close, Action buy, Action sell, Action history, Action actionRequested, Action policyRequested)
        {
            back.onClick.RemoveAllListeners(); back.onClick.AddListener(() => close());
            buyTab.onClick.RemoveAllListeners(); buyTab.onClick.AddListener(() => buy());
            sellTab.onClick.RemoveAllListeners(); sellTab.onClick.AddListener(() => sell());
            historyTab.onClick.RemoveAllListeners(); historyTab.onClick.AddListener(() => history());
            action.onClick.RemoveAllListeners(); action.onClick.AddListener(() => actionRequested());
            policyButton.onClick.RemoveAllListeners(); policyButton.onClick.AddListener(() => policyRequested());
        }

        public void Render(StorefrontDto storefront, int selectedIndex)
        {
            if (storefront == null) { ShowState(StoreUiState.Error, "P08_CONTENT_MISSING"); return; }
            products = storefront.Products;
            StoreUiState state = storefront.Availability == "LOCKED" ? StoreUiState.Locked : products.Count == 0 ? StoreUiState.Empty : StoreUiState.Content;
            ShowState(state, storefront.DisabledReason);
            wallet.Render(storefront.KingdomGold, storefront.PersonalGold);
            mercenaryLabel.text = storefront.MercenaryId == null ? "용병 없음" : "레온 · 선택됨";
            merchantStatus.text = storefront.Availability == "OPEN" ? $"<color=#8ECF91>● 영업 중</color>  상점 {storefront.Level}레벨" : "<color=#D86B62>● 거래 중지</color>  시설 상태를 확인하세요";
            policy.text = $"가격 정책  {Policy(storefront.PolicyId)}";
            productList.Bind(products, selectedIndex);
            if (products.Count == 0) { detailTitle.text = "상품 상세"; detailBody.text = "표시할 상품이 없습니다."; action.interactable = false; }
            else
            {
                int index = Mathf.Clamp(selectedIndex, 0, products.Count - 1); StoreProductDto item = products[index];
                detailTitle.text = item.Name;
                detailBody.text = $"<color=#B9B0A3>{Kind(item.Kind)} · {Source(item.SourceType)}</color>\n\n재고  {item.Quantity:N0}\n단계  {(item.Tier == 0 ? "-" : item.Tier.ToString())}\n품질  {Quality(item.QualityId)}\n\n<color=#F2D47A><size=36>{item.UnitPrice:N0} 골드</size></color>\n\n개인 골드에서 결제하고\n왕국 금고로 이전합니다.";
                actionLabel.text = "구매 견적 보기"; action.interactable = storefront.Availability == "OPEN";
            }
            activity.text = storefront.LedgerCount == 0 ? "아직 거래가 없습니다." : $"최근 거래 {storefront.LedgerCount}건 · 장부 검증 완료";
            policyButton.interactable = storefront.Availability == "OPEN" && storefront.Level >= 2;
        }

        public void ShowState(StoreUiState state, string diagnostic = null)
        {
            bool showContent = state is StoreUiState.Content or StoreUiState.Empty or StoreUiState.Offline;
            content.SetActive(showContent); statePanel.SetActive(state is StoreUiState.Loading or StoreUiState.Error or StoreUiState.Locked);
            stateTitle.text = state switch { StoreUiState.Loading => "상점을 준비하고 있습니다", StoreUiState.Locked => "왕국 상점이 잠겨 있습니다", StoreUiState.Error => "상점을 표시할 수 없습니다", _ => string.Empty };
            stateBody.text = state switch
            {
                StoreUiState.Loading => "재고와 장부를 검증하는 중입니다…",
                StoreUiState.Locked => "상점을 건설하고 상인을 배치하면 거래할 수 있습니다.",
                StoreUiState.Error => "저장 데이터는 변경하지 않았습니다.\n잠시 후 다시 열어 주세요.",
                _ => string.Empty
            };
        }

        public StoreProductDto ProductAt(int index) => index >= 0 && index < products.Count ? products[index] : null;
        public void ShowToast(string message) { toastText.text = message; toast.SetActive(true); }
        public void HideToast() => toast.SetActive(false);
        private static string Kind(string value) => value switch { "POTION" => "물약", "EQUIPMENT" => "장비", "ITEM" => "재료", _ => "상품" };
        private static string Source(string value) => value switch { "PRODUCTION" => "왕국 생산", "HUNT" => "사냥 전리품", "BOSS" => "우두머리 전리품", _ => "왕국 보급" };
        private static string Quality(string value) => value switch { "QUALITY_COMMON" => "일반", "QUALITY_FINE" => "고급", "QUALITY_RARE" => "희귀", "QUALITY_LEGACY" => "영웅", "QUALITY_RELIC" => "유물", null => "표준", _ => "표준" };
        private static string Policy(string value) => value switch { "STANDARD" => "표준", "DISCOUNT" => "할인", "PREMIUM" => "고급", _ => "표준" };
    }
}
