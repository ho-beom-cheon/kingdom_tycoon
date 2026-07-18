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
            title.text = dto.World.Name + "  Lv." + dto.World.Level;
            stateText.text = dto.World.State + (dto.World.StopReason == "NONE" ? string.Empty : " · " + dto.World.StopReason);
            effectText.text = dto.EffectText;
            costText.text = "왕국 골드 " + dto.CostGold + (dto.CostItems.Count == 0 ? string.Empty : "\n" + string.Join("\n", dto.CostItems.Select(item => $"{item.Name} {item.Owned}/{item.Required}")));
            actionLabel.text = ActionText(dto.PrimaryAction);
            primaryAction.interactable = dto.DisabledReason == null;
            errorText.text = dto.DisabledReason ?? string.Empty;
        }

        public void ShowError(string errorCode) => errorText.text = errorCode ?? string.Empty;
        public void Close() { Current = null; gameObject.SetActive(false); }

        private static string ActionText(string action) => action switch
        {
            "BUILD" => "건설", "UPGRADE" => "업그레이드", "CLAIM" => "완료 받기", "ASSIGN" => "NPC 배치", "UNASSIGN" => "NPC 해제", "WAIT" => "건설 중", "MAX" => "최대 레벨", _ => "잠김"
        };
    }
}
