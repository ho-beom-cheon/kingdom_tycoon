using System;
using KingdomTycoon.Application.EquipmentGrowth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.EquipmentGrowth
{
    public sealed class EquipmentGrowthScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private TMP_Text facility;
        [SerializeField] private TMP_Text itemTitle;
        [SerializeField] private TMP_Text itemStats;
        [SerializeField] private TMP_Text refine;
        [SerializeField] private TMP_Text result;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private TMP_Text emptyTitle;
        [SerializeField] private TMP_Text emptyBody;
        [SerializeField] private GameObject toast;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Button close;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private Button enhance;
        [SerializeField] private Button refinePrevious;
        [SerializeField] private Button refineNext;
        [SerializeField] private Button rollRefine;
        [SerializeField] private Button accept;
        [SerializeField] private Button keep;
        [SerializeField] private Button dismantle;
        public CanvasGroup CanvasGroup => canvasGroup;

        public void Configure(CanvasGroup group, TMP_Text metaLabel, TMP_Text facilityLabel, TMP_Text title, TMP_Text stats, TMP_Text refineLabel, TMP_Text resultLabel,
            GameObject state, TMP_Text stateTitle, TMP_Text stateBody, GameObject toastRoot, TMP_Text toastLabel, Button closeButton, Button previousButton, Button nextButton,
            Button enhanceButton, Button refinePreviousButton, Button refineNextButton, Button rollButton, Button acceptButton, Button keepButton, Button dismantleButton)
        {
            canvasGroup = group; meta = metaLabel; facility = facilityLabel; itemTitle = title; itemStats = stats; refine = refineLabel; result = resultLabel;
            emptyState = state; emptyTitle = stateTitle; emptyBody = stateBody; toast = toastRoot; toastText = toastLabel; close = closeButton; previous = previousButton; next = nextButton;
            enhance = enhanceButton; refinePrevious = refinePreviousButton; refineNext = refineNextButton; rollRefine = rollButton; accept = acceptButton; keep = keepButton; dismantle = dismantleButton;
        }
        public void Bind(Action closeAction, Action previousAction, Action nextAction, Action enhanceAction, Action refinePreviousAction, Action refineNextAction, Action rollAction, Action acceptAction, Action keepAction, Action dismantleAction)
        {
            close.onClick.AddListener(() => closeAction()); previous.onClick.AddListener(() => previousAction()); next.onClick.AddListener(() => nextAction()); enhance.onClick.AddListener(() => enhanceAction());
            refinePrevious.onClick.AddListener(() => refinePreviousAction()); refineNext.onClick.AddListener(() => refineNextAction()); rollRefine.onClick.AddListener(() => rollAction());
            accept.onClick.AddListener(() => acceptAction()); keep.onClick.AddListener(() => keepAction()); dismantle.onClick.AddListener(() => dismantleAction());
        }
        public void ShowLoading() { emptyState.SetActive(true); emptyTitle.text = "장비 공방을 준비하는 중"; emptyBody.text = "장비·재료·대장간 상태를 확인하고 있습니다."; }
        public void ShowError(string code) { Debug.LogWarning(code); emptyState.SetActive(true); emptyTitle.text = "장비 공방을 열 수 없습니다"; emptyBody.text = "저장 데이터는 변경하지 않았습니다.\n잠시 후 다시 열어 주세요."; }
        public void Render(EquipmentGrowthOverviewDto value, int selectedIndex, string selectedRefine)
        {
            meta.text = $"콘텐츠 8  ·  저장 {value.Revision}  ·  개인 골드 {value.PersonalGold:N0}";
            bool active = value.FacilityState == "ACTIVE"; facility.text = $"대장간 {value.FacilityLevel}레벨  ·  <color={(active ? "#8ED6A3" : "#E9A05B")}>{(active ? "가동 중" : "정지")}</color>";
            if (value.Equipment.Count == 0) { emptyState.SetActive(true); emptyTitle.text = "성장시킬 장비가 없습니다"; emptyBody.text = "사냥 전리품을 보관하거나 상점에서 장비를 구매하면\n여기에서 강화·재련·분해할 수 있습니다."; SetActions(false, false); return; }
            emptyState.SetActive(false); selectedIndex = Mathf.Clamp(selectedIndex, 0, value.Equipment.Count - 1); GrowthEquipmentDto item = value.Equipment[selectedIndex];
            itemTitle.text = $"{EquipmentName(item.TemplateId)}  <color=#F1C96A>+{item.EnhancementLevel}</color>";
            itemStats.text = $"{item.Tier}단계  ·  {QualityName(item.QualityId)}\n전투력 <size=42><color=#F4E5B2>{item.Power:N0}</color></size>\n강화 천장 {item.PityBps / 100f:0.##}%  ·  {selectedIndex + 1}/{value.Equipment.Count}";
            string current = string.IsNullOrEmpty(item.RefineOptionId) ? "없음" : $"{OptionName(item.RefineOptionId)} +{item.RefineValueBps / 100f:0.##}%";
            string pending = string.IsNullOrEmpty(item.PendingOptionId) ? "후보 없음" : $"<color=#74D8C5>{OptionName(item.PendingOptionId)} +{item.PendingValueBps / 100f:0.##}%</color>";
            refine.text = $"현재 옵션  {current}\n새 후보  {pending}\n\n선택 옵션  <color=#F1C96A>{OptionName(selectedRefine)}</color>";
            result.text = $"최근 결과\n<color=#DCCCA9>{ResultName(value.LastResult)}</color>\n\n실패해도 장비가 파괴되거나\n강화 단계가 내려가지 않습니다.";
            SetActions(active && !item.Locked && !item.Equipped, !string.IsNullOrEmpty(item.PendingOptionId));
        }
        private void SetActions(bool enabled, bool pending) { enhance.interactable = enabled && !pending; rollRefine.interactable = enabled && !pending; dismantle.interactable = enabled && !pending; accept.interactable = enabled && pending; keep.interactable = enabled && pending; }
        public void ShowToast(string message) { toastText.text = message; toast.SetActive(true); }
        public void HideToast() => toast.SetActive(false);
        private static string EquipmentName(string value) => value switch { "EQ_T1_WARRIOR_WEAPON" => "초급 전사 무기", "EQ_T1_GUARDIAN_WEAPON" => "초급 수호자 무기", "EQ_T1_ARCHER_WEAPON" => "초급 궁수 무기", "EQ_T1_MAGE_WEAPON" => "초급 마법사 무기", "EQ_T1_CLERIC_WEAPON" => "초급 성직자 무기", _ => "이름 없는 장비" };
        private static string QualityName(string value) => value switch { "QUALITY_COMMON" => "일반", "QUALITY_FINE" => "고급", "QUALITY_RARE" => "희귀", "QUALITY_LEGACY" => "영웅", "QUALITY_RELIC" => "유물", _ => "일반" };
        private static string OptionName(string value) => value switch { "REF_ATK_POWER" => "공격력", "REF_CRIT" => "치명타", "REF_DEF" => "방어력", "REF_HP" => "생명력", "REF_MATERIAL" => "재료 획득", "REF_RARE_FIND" => "희귀 발견", "REF_FIRE" => "화염 피해", "REF_FROST" => "냉기 저항", "REF_POISON" => "중독 위력", "REF_BOSS" => "보스 피해", "REF_PART" => "부위 피해", _ => value };
        private static string ResultName(string value) => value switch { "NONE" => "아직 작업 기록이 없습니다", "P10_ENHANCE_SUCCESS" => "강화 성공", "P10_ENHANCE_FAILED" => "강화 실패 · 천장 누적", "P10_REFINE_ROLLED" => "재련 후보 생성", "P10_REFINE_ACCEPTED" => "새 옵션 적용", "P10_REFINE_KEPT_CURRENT" => "기존 옵션 유지", "P10_DISMANTLED" => "분해 및 재료 회수", _ => "작업 결과를 저장했습니다" };
    }
}
