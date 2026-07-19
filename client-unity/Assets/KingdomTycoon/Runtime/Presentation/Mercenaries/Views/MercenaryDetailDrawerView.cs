using System;
using System.Collections;
using KingdomTycoon.Application.Mercenaries;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Mercenaries.Views
{
    public sealed class MercenaryDetailDrawerView : MonoBehaviour
    {
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text identity;
        [SerializeField] private TMP_Text body;
        [SerializeField] private TMP_Text error;
        [SerializeField] private Button close;
        [SerializeField] private Button activeToggle;
        [SerializeField] private TMP_Text activeLabel;
        [SerializeField] private Button[] tabs;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button scrim;
        [SerializeField] private CanvasGroup canvasGroup;
        private MercenaryDetailDto current;
        private int tabIndex;
        private Vector2 openPosition;
        private bool positionInitialized;
        private Coroutine animationRoutine;
        private bool toggleAllowed = true;
        private bool busy;

        public event Action CloseRequested;
        public event Action<bool> ActiveToggleRequested;
        public string BoundInstanceId => current?.InstanceId;
        public int SelectedTabIndex => tabIndex;
        public Button CloseButton => close;
        public Button GetTabButton(int index) => tabs[index];
        public string BodyText => body?.text;
        public string ErrorText => error?.text;
        public bool IsToggleInteractable => activeToggle != null && activeToggle.interactable;

        public void Configure(Image image, TMP_Text title, TMP_Text content, TMP_Text errorText, Button closeButton,
            Button toggle, TMP_Text toggleLabel, Button[] tabButtons, RectTransform drawerPanel, Button scrimButton, CanvasGroup group)
        {
            portrait = image; identity = title; body = content; error = errorText; close = closeButton; activeToggle = toggle;
            activeLabel = toggleLabel; tabs = tabButtons; panel = drawerPanel; scrim = scrimButton; canvasGroup = group;
        }

        private void Awake()
        {
            close.onClick.AddListener(Close);
            scrim.onClick.AddListener(Close);
            activeToggle.onClick.AddListener(Toggle);
            for (int index = 0; index < tabs.Length; index++) { int captured = index; tabs[index].onClick.AddListener(() => SelectTab(captured)); }
            EnsurePositionInitialized();
        }

        private void Close() => CloseRequested?.Invoke();
        private void Toggle() { if (current != null) ActiveToggleRequested?.Invoke(!current.Active); }
        public void Open(MercenaryDetailDto dto, Sprite sprite)
        {
            bool newSelection = current?.InstanceId != dto?.InstanceId;
            gameObject.SetActive(true);
            if (newSelection) tabIndex = 0;
            Bind(dto, sprite);
            StartAnimation(true);
        }

        public void Bind(MercenaryDetailDto dto, Sprite sprite)
        {
            bool newSelection = current?.InstanceId != dto?.InstanceId;
            current = dto ?? throw new ArgumentNullException(nameof(dto));
            if (newSelection) tabIndex = 0;
            portrait.sprite = sprite; portrait.enabled = sprite != null;
            identity.text = $"{dto.DisplayName}\n{dto.JobName} · {Grade(dto.GradeName)} · {dto.RankName} 레벨 {dto.Level}";
            activeLabel.text = dto.Active ? "활동 해제" : "활동 배치";
            error.text = string.Empty;
            RenderTab();
        }

        public void BindActivityAvailability(bool allowed, string reason)
        {
            toggleAllowed = allowed;
            activeToggle.interactable = allowed && !busy;
            error.text = allowed ? string.Empty : reason ?? string.Empty;
        }

        public void SetBusy(bool value)
        {
            busy = value;
            activeToggle.interactable = toggleAllowed && !busy;
        }
        public void ShowError(string value) => error.text = value ?? string.Empty;
        public void CloseDrawer()
        {
            bool animate = current != null && gameObject.activeInHierarchy;
            current = null;
            if (animate) StartAnimation(false);
            else SetClosedImmediate();
        }
        private void SelectTab(int value) { tabIndex = value; RenderTab(); }

        private void RenderTab()
        {
            if (current == null) return;
            body.text = tabIndex switch
            {
                0 => $"레벨 {current.Level}/{current.RankMaxLevel}\n경험치 {current.Exp}\n성격 {current.PersonalityName}\n특성 {string.Join(", ", current.TraitNames)}\n개인 골드 {current.PersonalGold}\n기여도 {current.Contribution}\n현재 행동 {AutonomyText(current.AutonomyState)}\n행동 이유 {ReasonText(current.ReasonCode)}\n지역 {RegionText(current.CurrentRegionId)}\n사냥을 시작하면 전투 능력치를 확인할 수 있습니다.",
                1 => $"무기 {Slot("WEAPON")}\n갑옷 {Slot("ARMOR")}\n투구 {Slot("HELMET")}\n장신구 {Slot("ACCESSORY")}\n물약 {PotionSummary()}\n가방에서 장비를 변경할 수 있습니다.",
                2 => $"{current.RankName} · 최대 레벨 {current.RankMaxLevel}\n{Grade(current.GradeName)}\n경험치 {current.Exp}\n승급 {PromotionText(current.PromotionStatus)}\n기여도 {current.Contribution}\n승급 심사에서 다음 등급에 도전할 수 있습니다.",
                _ => $"사냥 {Record("huntCount")}\n처치 {Record("killCount")}\n레이드 클리어 {Record("raidClearCount")}\n수집 아이템 {Record("itemsCollected")}"
            };
        }

        private string Slot(string key) => current.EquipmentSlots.TryGetValue(key, out string value) && value != null ? "장착됨" : "비어 있음";
        private long Record(string key) => current.Records.TryGetValue(key, out long value) ? value : 0;
        private string PotionSummary() => current.PotionStacks.Count == 0 ? "없음" : $"{current.PotionStacks.Count}종 보유";

        private static string AutonomyText(string state) => state switch
        {
            "IDLE" => "휴식",
            "HUNTING" => "사냥 중",
            "RETURNING" => "귀환 중",
            "INJURED" => "회복 중",
            _ => "대기"
        };

        private static string ReasonText(string reason) => reason switch
        {
            null or "" or "NONE" => "없음",
            "NO_ACTIVE_HUNT" => "진행 중인 사냥이 없음",
            "INJURED" => "부상 회복 필요",
            "PARTY_FULL" => "파티 인원 초과",
            _ => "현재 상황에 따라 자동 결정"
        };

        private static string RegionText(string region) => region switch
        {
            "REGION_R01" => "푸른 초원",
            "REGION_R02" => "안개 숲",
            "REGION_R03" => "붉은 협곡",
            "REGION_R04" => "얼어붙은 고원",
            "REGION_R05" => "마왕성 외곽",
            _ => "왕국"
        };

        private static string PromotionText(string status) => status switch
        {
            "READY" => "도전 가능",
            "IN_REVIEW" => "심사 중",
            "COMPLETED" => "완료",
            _ => "조건 미달"
        };

        private static string Grade(string value) => value switch
        {
            "C" => "일반 등급", "B" => "고급 등급", "A" => "희귀 등급", "S" => "영웅 등급", "SS" => "전설 등급", _ => value ?? "등급 미정"
        };

        private void StartAnimation(bool opening)
        {
            EnsurePositionInitialized();
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = StartCoroutine(Animate(opening));
        }

        private void EnsurePositionInitialized()
        {
            if (positionInitialized) return;
            openPosition = panel.anchoredPosition;
            positionInitialized = true;
        }

        private IEnumerator Animate(bool opening)
        {
            const float duration = 0.22f;
            Vector2 closed = openPosition + new Vector2(720f, 0f);
            Vector2 from = opening ? closed : panel.anchoredPosition;
            Vector2 to = opening ? openPosition : closed;
            float alphaFrom = opening ? 0f : canvasGroup.alpha;
            float alphaTo = opening ? 1f : 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float linear = Mathf.Clamp01(elapsed / duration);
                float cubicOut = 1f - Mathf.Pow(1f - linear, 3f);
                panel.anchoredPosition = Vector2.LerpUnclamped(from, to, cubicOut);
                canvasGroup.alpha = Mathf.LerpUnclamped(alphaFrom, alphaTo, cubicOut);
                yield return null;
            }
            panel.anchoredPosition = to;
            canvasGroup.alpha = alphaTo;
            animationRoutine = null;
            if (!opening) gameObject.SetActive(false);
        }

        private void SetClosedImmediate()
        {
            EnsurePositionInitialized();
            if (animationRoutine != null) { StopCoroutine(animationRoutine); animationRoutine = null; }
            if (panel != null) panel.anchoredPosition = openPosition + new Vector2(720f, 0f);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }
    }
}
