using System;
using System.Linq;
using System.Text;
using KingdomTycoon.Application.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Progression
{
    public sealed class ProgressionScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private TMP_Text guild;
        [SerializeField] private TMP_Text mercenary;
        [SerializeField] private TMP_Text journey;
        [SerializeField] private TMP_Text experience;
        [SerializeField] private TMP_Text requirements;
        [SerializeField] private TMP_Text costs;
        [SerializeField] private TMP_Text equipment;
        [SerializeField] private TMP_Text result;
        [SerializeField] private GameObject state;
        [SerializeField] private TMP_Text stateTitle;
        [SerializeField] private TMP_Text stateBody;
        [SerializeField] private GameObject toast;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private Button close;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private Button primary;
        [SerializeField] private TMP_Text primaryText;
        public CanvasGroup CanvasGroup => canvasGroup;

        public void Configure(CanvasGroup group, TMP_Text metaLabel, TMP_Text guildLabel, TMP_Text mercenaryLabel, TMP_Text journeyLabel,
            TMP_Text experienceLabel, TMP_Text requirementsLabel, TMP_Text costsLabel, TMP_Text equipmentLabel, TMP_Text resultLabel,
            GameObject stateRoot, TMP_Text stateTitleLabel, TMP_Text stateBodyLabel, GameObject toastRoot, TMP_Text toastLabel,
            Button closeButton, Button previousButton, Button nextButton, Button primaryButton, TMP_Text primaryLabel)
        {
            canvasGroup = group; meta = metaLabel; guild = guildLabel; mercenary = mercenaryLabel; journey = journeyLabel; experience = experienceLabel;
            requirements = requirementsLabel; costs = costsLabel; equipment = equipmentLabel; result = resultLabel; state = stateRoot;
            stateTitle = stateTitleLabel; stateBody = stateBodyLabel; toast = toastRoot; toastText = toastLabel; close = closeButton;
            previous = previousButton; next = nextButton; primary = primaryButton; primaryText = primaryLabel;
        }

        public void Bind(Action closeAction, Action previousAction, Action nextAction, Action primaryAction)
        { close.onClick.AddListener(() => closeAction()); previous.onClick.AddListener(() => previousAction()); next.onClick.AddListener(() => nextAction()); primary.onClick.AddListener(() => primaryAction()); }

        public void ShowLoading() { state.SetActive(true); stateTitle.text = "길드 기록을 확인하는 중"; stateBody.text = "레벨·기여도·승급 심사 상태를 불러오고 있습니다."; }
        public void ShowError(string code) { state.SetActive(true); stateTitle.text = "성장 심사를 열 수 없습니다"; stateBody.text = "저장 데이터는 변경하지 않았습니다.\n\n" + code; }
        public void HideToast() => toast.SetActive(false);
        public void ShowToast(string message) { toastText.text = message; toast.SetActive(true); }

        public void Render(ProgressionOverviewDto value, int selectedIndex)
        {
            meta.text = $"content.9  ·  r{value.Revision}  ·  왕국 골드 {value.KingdomGold:N0}";
            bool guildActive = value.GuildState == "ACTIVE";
            guild.text = $"모험가 길드 Lv.{value.GuildLevel}  ·  <color={(guildActive ? "#82DEC5" : "#E7A15C")}>{(guildActive ? "심사 접수 중" : "운영 중단")}</color>";
            if (value.Mercenaries.Count == 0)
            {
                state.SetActive(true); stateTitle.text = "등록된 용병이 없습니다"; stateBody.text = "주점에서 용병을 모집하면 성장 경로와 심사 조건을 확인할 수 있습니다."; primary.interactable = false; return;
            }
            state.SetActive(false); selectedIndex = Mathf.Clamp(selectedIndex, 0, value.Mercenaries.Count - 1); PromotionMercenaryDto row = value.Mercenaries[selectedIndex];
            mercenary.text = $"<color=#F3D071>{row.Name}</color>\n<size=24>{Grade(row.GradeId)} · {Rank(row.RankId)} · Lv.{row.Level}</size>\n<size=19>{selectedIndex + 1}/{value.Mercenaries.Count} · {Status(row.Status)}</size>";
            string[] path = { "RANK_APPRENTICE", "RANK_REGULAR", "RANK_SKILLED", "RANK_ELITE", "RANK_HERO", "RANK_LEGEND" };
            journey.text = string.Join("   ›   ", path.Select(id => id == row.RankId ? $"<color=#F3D071><b>{Rank(id)}</b></color>" : Rank(id)));
            float ratio = row.ExperienceToNext == 0 ? 1 : Mathf.Clamp01((float)row.Experience / row.ExperienceToNext);
            experience.text = $"현재 성장\n<size=35><color=#F4E6BD>Lv.{row.Level}</color> / {row.MaxLevel}</size>\nEXP {row.Experience:N0} / {row.ExperienceToNext:N0}  ·  {ratio * 100f:0}%";
            var checklist = new StringBuilder(); foreach (PromotionRequirementDto requirement in row.Requirements)
                checklist.Append(requirement.Met ? "<color=#82DEC5>✓</color> " : "<color=#E7A15C>!</color> ").Append(requirement.Label).Append("  ").Append(requirement.Current.ToString("N0")).Append(" / ").Append(requirement.Required.ToString("N0")).AppendLine();
            requirements.text = checklist.ToString().TrimEnd();
            costs.text = row.NextRankId == null ? "최종 랭크에 도달했습니다." : $"{Rank(row.NextRankId)} 심사 비용\n개인 골드  {row.PersonalGold:N0} / {row.PersonalGoldCost:N0}\n왕국 골드  {value.KingdomGold:N0} / {row.KingdomGoldCost:N0}";
            bool recommended = row.EquipmentScore >= row.RecommendedEquipmentScore;
            equipment.text = $"장비 준비도\n<size=32><color={(recommended ? "#82DEC5" : "#E7A15C")}>{row.EquipmentScore:N0}</color></size> / 권장 {row.RecommendedEquipmentScore:N0}\n{(recommended ? "권장 전투력을 충족했습니다." : "권장 미달이어도 승급은 가능합니다.")}";
            result.text = row.Status == "IN_REVIEW" ? $"길드 심사 진행 중\n<color=#F3D071>{FormatTime(row.SecondsRemaining)}</color> 남음" : row.Status == "COMPLETED_PENDING_APPLY" ? "심사 완료\n승급 결과를 적용할 수 있습니다." : $"최근 기록\n{Result(value.LastResult)}";
            bool all = row.Requirements.All(item => item.Met) && row.PersonalGold >= row.PersonalGoldCost && value.KingdomGold >= row.KingdomGoldCost;
            primaryText.text = row.Status == "COMPLETED_PENDING_APPLY" ? "승급 완료" : row.Status == "IN_REVIEW" ? $"심사 중 · {FormatTime(row.SecondsRemaining)}" : row.NextRankId == null ? "전설 랭크 달성" : "심사 시작";
            primary.interactable = row.Status == "COMPLETED_PENDING_APPLY" || row.Status == "READY" && all;
        }

        private static string Grade(string id) => id?.Replace("GRADE_", "") + "등급";
        private static string Rank(string id) => id switch { "RANK_APPRENTICE" => "수습", "RANK_REGULAR" => "정식", "RANK_SKILLED" => "숙련", "RANK_ELITE" => "정예", "RANK_HERO" => "영웅", "RANK_LEGEND" => "전설", _ => id ?? "-" };
        private static string Status(string value) => value switch { "READY" => "심사 준비", "IN_REVIEW" => "심사 중", "COMPLETED_PENDING_APPLY" => "적용 대기", _ => "성장 중" };
        private static string Result(string value) => value switch { "NONE" => "아직 성장 기록이 없습니다.", "P11_PROMOTION_READY" => "승급 조건 달성", "P11_PROMOTION_REVIEW_STARTED" => "길드 심사 접수", "P11_PROMOTION_REVIEW_COMPLETED" => "길드 심사 완료", "P11_PROMOTION_APPLIED" => "승급 적용 완료", "P11_EXPERIENCE_AWARDED" => "사냥 경험치 획득", _ => value };
        private static string FormatTime(long seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
