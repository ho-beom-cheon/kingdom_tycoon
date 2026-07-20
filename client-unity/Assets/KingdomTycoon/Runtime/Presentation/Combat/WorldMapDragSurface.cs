using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomTycoon.Presentation.Combat
{
    public sealed class WorldMapDragSurface : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        private Canvas canvas;

        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public bool IsDragging { get; private set; }

        public void Configure(RectTransform viewportRect, RectTransform contentRect)
        {
            viewport = viewportRect; content = contentRect; canvas = GetComponentInParent<Canvas>(); ClampToBounds();
        }

        public void OnBeginDrag(PointerEventData eventData) => IsDragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            float scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            PanBy(eventData.delta.x / scale);
        }

        public void OnEndDrag(PointerEventData eventData) => IsDragging = false;

        public void PanBy(float deltaX)
        {
            if (content == null) return;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x + deltaX, 0f); ClampToBounds();
        }

        public void PanToWorldX(float worldX)
        {
            if (content == null || viewport == null) return;
            content.anchoredPosition = new Vector2(viewport.rect.width * .35f - worldX, 0f); ClampToBounds();
        }

        public void ClampToBounds()
        {
            if (content == null || viewport == null) return;
            float minimumX = Mathf.Min(0f, viewport.rect.width - content.rect.width);
            content.anchoredPosition = new Vector2(Mathf.Clamp(content.anchoredPosition.x, minimumX, 0f), 0f);
        }

        private void OnRectTransformDimensionsChange() => ClampToBounds();
    }
}
