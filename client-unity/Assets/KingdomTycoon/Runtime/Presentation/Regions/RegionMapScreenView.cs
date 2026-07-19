using System;
using System.Linq;
using System.Text;
using KingdomTycoon.Application.Regions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Regions
{
    public sealed class RegionMapScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private TMP_Text kingdomStatus;
        [SerializeField] private Button[] regionButtons;
        [SerializeField] private TMP_Text[] regionLabels;
        [SerializeField] private TMP_Text regionTitle;
        [SerializeField] private TMP_Text regionTier;
        [SerializeField] private TMP_Text regionProgress;
        [SerializeField] private Image progressFill;
        [SerializeField] private TMP_Text requirements;
        [SerializeField] private TMP_Text deployment;
        [SerializeField] private TMP_Text activity;
        [SerializeField] private Button close;
        [SerializeField] private Button policy;
        [SerializeField] private TMP_Text policyLabel;
        [SerializeField] private Button hunt;
        [SerializeField] private TMP_Text huntLabel;
        [SerializeField] private GameObject state;
        [SerializeField] private TMP_Text stateTitle;
        [SerializeField] private TMP_Text stateBody;
        [SerializeField] private GameObject toast;
        [SerializeField] private TMP_Text toastText;
        public CanvasGroup CanvasGroup => canvasGroup;

        public void Configure(CanvasGroup group, TMP_Text metaLabel, TMP_Text kingdomLabel, Button[] nodeButtons, TMP_Text[] nodeLabels,
            TMP_Text titleLabel, TMP_Text tierLabel, TMP_Text progressLabel, Image progressImage, TMP_Text requirementsLabel,
            TMP_Text deploymentLabel, TMP_Text activityLabel, Button closeButton, Button policyButton, TMP_Text policyText,
            Button huntButton, TMP_Text huntText, GameObject stateRoot, TMP_Text stateTitleLabel, TMP_Text stateBodyLabel,
            GameObject toastRoot, TMP_Text toastLabel)
        {
            canvasGroup = group; meta = metaLabel; kingdomStatus = kingdomLabel; regionButtons = nodeButtons; regionLabels = nodeLabels;
            regionTitle = titleLabel; regionTier = tierLabel; regionProgress = progressLabel; progressFill = progressImage;
            requirements = requirementsLabel; deployment = deploymentLabel; activity = activityLabel; close = closeButton;
            policy = policyButton; policyLabel = policyText; hunt = huntButton; huntLabel = huntText; state = stateRoot;
            stateTitle = stateTitleLabel; stateBody = stateBodyLabel; toast = toastRoot; toastText = toastLabel;
        }

        public void Bind(Action closeAction, Action<int> selectAction, Action policyAction, Action huntAction)
        {
            close.onClick.AddListener(() => closeAction()); policy.onClick.AddListener(() => policyAction()); hunt.onClick.AddListener(() => huntAction());
            for (int index = 0; index < regionButtons.Length; index++) { int selected = index; regionButtons[index].onClick.AddListener(() => selectAction(selected)); }
        }

        public void ShowLoading() { state.SetActive(true); stateTitle.text = "왕국 지도를 펼치는 중"; stateBody.text = "지역 개방 조건과 파견 정책을 불러오고 있습니다."; }
        public void ShowError(string code) { state.SetActive(true); stateTitle.text = "지도를 불러오지 못했습니다"; stateBody.text = "저장 데이터는 변경하지 않았습니다.\n\n" + code; }
        public void HideToast() => toast.SetActive(false);
        public void ShowToast(string message) { toastText.text = message; toast.SetActive(true); }

        public void Render(RegionOverviewDto value, int selectedIndex)
        {
            state.SetActive(false); meta.text = $"content.10  ·  r{value.Revision}  ·  5개 지역";
            kingdomStatus.text = $"{Stage(value.KingdomStageId)}  ·  개방 {value.UnlockedCount}/5  ·  파견 정책 정상";
            selectedIndex = Mathf.Clamp(selectedIndex, 0, value.Regions.Count - 1);
            for (int index = 0; index < regionButtons.Length && index < value.Regions.Count; index++)
            {
                RegionSummaryDto node = value.Regions[index]; bool selected = index == selectedIndex;
                regionLabels[index].text = node.Unlocked
                    ? $"<size=15>0{node.Order}</size>\n<b>{node.Name}</b>\n<size=16>{node.ProgressPercent}% · {(node.Allowed ? "파견 허용" : "파견 중지")}</size>"
                    : $"<size=15>0{node.Order}</size>\n<b>미개척 지역</b>\n<size=16>{node.LockReason}</size>";
                Image image = regionButtons[index].targetGraphic as Image;
                if (image != null) image.color = selected ? new Color32(191, 133, 53, 255) : node.Unlocked ? new Color32(43, 84, 78, 255) : new Color32(49, 54, 58, 245);
            }
            RegionSummaryDto row = value.Regions[selectedIndex]; regionTitle.text = row.Unlocked ? row.Name : "봉인된 개척지";
            regionTier.text = $"TIER {row.Tier}  ·  {Environment(row.EnvironmentTag)}  ·  최소 {Rank(row.MinimumRankId)}  ·  권장 전투력 {row.RecommendedPower:N0}";
            regionProgress.text = $"지역 조사도  <color=#F4D27A>{row.ProgressPercent}%</color>";
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(row.ProgressPercent / 100f), 1f);
            var checklist = new StringBuilder();
            foreach (RegionRequirementDto requirement in row.Requirements)
                checklist.Append(requirement.Met ? "<color=#83E0C5>✓</color> " : "<color=#E6A15D>◆</color> ").Append(requirement.Label).Append("   ").Append(requirement.Current.ToString("N0")).Append(" / ").Append(requirement.Required.ToString("N0")).AppendLine();
            requirements.text = checklist.Length == 0 ? "<color=#83E0C5>✓</color> 기본 영지 즉시 개방" : checklist.ToString().TrimEnd();
            RegionMercenaryDto[] eligible = value.Mercenaries.Where(item => item.Active && item.AutonomyState == "IDLE_TOWN" && item.CurrentRegionId == null && item.RankOrder >= row.MinimumRankOrder).ToArray();
            deployment.text = $"권장 파티  {row.RecommendedPartySize}명\n회복 물약  {row.RecommendedPotions}개\n가방 여유  {row.ReserveSlots}칸\n\n<size=19>출전 가능 용병 {eligible.Length}명</size>";
            activity.text = $"누적 사냥  {row.HuntCount:N0}회\n정예 처치  {row.EliteKillCount:N0}회\n도달 최고 등급  {Rank(row.HighestRankReachedId)}\n\n<size=18>{Result(value.LastResultCode)}</size>";
            policyLabel.text = row.Allowed ? "파견 허용 중" : "파견 중지됨"; policy.interactable = row.Unlocked && row.ActiveMercenaries == 0;
            huntLabel.text = !row.Unlocked ? "개방 조건 확인" : !row.Allowed ? "파견 정책이 중지됨" : eligible.Length == 0 ? "출전 가능한 용병 없음" : "추천 파티로 사냥 시작";
            hunt.interactable = row.Unlocked && row.Allowed && eligible.Length > 0;
        }

        private static string Stage(string value) => value switch { "KINGDOM_1" => "초기 왕국", "KINGDOM_2" => "성장 왕국", "KINGDOM_3" => "번영 왕국", "KINGDOM_4" => "대왕국", _ => value };
        private static string Rank(string value) => value switch { "RANK_APPRENTICE" => "수습", "RANK_REGULAR" => "정식", "RANK_SKILLED" => "숙련", "RANK_ELITE" => "정예", "RANK_HERO" => "영웅", "RANK_LEGEND" => "전설", null => "-", _ => value };
        private static string Environment(string value) => value switch { "MEADOW" => "초원", "FOREST" => "고대림", "MINE" => "폐광", "SWAMP" => "습지", "FROST_RUIN" => "서리 유적", _ => value };
        private static string Result(string value) => value switch { "P12_READY" => "개척대가 다음 명령을 기다립니다.", "P12_REGION_UNLOCKED" => "새 지역이 왕국 지도에 등록됐습니다.", "P12_POLICY_UPDATED" => "파견 정책을 갱신했습니다.", "P12_HUNT_SETTLED" => "사냥 기록과 조사도를 반영했습니다.", _ => value };
    }
}
