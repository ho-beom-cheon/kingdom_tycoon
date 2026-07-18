using System;
using KingdomTycoon.Application.Mercenaries;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Mercenaries.Views
{
    public sealed class MercenaryCardView : MonoBehaviour
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text identityText;
        [SerializeField] private TMP_Text stateText;
        private string instanceId;

        public event Action<string> Selected;
        public string BoundInstanceId => instanceId;

        public void Configure(Button button, Image image, TMP_Text name, TMP_Text identity, TMP_Text state)
        {
            selectButton = button; portrait = image; nameText = name; identityText = identity; stateText = state;
        }

        private void Awake() => selectButton.onClick.AddListener(Select);
        private void OnDestroy() => selectButton.onClick.RemoveListener(Select);
        private void Select() { if (instanceId != null) Selected?.Invoke(instanceId); }

        public void Bind(MercenaryCardDto dto, Sprite sprite)
        {
            instanceId = dto.InstanceId;
            portrait.sprite = sprite;
            portrait.enabled = sprite != null;
            nameText.text = dto.DisplayName;
            identityText.text = $"{dto.JobName} · {dto.GradeName} · {dto.RankName} Lv.{dto.Level}";
            stateText.text = (dto.Active ? "활동" : "대기") + " · " + dto.AutonomyState
                + (dto.Injured ? " · 부상" : dto.PromotionReady ? " · 승급 가능" : string.Empty);
        }

        public void ResetView()
        {
            instanceId = null;
            portrait.sprite = null;
            nameText.text = identityText.text = stateText.text = string.Empty;
            gameObject.SetActive(false);
        }
    }
}
