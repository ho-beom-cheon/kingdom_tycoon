using UnityEngine;
using UnityEngine.EventSystems;

namespace KingdomTycoon.Presentation.Combat
{
    public sealed class WorldMapDragSurface : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        public const float MinimumZoom = .65f;
        public const float MaximumZoom = 1.35f;
        public const float StartingZoom = .92f;
        public const float TapThreshold = 18f;
        public const float DirectManipulationGain = 1.12f;
        private const float MaximumInertialSpeed = 2600f;

        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        private Canvas canvas;
        private Vector2 dragDistance;
        private Vector2 pendingDragDelta;
        private Vector2 velocity;
        private Vector2 cachedMaximumOffset;
        private float zoom = 1f;
        private float canvasScale = 1f;
        private bool suppressNextTap;
        private float previousPinchDistance;

        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public bool IsDragging { get; private set; }
        public bool IsCameraMoving => IsDragging || pendingDragDelta.sqrMagnitude > .01f || velocity.sqrMagnitude >= 4f;
        public float Zoom => zoom;
        public bool TapSuppressed => suppressNextTap;

        public void Configure(RectTransform viewportRect, RectTransform contentRect)
        {
            viewport = viewportRect; content = contentRect; canvas = GetComponentInParent<Canvas>();
            zoom = Mathf.Clamp(content == null ? 1f : content.localScale.x, MinimumZoom, MaximumZoom);
            ApplyZoom();
            RefreshGeometryCache();
            ClampToBounds();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RefreshGeometryCache();
            IsDragging = true;
            dragDistance = Vector2.zero;
            pendingDragDelta = Vector2.zero;
            velocity = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 delta = eventData.delta / canvasScale * DirectManipulationGain;
            dragDistance += new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            pendingDragDelta += delta;
            velocity = Vector2.ClampMagnitude(delta / Mathf.Max(Time.unscaledDeltaTime, .001f), MaximumInertialSpeed);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ApplyPendingDrag();
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
            SetContentPosition(content.anchoredPosition + delta);
        }

        public void PanToWorldX(float worldX) => PanToWorld(new Vector2(worldX, 0f));

        public void PanToWorld(Vector2 worldPosition)
        {
            if (content == null || viewport == null) return;
            SetContentPosition(-worldPosition * zoom);
        }

        public void ResetView()
        {
            ResetView(StartingZoom);
        }

        public void ResetView(float targetZoom)
        {
            zoom = Mathf.Clamp(targetZoom, MinimumZoom, MaximumZoom);
            pendingDragDelta = Vector2.zero;
            velocity = Vector2.zero;
            ApplyZoom();
            RefreshGeometryCache();
            PanToWorld(MobileLivingWorldLayout.KingdomCenter);
        }

        public void ZoomBy(float delta)
        {
            ApplyPendingDrag();
            zoom = Mathf.Clamp(zoom + delta, MinimumZoom, MaximumZoom);
            ApplyZoom();
            RefreshGeometryCache();
            ClampToBounds();
        }

        public void ClampToBounds()
        {
            if (content == null || viewport == null) return;
            SetContentPosition(content.anchoredPosition);
        }

        private void Update()
        {
            HandlePinch();
            if (IsDragging || velocity.sqrMagnitude < 4f) return;
            velocity *= Mathf.Pow(.055f, Time.unscaledDeltaTime);
            PanBy(velocity * Time.unscaledDeltaTime);
        }

        private void LateUpdate() => ApplyPendingDrag();

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

        private void ApplyPendingDrag()
        {
            if (content == null || pendingDragDelta.sqrMagnitude <= .0001f) return;
            Vector2 delta = pendingDragDelta;
            pendingDragDelta = Vector2.zero;
            SetContentPosition(content.anchoredPosition + delta);
        }

        private void SetContentPosition(Vector2 requested)
        {
            Vector2 clamped = new(
                Mathf.Clamp(requested.x, -cachedMaximumOffset.x, cachedMaximumOffset.x),
                Mathf.Clamp(requested.y, -cachedMaximumOffset.y, cachedMaximumOffset.y));
            if ((content.anchoredPosition - clamped).sqrMagnitude > .0001f)
                content.anchoredPosition = clamped;
        }

        private void RefreshGeometryCache()
        {
            canvasScale = canvas == null || canvas.scaleFactor <= 0f ? 1f : canvas.scaleFactor;
            if (content == null || viewport == null) { cachedMaximumOffset = Vector2.zero; return; }
            cachedMaximumOffset = new Vector2(
                Mathf.Max(0f, (content.rect.width * zoom - viewport.rect.width) * .5f),
                Mathf.Max(0f, (content.rect.height * zoom - viewport.rect.height) * .5f));
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshGeometryCache();
            ClampToBounds();
        }
    }
}
