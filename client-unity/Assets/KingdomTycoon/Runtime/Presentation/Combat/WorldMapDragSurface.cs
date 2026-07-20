using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomTycoon.Presentation.Combat
{
    public sealed class WorldMapDragSurface : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public const float MinimumZoom = .65f;
        public const float MaximumZoom = 1.35f;
        public const float TapThreshold = 18f;

        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        private Canvas canvas;
        private Vector2 dragDistance;
        private Vector2 velocity;
        private float zoom = 1f;
        private bool suppressNextTap;
        private float previousPinchDistance;

        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public bool IsDragging { get; private set; }
        public float Zoom => zoom;
        public bool TapSuppressed => suppressNextTap;

        public void Configure(RectTransform viewportRect, RectTransform contentRect)
        {
            viewport = viewportRect; content = contentRect; canvas = GetComponentInParent<Canvas>();
            zoom = Mathf.Clamp(content == null ? 1f : content.localScale.x, MinimumZoom, MaximumZoom);
            ApplyZoom();
            ClampToBounds();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            IsDragging = true;
            dragDistance = Vector2.zero;
            velocity = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            float scale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            Vector2 delta = eventData.delta / scale;
            dragDistance += new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            velocity = delta / Mathf.Max(Time.unscaledDeltaTime, .001f);
            PanBy(delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            IsDragging = false;
            if (dragDistance.magnitude > TapThreshold) suppressNextTap = true;
        }

        public void OnScroll(PointerEventData eventData) => ZoomBy(eventData.scrollDelta.y * .08f);

        public bool ConsumeTapSuppression()
        {
            bool value = suppressNextTap;
            suppressNextTap = false;
            return value;
        }

        public void PanBy(float deltaX) => PanBy(new Vector2(deltaX, 0f));

        public void PanBy(Vector2 delta)
        {
            if (content == null) return;
            content.anchoredPosition += delta;
            ClampToBounds();
        }

        public void PanToWorldX(float worldX) => PanToWorld(new Vector2(worldX, 0f));

        public void PanToWorld(Vector2 worldPosition)
        {
            if (content == null || viewport == null) return;
            content.anchoredPosition = -worldPosition * zoom;
            ClampToBounds();
        }

        public void ResetView()
        {
            zoom = 1f;
            velocity = Vector2.zero;
            ApplyZoom();
            PanToWorld(MobileLivingWorldLayout.KingdomCenter);
        }

        public void ZoomBy(float delta)
        {
            zoom = Mathf.Clamp(zoom + delta, MinimumZoom, MaximumZoom);
            ApplyZoom();
            ClampToBounds();
        }

        public void ClampToBounds()
        {
            if (content == null || viewport == null) return;
            float maximumX = Mathf.Max(0f, (content.rect.width * zoom - viewport.rect.width) * .5f);
            float maximumY = Mathf.Max(0f, (content.rect.height * zoom - viewport.rect.height) * .5f);
            content.anchoredPosition = new Vector2(
                Mathf.Clamp(content.anchoredPosition.x, -maximumX, maximumX),
                Mathf.Clamp(content.anchoredPosition.y, -maximumY, maximumY));
        }

        private void Update()
        {
            HandlePinch();
            if (IsDragging || velocity.sqrMagnitude < 4f) return;
            velocity *= Mathf.Pow(.055f, Time.unscaledDeltaTime);
            PanBy(velocity * Time.unscaledDeltaTime);
        }

        private void HandlePinch()
        {
            if (Input.touchCount != 2) { previousPinchDistance = 0f; return; }
            Touch first = Input.GetTouch(0);
            Touch second = Input.GetTouch(1);
            float distance = Vector2.Distance(first.position, second.position);
            if (previousPinchDistance > 0f) ZoomBy((distance - previousPinchDistance) / 700f);
            previousPinchDistance = distance;
        }

        private void ApplyZoom()
        {
            if (content != null) content.localScale = new Vector3(zoom, zoom, 1f);
        }

        private void OnRectTransformDimensionsChange() => ClampToBounds();
    }
}
