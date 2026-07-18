using System;
using KingdomTycoon.Application.Facilities.Queries;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Kingdom.Views
{
    public sealed class FacilityWorldView : MonoBehaviour
    {
        [SerializeField] private string facilityId;
        [SerializeField] private Image baseSprite;
        [SerializeField] private Image stateOverlay;
        [SerializeField] private Image stoppedIcon;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button hitTarget;
        [SerializeField] private Sprite lockedSprite;
        [SerializeField] private Sprite constructionSprite;
        [SerializeField] private Sprite stoppedSprite;

        public event Action<string> Selected;
        public string FacilityId => facilityId;
        public string BoundState { get; private set; }

        private void Awake()
        {
            if (hitTarget != null) hitTarget.onClick.AddListener(Select);
        }

        private void OnDestroy()
        {
            if (hitTarget != null) hitTarget.onClick.RemoveListener(Select);
        }

        public void Configure(string id, Image baseImage, Image overlayImage, Image stoppedImage, TMP_Text labelText, Button button, Sprite locked, Sprite construction, Sprite stopped)
        {
            facilityId = id; baseSprite = baseImage; stateOverlay = overlayImage; stoppedIcon = stoppedImage; label = labelText; hitTarget = button;
            lockedSprite = locked; constructionSprite = construction; stoppedSprite = stopped;
        }

        public void Bind(FacilityWorldDto dto)
        {
            if (dto == null || dto.FacilityId != facilityId) throw new ArgumentException("Facility DTO does not match this view.", nameof(dto));
            BoundState = dto.State;
            label.text = dto.Name + "  Lv." + dto.Level + StateSuffix(dto);
            bool locked = dto.State == "LOCKED";
            bool construction = dto.State is "BUILDING" or "UPGRADING";
            stateOverlay.gameObject.SetActive(locked || construction);
            stateOverlay.sprite = locked ? lockedSprite : constructionSprite;
            stoppedIcon.gameObject.SetActive(dto.State == "STOPPED");
            stoppedIcon.sprite = stoppedSprite;
            baseSprite.color = dto.State == "BUILDABLE" ? new Color(1f, 1f, 1f, 0.35f) : dto.State == "STOPPED" ? new Color(0.63f, 0.63f, 0.63f, 1f) : Color.white;
        }

        private static string StateSuffix(FacilityWorldDto dto)
        {
            if (dto.State is "BUILDING" or "UPGRADING") return dto.RemainingSeconds > 0 ? $"  {dto.RemainingSeconds / 60}:{dto.RemainingSeconds % 60:00}" : "  완료";
            if (dto.State == "STOPPED") return "  · NPC 필요";
            if (dto.State == "BUILDABLE") return "  · 건설 가능";
            return string.Empty;
        }

        private void Select() => Selected?.Invoke(facilityId);
    }
}
