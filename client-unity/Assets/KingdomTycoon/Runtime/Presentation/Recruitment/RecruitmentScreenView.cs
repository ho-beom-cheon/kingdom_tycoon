using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Application.Recruitment;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Recruitment
{
    public sealed class RecruitmentScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text walletText;
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private TMP_Text authorityText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button tavernTab;
        [SerializeField] private Button specialTab;
        [SerializeField] private Button historyTab;
        [SerializeField] private GameObject tavernRoot;
        [SerializeField] private GameObject specialRoot;
        [SerializeField] private GameObject historyRoot;
        [SerializeField] private Button[] candidateButtons;
        [SerializeField] private TMP_Text[] candidateLabels;
        [SerializeField] private TMP_Text detailTitle;
        [SerializeField] private TMP_Text detailBody;
        [SerializeField] private Button refreshButton;
        [SerializeField] private TMP_Text refreshLabel;
        [SerializeField] private Button lockButton;
        [SerializeField] private TMP_Text lockLabel;
        [SerializeField] private Button hireButton;
        [SerializeField] private TMP_Text hireLabel;
        [SerializeField] private Button specialRecruitButton;
        [SerializeField] private TMP_Text specialRecruitLabel;
        [SerializeField] private TMP_Text pitySLabel;
        [SerializeField] private TMP_Text pitySsLabel;
        [SerializeField] private Image pitySFill;
        [SerializeField] private Image pitySsFill;
        [SerializeField] private TMP_Text historyText;
        [SerializeField] private GameObject statePanel;
        [SerializeField] private TMP_Text stateTitle;
        [SerializeField] private TMP_Text stateBody;
        [SerializeField] private GameObject toast;
        [SerializeField] private TMP_Text toastText;

        private RecruitmentOverviewDto overview;
        private int selectedIndex;
        private string currentTab = "TAVERN";

        public event Action CloseRequested;
        public event Action RefreshRequested;
        public event Action<string, bool> LockRequested;
        public event Action<string> HireRequested;
        public event Action SpecialRequested;
        public event Action HistoryRequested;

        private void OnEnable() => Bind();

        public void Configure(CanvasGroup group, TMP_Text wallet, TMP_Text capacity, TMP_Text authority, Button close,
            Button tavern, Button special, Button history, GameObject tavernPanel, GameObject specialPanel, GameObject historyPanel,
            Button[] candidates, TMP_Text[] candidateTexts, TMP_Text title, TMP_Text body, Button refresh, TMP_Text refreshText,
            Button lockAction, TMP_Text lockText, Button hire, TMP_Text hireText, Button specialAction, TMP_Text specialText,
            TMP_Text pityS, TMP_Text pitySs, Image sFill, Image ssFill, TMP_Text historyList,
            GameObject state, TMP_Text stateHeading, TMP_Text stateDescription, GameObject toastPanel, TMP_Text toastLabel)
        {
            canvasGroup = group; walletText = wallet; capacityText = capacity; authorityText = authority; closeButton = close;
            tavernTab = tavern; specialTab = special; historyTab = history; tavernRoot = tavernPanel; specialRoot = specialPanel; historyRoot = historyPanel;
            candidateButtons = candidates; candidateLabels = candidateTexts; detailTitle = title; detailBody = body; refreshButton = refresh; refreshLabel = refreshText;
            lockButton = lockAction; lockLabel = lockText; hireButton = hire; hireLabel = hireText; specialRecruitButton = specialAction; specialRecruitLabel = specialText;
            pitySLabel = pityS; pitySsLabel = pitySs; pitySFill = sFill; pitySsFill = ssFill; historyText = historyList;
            statePanel = state; stateTitle = stateHeading; stateBody = stateDescription; toast = toastPanel; toastText = toastLabel;
            Bind();
        }

        public void Render(RecruitmentOverviewDto value, IReadOnlyList<string> historyLines = null)
        {
            overview = value; selectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, value.Candidates.Count - 1));
            walletText.text = $"왕국 골드  <color=#F4D27A>{value.KingdomGold:N0}</color>    모집권  <color=#83E0C5>{value.Tickets}</color>    무료 프리미엄  <color=#8ED7FF>{value.FreePremium:N0}</color>";
            capacityText.text = $"숙소  {value.Owned} / {value.Limit}"; authorityText.text = value.Authority == "MOCK_ONLY" ? "개발용 모의 모집" : "서버 검증 완료";
            for (int index = 0; index < candidateButtons.Length; index++)
            {
                bool visible = index < value.Candidates.Count; candidateButtons[index].gameObject.SetActive(visible); if (!visible) continue;
                RecruitmentCandidateDto candidate = value.Candidates[index]; candidateLabels[index].text = $"<size=17>{Grade(candidate.GradeId)}</size>\n<b>{candidate.DisplayName}</b>\n<size=18>{Job(candidate.JobId)}</size>\n<size=16>{candidate.HireCost:N0} 골드 {(candidate.Locked ? "  ◆ 잠금" : string.Empty)}</size>";
                candidateButtons[index].GetComponent<Image>().color = index == selectedIndex ? new Color32(116, 82, 45, 255) : GradeColor(candidate.GradeId);
            }
            RenderSelected();
            long s = value.Pity.TryGetValue("PITY_S_PLUS", out long sValue) ? sValue : 0; long ss = value.Pity.TryGetValue("PITY_SS", out long ssValue) ? ssValue : 0;
            pitySLabel.text = $"영웅 이상 보장  {s} / 10"; pitySsLabel.text = $"전설 확정 천장  {ss} / 80"; SetProgress(pitySFill, s / 10f); SetProgress(pitySsFill, ss / 80f);
            bool capacity = value.Owned >= value.Limit; specialRecruitButton.interactable = !capacity && (value.Tickets > 0 || value.FreePremium >= 300); specialRecruitLabel.text = capacity ? "숙소가 가득 찼습니다" : value.Tickets > 0 ? "모집권 1장으로 특별 모집" : "무료 프리미엄 300으로 모집";
            if (historyLines != null) historyText.text = historyLines.Count == 0 ? "아직 모집 기록이 없습니다." : string.Join("\n", historyLines.Select(HistoryLine));
            if (value.Candidates.Count == 0 && currentTab == "TAVERN") ShowState("주점이 조용합니다", "무료 갱신으로 새로운 후보를 불러오세요."); else HideState();
            ShowTab(currentTab);
        }

        public void ShowTab(string tab)
        {
            currentTab = tab; tavernRoot.SetActive(tab == "TAVERN"); specialRoot.SetActive(tab == "SPECIAL"); historyRoot.SetActive(tab == "HISTORY");
            SetTabColor(tavernTab, tab == "TAVERN"); SetTabColor(specialTab, tab == "SPECIAL"); SetTabColor(historyTab, tab == "HISTORY");
        }

        public void ShowState(string title, string body) { stateTitle.text = title; stateBody.text = body; statePanel.SetActive(true); }
        public void HideState() => statePanel.SetActive(false);
        public void ShowToast(string message, bool error = false) { toastText.text = message; toast.GetComponent<Image>().color = error ? new Color32(126, 54, 50, 252) : new Color32(38, 105, 81, 252); toast.SetActive(true); }
        public void SetVisible(bool visible) { gameObject.SetActive(visible); if (canvasGroup != null) { canvasGroup.alpha = visible ? 1 : 0; canvasGroup.interactable = visible; canvasGroup.blocksRaycasts = visible; } }

        private void Bind()
        {
            closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            tavernTab.onClick.RemoveAllListeners(); tavernTab.onClick.AddListener(() => ShowTab("TAVERN")); specialTab.onClick.RemoveAllListeners(); specialTab.onClick.AddListener(() => ShowTab("SPECIAL")); historyTab.onClick.RemoveAllListeners(); historyTab.onClick.AddListener(() => { ShowTab("HISTORY"); HistoryRequested?.Invoke(); });
            refreshButton.onClick.RemoveAllListeners(); refreshButton.onClick.AddListener(() => RefreshRequested?.Invoke()); lockButton.onClick.RemoveAllListeners(); lockButton.onClick.AddListener(() => { RecruitmentCandidateDto value = Selected(); if (value != null) LockRequested?.Invoke(value.CandidateId, !value.Locked); });
            hireButton.onClick.RemoveAllListeners(); hireButton.onClick.AddListener(() => { RecruitmentCandidateDto value = Selected(); if (value != null) HireRequested?.Invoke(value.CandidateId); }); specialRecruitButton.onClick.RemoveAllListeners(); specialRecruitButton.onClick.AddListener(() => SpecialRequested?.Invoke());
            for (int index = 0; index < candidateButtons.Length; index++) { int captured = index; candidateButtons[index].onClick.RemoveAllListeners(); candidateButtons[index].onClick.AddListener(() => { selectedIndex = captured; Render(overview); }); }
        }

        private void RenderSelected()
        {
            RecruitmentCandidateDto value = Selected(); if (value == null) { detailTitle.text = "후보를 기다리는 중"; detailBody.text = "갱신 후 후보의 직업과 등급을 비교할 수 있습니다."; lockButton.interactable = hireButton.interactable = false; return; }
            detailTitle.text = value.DisplayName; detailBody.text = $"{Grade(value.GradeId)}  ·  {Job(value.JobId)}\n\n잠재 등급은 고정되지만 성장과 장비, 승급으로 전투 역할이 확장됩니다.\n\n고용비  <color=#F4D27A>{value.HireCost:N0} 왕국 골드</color>";
            lockButton.interactable = true; hireButton.interactable = overview.Owned < overview.Limit && overview.KingdomGold >= value.HireCost; lockLabel.text = value.Locked ? "후보 잠금 해제" : "후보 잠금"; hireLabel.text = overview.Owned >= overview.Limit ? "숙소가 가득 찼습니다" : $"{value.HireCost:N0} 골드로 고용";
            DateTimeOffset now = DateTimeOffset.UtcNow; bool free = !overview.NextFreeRefreshAt.HasValue || now >= overview.NextFreeRefreshAt.Value; refreshLabel.text = free ? "무료 후보 갱신" : $"즉시 갱신  ·  골드 사용";
        }

        private RecruitmentCandidateDto Selected() => overview != null && selectedIndex < overview.Candidates.Count ? overview.Candidates[selectedIndex] : null;
        private static string Job(string id) => id switch { "JOB_WARRIOR" => "전사", "JOB_GUARDIAN" => "수호자", "JOB_ARCHER" => "궁수", "JOB_MAGE" => "마법사", "JOB_CLERIC" => "성직자", _ => id };
        private static string Grade(string id) => id switch { "GRADE_C" => "일반", "GRADE_B" => "고급", "GRADE_A" => "희귀", "GRADE_S" => "영웅", "GRADE_SS" => "전설", _ => "등급 미정" };
        private static Color GradeColor(string id) => id switch { "GRADE_A" => new Color32(48, 77, 115, 255), "GRADE_B" => new Color32(50, 84, 57, 255), "GRADE_S" => new Color32(82, 60, 120, 255), "GRADE_SS" => new Color32(130, 91, 36, 255), _ => new Color32(62, 63, 61, 255) };
        private static void SetProgress(Image fill, float value) => fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        private static void SetTabColor(Button button, bool active) { ColorBlock colors = button.colors; button.GetComponent<Image>().color = active ? new Color32(132, 82, 39, 255) : new Color32(35, 48, 49, 255); button.colors = colors; }
        private static string HistoryLine(string raw)
        {
            string[] values = raw.Split('|'); if (values.Length < 4) return "모집 기록을 확인할 수 없습니다."; string type = values[0] == "TAVERN" ? "주점 고용" : "특별 모집"; return $"<color=#D9B96D>{type}</color>    {Grade(values[1])} 용병    비용 {values[2]}    <size=17>{values[3].Replace("T", " ").Replace(".000Z", " 세계 표준시")}</size>";
        }
    }
}
