using System;
using System.Linq;
using KingdomTycoon.Application.Facilities.Queries;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Kingdom.Views
{
    public sealed class FacilityDrawerView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private TMP_Text effectText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private TMP_Text actionLabel;
        [SerializeField] private Button primaryAction;
        [SerializeField] private Button closeButton;

        public event Action PrimaryActionRequested;
        public event Action CloseRequested;
        public FacilityDetailDto Current { get; private set; }

        private void Awake()
        {
            primaryAction.onClick.AddListener(() => PrimaryActionRequested?.Invoke());
            closeButton.onClick.AddListener(() => CloseRequested?.Invoke());
        }

        public void Configure(TMP_Text titleText, TMP_Text state, TMP_Text effect, TMP_Text cost, TMP_Text error, TMP_Text action, Button primary, Button close)
        {
            title = titleText; stateText = state; effectText = effect; costText = cost; errorText = error; actionLabel = action; primaryAction = primary; closeButton = close;
        }

        public void Open(FacilityDetailDto dto)
        {
            gameObject.SetActive(true);
            Bind(dto);
        }

        public void Bind(FacilityDetailDto dto)
        {
            Current = dto ?? throw new ArgumentNullException(nameof(dto));
            title.text = dto.World.Name + "  레벨 " + dto.World.Level;
            stateText.text = StateText(dto.World.State) + (dto.World.StopReason == "NONE" ? string.Empty : " · " + StopReasonText(dto.World.StopReason));
            effectText.text = dto.EffectText;
            costText.text = "왕국 골드 " + dto.CostGold + (dto.CostItems.Count == 0 ? string.Empty : "\n" + string.Join("\n", dto.CostItems.Select(item => $"{item.Name} {item.Owned}/{item.Required}")));
            actionLabel.text = ActionText(dto.PrimaryAction);
            primaryAction.interactable = dto.DisabledReason == null;
            errorText.text = DisabledReasonText(dto.DisabledReason);
        }

        public void ShowError(string errorCode) => errorText.text = DisabledReasonText(errorCode);
        public void Close() { Current = null; gameObject.SetActive(false); }

        private static string ActionText(string action) => action switch
        {
            "BUILD" => "건설", "UPGRADE" => "강화", "CLAIM" => "완료 받기", "ASSIGN" => "담당자 배치", "UNASSIGN" => "담당자 해제", "WAIT" => "건설 중", "MAX" => "최대 레벨", _ => "잠김"
        };

        private static string StateText(string state) => state switch
        {
            "LOCKED" => "잠김",
            "BUILDABLE" => "건설 가능",
            "BUILDING" => "건설 중",
            "ACTIVE" => "운영 중",
            "STOPPED" => "운영 중단",
            "UPGRADING" => "강화 중",
            _ => "상태 확인 중"
        };

        private static string StopReasonText(string reason) => reason switch
        {
            "NPC_REQUIRED" => "담당자 필요",
            "PREREQUISITE_REQUIRED" => "선행 시설 필요",
            _ => "운영 조건을 확인하세요"
        };

        private static string DisabledReasonText(string reason) => reason switch
        {
            null or "" => string.Empty,
            "FACILITY_NPC_REQUIRED" => "배치할 담당자가 필요합니다.",
            "FACILITY_GOLD_INSUFFICIENT" => "왕국 골드가 부족합니다.",
            "FACILITY_ITEM_INSUFFICIENT" => "필요한 재료가 부족합니다.",
            "FACILITY_PREREQUISITE_REQUIRED" => "선행 시설 조건을 충족해야 합니다.",
            _ => "현재 조건에서는 진행할 수 없습니다."
        };
    }
}
