using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Application.Raids;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Raids
{
    public sealed class RaidScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup; [SerializeField] private Button closeButton;
        [SerializeField] private Button[] raidButtons; [SerializeField] private TMP_Text[] raidLabels;
        [SerializeField] private Button[] memberButtons; [SerializeField] private TMP_Text[] memberLabels;
        [SerializeField] private Button[] partButtons; [SerializeField] private TMP_Text[] partLabels;
        [SerializeField] private TMP_Text titleText; [SerializeField] private TMP_Text briefingText; [SerializeField] private TMP_Text partyText;
        [SerializeField] private TMP_Text warningText; [SerializeField] private TMP_Text resultText; [SerializeField] private TMP_Text traceText;
        [SerializeField] private Image bossFrame; [SerializeField] private Image bossAura; [SerializeField] private TMP_Text bossGlyph; [SerializeField] private Image bossHpFill; [SerializeField] private Button deployButton; [SerializeField] private TMP_Text deployLabel;
        private RaidOverviewDto overview; private int raidIndex; private int partIndex; private readonly HashSet<string> selected = new(StringComparer.Ordinal);
        public event Action CloseRequested; public event Action<RaidSummaryDto, IReadOnlyList<string>, string, bool> DeployRequested;

        public void Configure(CanvasGroup group, Button close, Button[] raids, TMP_Text[] raidTexts, Button[] members, TMP_Text[] memberTexts,
            Button[] parts, TMP_Text[] partTexts, TMP_Text title, TMP_Text briefing, TMP_Text party, TMP_Text warning, TMP_Text result, TMP_Text trace, Image frame, Image aura, TMP_Text glyph, Image bossFill, Button deploy, TMP_Text deployText)
        { canvasGroup = group; closeButton = close; raidButtons = raids; raidLabels = raidTexts; memberButtons = members; memberLabels = memberTexts; partButtons = parts; partLabels = partTexts; titleText = title; briefingText = briefing; partyText = party; warningText = warning; resultText = result; traceText = trace; bossFrame = frame; bossAura = aura; bossGlyph = glyph; bossHpFill = bossFill; deployButton = deploy; deployLabel = deployText; Bind(); }
        private void OnEnable() => Bind();
        public void Render(RaidOverviewDto value)
        {
            overview = value; raidIndex = Math.Clamp(raidIndex, 0, Math.Max(0, value.Raids.Count - 1));
            for (int i = 0; i < raidButtons.Length; i++) { bool visible = i < value.Raids.Count; raidButtons[i].gameObject.SetActive(visible); if (!visible) continue; RaidSummaryDto raid = value.Raids[i]; raidLabels[i].text = $"{RaidName(raid.Id)}\n<size=17>{Difficulty(raid.Difficulty)}  ·  권장 {raid.RecommendedPower:N0}</size>"; raidButtons[i].GetComponent<Image>().color = i == raidIndex ? Accent(raid.Id) : new Color32(38, 49, 50, 255); }
            RaidSummaryDto current = CurrentRaid(); int minimumRank = current.Id == "RAID_HYDRA" ? 4 : 5;
            for (int i = 0; i < memberButtons.Length; i++) { bool visible = i < value.PartyCandidates.Count; memberButtons[i].gameObject.SetActive(visible); if (!visible) continue; RaidPartyMemberDto member = value.PartyCandidates[i]; bool rankOk = RankOrder(member.RankId) >= minimumRank; bool picked = selected.Contains(member.Id); memberLabels[i].text = $"<b>{member.Name}</b>  {Job(member.JobId)}\n<size=16>{Rank(member.RankId)} · 전투력 {member.Power:N0} · 물약 {member.Potions}</size>"; memberButtons[i].interactable = member.Eligible && rankOk; memberButtons[i].GetComponent<Image>().color = picked ? new Color32(39, 115, 91, 255) : memberButtons[i].interactable ? new Color32(45, 58, 57, 255) : new Color32(49, 42, 42, 210); }
            for (int i = 0; i < partButtons.Length; i++) { bool visible = i < current.Parts.Count; partButtons[i].gameObject.SetActive(visible); if (!visible) continue; RaidPartDto part = current.Parts[i]; partLabels[i].text = $"{Part(part.Id)}\n<size=15>{part.BehaviorChange.Replace('_', ' ')} · 파괴 {part.BreakCount}</size>"; partButtons[i].interactable = part.Available; partButtons[i].GetComponent<Image>().color = i == partIndex ? Accent(current.Id) : new Color32(43, 54, 53, 255); }
            titleText.text = $"{RaidName(current.Id)}  <size=24><color=#9FCFC2>{Difficulty(current.Difficulty)}</color></size>";
            bool hydra = current.Id == "RAID_HYDRA"; bossFrame.color = hydra ? new Color32(31, 48, 37, 255) : new Color32(47, 31, 35, 255); bossAura.color = hydra ? new Color32(70, 102, 54, 190) : new Color32(126, 47, 45, 210); bossGlyph.text = hydra ? "□\n<size=19>VENOM MULTIHEAD</size>" : "△\n<size=19>ASHEN SKY TYRANT</size>"; bossHpFill.color = hydra ? new Color32(98, 162, 76, 255) : new Color32(173, 67, 61, 255);
            briefingText.text = current.Unlocked ? $"제한 시간 {current.TimeLimitSeconds / 60}:{current.TimeLimitSeconds % 60:00}  ·  편성 {current.PartyMin}~{current.PartyMax}명  ·  최고 기록 {(current.BestClearTimeMs.HasValue ? $"{current.BestClearTimeMs.Value / 1000f:0.0}초" : "없음")}" : "잠금 상태 · 왕국/지역 진행 및 이전 난이도 클리어 조건이 필요합니다.";
            string[] jobs = value.PartyCandidates.Where(member => selected.Contains(member.Id)).Select(member => member.JobId).ToArray(); var warnings = new List<string>(); if (!jobs.Contains("JOB_GUARDIAN")) warnings.Add("탱커 없음"); if (!jobs.Contains("JOB_CLERIC")) warnings.Add("힐러 없음"); if (!jobs.Any(job => job is "JOB_ARCHER" or "JOB_MAGE")) warnings.Add("원거리 화력 없음"); warningText.text = warnings.Count == 0 ? "<color=#75D8B8>역할 균형 양호</color>" : "<color=#F1B36A>주의 · " + string.Join(" · ", warnings) + "</color>";
            partyText.text = $"원정대 {selected.Count} / {current.PartyMax}"; deployButton.interactable = current.Unlocked && selected.Count >= current.PartyMin && selected.Count <= current.PartyMax; deployLabel.text = current.Unlocked ? deployButton.interactable ? "경고 확인 후 레이드 출정" : $"최소 {current.PartyMin}명을 편성하세요" : "해금 조건을 충족하세요"; bossHpFill.rectTransform.anchorMax = new Vector2(1, 1);
        }
        public void RenderResult(RaidOperationResult value) { resultText.text = value.Success ? $"<color=#F4D27A><b>토벌 성공</b></color>  {value.DurationMs / 1000f:0.0}초\n파괴 부위: {string.Join(", ", value.BrokenPartIds.Select(Part))}\n보상: {(value.RewardLines.Count == 0 ? "없음" : string.Join(" · ", value.RewardLines))}" : $"<color=#E17C72><b>토벌 실패</b></color>  {value.DurationMs / 1000f:0.0}초\n부상: {value.InjuredMercenaryInstanceIds.Count}명 · 보상 없음"; traceText.text = value.TraceLines.Count == 0 ? "전투 추적 정보 없음" : string.Join("\n", value.TraceLines.TakeLast(8)); bossHpFill.rectTransform.anchorMax = new Vector2(value.Success ? 0 : .34f, 1); }
        public void ShowError(string code) { resultText.text = $"<color=#E17C72><b>출정 불가</b></color>\n{code}"; }
        public void SetVisible(bool visible) { gameObject.SetActive(visible); if (canvasGroup != null) { canvasGroup.alpha = visible ? 1 : 0; canvasGroup.interactable = visible; canvasGroup.blocksRaycasts = visible; } }
        private void Bind()
        {
            if (closeButton == null) return; closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            for (int i = 0; i < raidButtons.Length; i++) { int index = i; raidButtons[i].onClick.RemoveAllListeners(); raidButtons[i].onClick.AddListener(() => { raidIndex = index; partIndex = 0; selected.Clear(); Render(overview); }); }
            for (int i = 0; i < memberButtons.Length; i++) { int index = i; memberButtons[i].onClick.RemoveAllListeners(); memberButtons[i].onClick.AddListener(() => { if (index >= overview.PartyCandidates.Count) return; string id = overview.PartyCandidates[index].Id; if (!selected.Remove(id)) selected.Add(id); Render(overview); }); }
            for (int i = 0; i < partButtons.Length; i++) { int index = i; partButtons[i].onClick.RemoveAllListeners(); partButtons[i].onClick.AddListener(() => { partIndex = index; Render(overview); }); }
            deployButton.onClick.RemoveAllListeners(); deployButton.onClick.AddListener(() => { RaidSummaryDto raid = CurrentRaid(); DeployRequested?.Invoke(raid, selected.OrderBy(value => value, StringComparer.Ordinal).ToArray(), raid.Parts[Math.Clamp(partIndex, 0, raid.Parts.Count - 1)].Id, true); });
        }
        private RaidSummaryDto CurrentRaid() => overview.Raids[raidIndex];
        private static Color Accent(string id) => id == "RAID_HYDRA" ? new Color32(92, 130, 66, 255) : new Color32(137, 60, 55, 255);
        private static int RankOrder(string id) => id switch { "RANK_APPRENTICE" => 1, "RANK_REGULAR" => 2, "RANK_SKILLED" => 3, "RANK_ELITE" => 4, "RANK_HERO" => 5, "RANK_LEGEND" => 6, _ => 0 };
        private static string Rank(string id) => id?.Replace("RANK_", string.Empty) ?? "-";
        private static string Difficulty(string id) => id switch { "NORMAL" => "일반", "HARD" => "고난", "CORRUPTED" => "타락", _ => id };
        private static string RaidName(string id) => id == "RAID_HYDRA" ? "늪의 히드라" : "재의 고룡";
        private static string Job(string id) => id switch { "JOB_WARRIOR" => "전사", "JOB_GUARDIAN" => "수호자", "JOB_ARCHER" => "궁수", "JOB_MAGE" => "마법사", "JOB_CLERIC" => "성직자", _ => id };
        private static string Part(string id) => id?.Replace("HYDRA_", string.Empty).Replace("DRAGON_", string.Empty) switch { "HEAD" => "머리", "BODY" => "몸통", "HEART" => "심장", "HORN" => "뿔", "WING" => "날개", var value => value };
    }
}
