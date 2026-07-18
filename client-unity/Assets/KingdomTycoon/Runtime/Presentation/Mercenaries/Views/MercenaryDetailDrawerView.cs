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
            identity.text = $"{dto.DisplayName}\n{dto.JobName} · {dto.GradeName} · {dto.RankName} Lv.{dto.Level}";
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
                0 => $"레벨 {current.Level}/{current.RankMaxLevel}\nEXP {current.Exp}\n성격 {current.PersonalityName}\n특성 {string.Join(", ", current.TraitNames)}\n개인 골드 {current.PersonalGold}\n기여도 {current.Contribution}\n현재 행동 {current.AutonomyState}\n행동 이유 {current.ReasonCode}\n지역 {current.CurrentRegionId ?? "왕국"}\n전투 능력치는 P06에서 개방됩니다.",
                1 => $"무기 {Slot("WEAPON")}\n갑옷 {Slot("ARMOR")}\n투구 {Slot("HELMET")}\n장신구 {Slot("ACCESSORY")}\n물약 {PotionSummary()}\n장비 변경은 P07에서 개방됩니다.",
                2 => $"{current.RankName} · 최대 레벨 {current.RankMaxLevel}\n{current.GradeName}\nEXP {current.Exp}\n승급 {current.PromotionStatus}\n기여도 {current.Contribution}\n승급 심사는 P11에서 개방됩니다.",
                _ => $"사냥 {Record("huntCount")}\n처치 {Record("killCount")}\n레이드 클리어 {Record("raidClearCount")}\n수집 아이템 {Record("itemsCollected")}"
            };
        }

        private string Slot(string key) => current.EquipmentSlots.TryGetValue(key, out string value) && value != null ? value : "비어 있음";
        private long Record(string key) => current.Records.TryGetValue(key, out long value) ? value : 0;
        private string PotionSummary() => current.PotionStacks.Count == 0 ? "없음" : string.Join(", ", current.PotionStacks);

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
