using System;
using KingdomTycoon.Application.Production;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Production
{
    public sealed class ProductionScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private TMP_Text[] facilityTexts;
        [SerializeField] private TMP_Text queueText;
        [SerializeField] private TMP_Text targetsText;
        [SerializeField] private TMP_Text selectedText;
        [SerializeField] private TMP_Text stateTitle;
        [SerializeField] private TMP_Text stateBody;
        [SerializeField] private GameObject statePanel;
        [SerializeField] private GameObject toast;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Button close;
        [SerializeField] private Button automate;
        [SerializeField] private Button advance;
        [SerializeField] private Button minus;
        [SerializeField] private Button plus;
        [SerializeField] private Button[] targetButtons;

        public CanvasGroup CanvasGroup => canvasGroup;

        public void Configure(CanvasGroup group, TMP_Text metaLabel, TMP_Text[] facilities, TMP_Text queue, TMP_Text targets, TMP_Text selected,
            GameObject state, TMP_Text stateHeading, TMP_Text stateMessage, GameObject toastRoot, TMP_Text toastLabel,
            Button closeButton, Button automationButton, Button tickButton, Button minusButton, Button plusButton, Button[] selectors)
        {
            canvasGroup = group; meta = metaLabel; facilityTexts = facilities; queueText = queue; targetsText = targets; selectedText = selected;
            statePanel = state; stateTitle = stateHeading; stateBody = stateMessage; toast = toastRoot; toastText = toastLabel;
            close = closeButton; automate = automationButton; advance = tickButton; minus = minusButton; plus = plusButton; targetButtons = selectors;
        }

        public void Bind(Action closeAction, Action automateAction, Action tickAction, Action decrease, Action increase, Action<int> select)
        {
            close.onClick.AddListener(() => closeAction()); automate.onClick.AddListener(() => automateAction()); advance.onClick.AddListener(() => tickAction());
            minus.onClick.AddListener(() => decrease()); plus.onClick.AddListener(() => increase());
            for (int index = 0; index < targetButtons.Length; index++) { int captured = index; targetButtons[index].onClick.AddListener(() => select(captured)); }
        }

        public void ShowLoading()
        {
            statePanel.SetActive(true); stateTitle.text = "생산 현황을 불러오는 중"; stateBody.text = "대기열과 상점 재고를 검증하고 있습니다.";
        }

        public void ShowError(string code)
        {
            Debug.LogWarning(code); statePanel.SetActive(true); stateTitle.text = "생산 현황을 표시할 수 없습니다"; stateBody.text = "저장 데이터는 변경하지 않았습니다.\n다시 열어 주세요.";
        }

        public void Render(ProductionOverviewDto value, int selectedIndex)
        {
            statePanel.SetActive(false); meta.text = $"콘텐츠 7  ·  생산 진행 {value.CurrentTick:N0}  ·  저장 {value.Revision}";
            for (int index = 0; index < facilityTexts.Length; index++)
            {
                if (index >= value.Facilities.Count) { facilityTexts[index].text = "시설 정보 없음"; continue; }
                ProductionFacilityDto item = value.Facilities[index]; string name = Name(item.FacilityId); string npc = string.IsNullOrEmpty(item.NpcProfession) ? "담당 관리인 없음" : $"{NpcName(item.NpcProfession)} · {ProficiencyName(item.Proficiency)}  숙련도 {item.Xp:N0}";
                facilityTexts[index].text = $"<size=30><color=#F2D47A>{name}</color></size>  {item.Level}레벨\n{npc}\n대기열 {item.QueueCount}/{item.QueueCapacity}  ·  남은 진행 {item.RemainingTicks:N0}\n<color={(item.StoppedReason == "NONE" ? "#8ECF91" : "#E7A74C")}>{Reason(item.StoppedReason)}</color>";
            }
            targetsText.text = "";
            for (int index = 0; index < value.Targets.Count; index++)
            {
                StockTargetDto target = value.Targets[index]; string marker = index == selectedIndex ? "▶" : " ";
                targetsText.text += $"{marker} {ProductName(target.ProductId),-11}  {target.Current}+{target.Queued}/{target.Target}  {(target.Enabled ? "자동" : "꺼짐")}\n";
            }
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Math.Max(0, value.Targets.Count - 1));
            StockTargetDto selected = value.Targets.Count == 0 ? null : value.Targets[selectedIndex];
            selectedText.text = selected == null ? "재고 목표 없음" : $"선택: {ProductName(selected.ProductId)}\n현재 {selected.Current} · 대기 {selected.Queued} · 목표 <color=#F2D47A>{selected.Target}</color>\n{Reason(selected.StopReason)}";
            queueText.text = value.Facilities.Count == 0 ? "대기열 없음" : $"가동 시설 {CountReady(value)}/{value.Facilities.Count}\n최근 이벤트 {value.RecentEvents}\n\n자동 보충은 목표와 큐 출력을 함께 계산합니다.\n생산 완료품은 왕국 상점에 즉시 입고됩니다.";
        }

        public void ShowToast(string message) { toastText.text = message; toast.SetActive(true); }
        public void HideToast() => toast.SetActive(false);
        private static int CountReady(ProductionOverviewDto value) { int count = 0; foreach (ProductionFacilityDto item in value.Facilities) if (item.StoppedReason == "NONE") count++; return count; }
        private static string Name(string value) => value switch { "FAC_BLACKSMITH" => "대장간", "FAC_ALCHEMY" => "연금 공방", "FAC_INFIRMARY" => "진료소", _ => value };
        private static string NpcName(string value) => value switch { "NPC_BLACKSMITH" => "대장장이", "NPC_ALCHEMIST" => "연금술사", "NPC_HEALER" => "치유사", _ => value };
        private static string ProficiencyName(string value) => value switch { "NPC_APPRENTICE" => "견습", "NPC_SKILLED" => "숙련", "NPC_ARTISAN" => "장인", "NPC_MASTER" => "명장", _ => value ?? "-" };
        private static string ProductName(string value) => value switch { "POT_HEAL_SMALL" => "소형 회복 물약", "EQ_T1_WARRIOR_WEAPON" => "전사 무기", "EQ_T1_GUARDIAN_WEAPON" => "수호자 무기", "EQ_T1_ARCHER_WEAPON" => "궁수 무기", "EQ_T1_MAGE_WEAPON" => "마법사 무기", "EQ_T1_CLERIC_WEAPON" => "성직자 무기", _ => value };
        private static string Reason(string value) => value switch
        {
            "NONE" => "생산 가능", "P09_FACILITY_LOCKED" => "시설을 먼저 건설하세요", "P09_FACILITY_INACTIVE" => "시설 가동을 확인하세요",
            "P09_NPC_REQUIRED" => "담당 관리인을 배치하세요", "P09_MATERIAL_INSUFFICIENT" => "재료가 부족합니다", "P09_QUEUE_FULL" => "대기열이 가득 찼습니다",
            "P09_TARGET_REACHED" => "목표 재고를 확보했습니다", "P09_OUTPUT_CAPACITY" => "상점 수용량이 부족합니다", "P09_PROFICIENCY_REQUIRED" => "관리인 숙련도가 부족합니다",
            "P09_RECIPE_LOCKED" => "시설 레벨이 부족합니다", _ => "생산 조건을 확인하세요"
        };
    }
}
