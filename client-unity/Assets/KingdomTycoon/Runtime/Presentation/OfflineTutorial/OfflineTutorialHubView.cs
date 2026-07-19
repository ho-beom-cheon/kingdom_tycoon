using System;
using System.Linq;
using KingdomTycoon.Application.OfflineTutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.OfflineTutorial
{
    public sealed class OfflineTutorialHubView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button advanceButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button skipAllButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text journeyText;
        [SerializeField] private TMP_Text actionText;

        public event Action CloseRequested;
        public event Action AdvanceRequested;
        public event Action SkipRequested;
        public event Action SkipAllRequested;

        public void Configure(CanvasGroup group, Button close, Button advance, Button skip, Button skipAll,
            TMP_Text status, TMP_Text reward, TMP_Text journey, TMP_Text action)
        {
            canvasGroup = group; closeButton = close; advanceButton = advance; skipButton = skip; skipAllButton = skipAll;
            statusText = status; rewardText = reward; journeyText = journey; actionText = action; Bind();
        }

        private void OnEnable() => Bind();

        public void Render(OfflineTutorialOverviewDto value)
        {
            long hours = value.EligibleSeconds / 3600;
            long minutes = value.EligibleSeconds % 3600 / 60;
            statusText.text = value.Status switch
            {
                "APPLIED" => $"<color=#79B8AA>정산 완료</color>  ·  {hours}시간 {minutes}분 적용",
                "CAPPED" => $"<color=#E0B768>8시간 한도 적용</color>  ·  {hours}시간 {minutes}분",
                "CLOCK_ROLLBACK" => "<color=#E98274>기기 시간 변경 감지</color>  ·  보상 미적용",
                _ => "<color=#91A39F>짧은 자리 비움</color>  ·  정산 기준 1분"
            };
            rewardText.text = value.Lines.Count == 0
                ? "이번 정산 보상은 없습니다. 사냥과 시설 운영을 시작해 보세요."
                : string.Join("\n", value.Lines.Select(Line));
            journeyText.text = $"왕국 여정  {value.CompletedCount} / {value.TotalCount}\n" +
                string.Join("\n", value.Steps.Select(step => $"{(step.Completed ? "✓" : step.Current ? "◆" : "·")}  {step.Order:00}  {StepName(step.ActionType)}"));
            TutorialStepDto current = value.CurrentStep;
            actionText.text = value.TutorialCompleted
                ? "<color=#79B8AA><b>왕국 운영 준비 완료</b></color>\n자동 사냥·시설·성장·레이드까지 자유롭게 운영할 수 있습니다."
                : $"<b>다음 목표</b>  {StepName(current?.ActionType)}\n대상: {current?.TargetId ?? "-"}";
            advanceButton.interactable = !value.TutorialCompleted;
            skipButton.interactable = current?.Skippable == true;
            skipAllButton.interactable = !value.TutorialCompleted;
        }

        public void ShowResult(string resultCode) => actionText.text = $"<color=#79B8AA><b>진행 반영 완료</b></color>\n{resultCode}";
        public void ShowError(string code) => actionText.text = $"<color=#E98274><b>처리할 수 없습니다</b></color>\n{code}";
        public void SetVisible(bool visible) { gameObject.SetActive(visible); if (canvasGroup != null) { canvasGroup.alpha = visible ? 1 : 0; canvasGroup.interactable = visible; canvasGroup.blocksRaycasts = visible; } }

        private void Bind()
        {
            if (closeButton == null) return;
            closeButton.onClick.RemoveAllListeners(); closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
            advanceButton.onClick.RemoveAllListeners(); advanceButton.onClick.AddListener(() => AdvanceRequested?.Invoke());
            skipButton.onClick.RemoveAllListeners(); skipButton.onClick.AddListener(() => SkipRequested?.Invoke());
            skipAllButton.onClick.RemoveAllListeners(); skipAllButton.onClick.AddListener(() => SkipAllRequested?.Invoke());
        }

        private static string Line(OfflineLineDto value) => value.Type switch
        {
            "HUNT" => $"자동 사냥 수익                         +{value.Quantity:N0}",
            "POTION_CONSUMPTION" => $"회복 물약 사용                           -{value.Quantity:N0}",
            "FACILITY" => $"시설 생산 진행                         +{value.Quantity:N0} tick",
            "NPC_PROFICIENCY" => $"관리인 숙련도                           +{value.Quantity:N0}",
            "INJURY_RECOVERY" => $"부상 회복                                 {value.Quantity:N0}명",
            "PROMOTION_REVIEW" => $"승급 심사 완료                           {value.Quantity:N0}명",
            _ => $"{value.Type}  {value.Quantity:N0}"
        };

        private static string StepName(string action) => action switch
        {
            "VIEW_KINGDOM" => "왕국 둘러보기", "RECRUIT_FROM_POOL" => "첫 용병 고용", "SET_REGION_PERMISSION" => "사냥 지역 허가",
            "OBSERVE_AUTONOMY_HUNT" => "자동 사냥 관찰", "RETURN_AND_SELL" => "귀환 후 전리품 판매",
            "BUILD_FACILITY_AND_CRAFT_RECIPE" => "시설 건설 및 제작", "WAIT_FOR_MERCENARY_BUY_AND_EQUIP" => "용병 구매와 장비 장착",
            "COMPLETE_PROMOTION" => "첫 승급 완료", "UNLOCK_REGION" => "다음 지역 해금", _ => action ?? "완료"
        };
    }
}
