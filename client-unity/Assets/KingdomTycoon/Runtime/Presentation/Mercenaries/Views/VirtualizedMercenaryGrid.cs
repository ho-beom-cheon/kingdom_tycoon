using System;
using System.Collections.Generic;
using KingdomTycoon.Application.Mercenaries;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Mercenaries.Views
{
    public sealed class VirtualizedMercenaryGrid : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private MercenaryCardView cardPrefab;
        [SerializeField] private int columnsClosed = 4;
        [SerializeField] private int columnsOpen = 3;
        [SerializeField] private float gap = 24f;
        [SerializeField] private float cardHeight = 220f;
        private readonly List<MercenaryCardView> pool = new();
        private IReadOnlyList<MercenaryCardDto> items = Array.Empty<MercenaryCardDto>();
        private Func<string, Sprite> portraits;
        private bool drawerOpen;
        private int firstVisibleRow = -1;
        private float lastViewportWidth = -1f;

        public event Action<string> Selected;
        public int PoolCount => pool.Count;
        public int BoundCount { get; private set; }
        public IReadOnlyList<MercenaryCardView> Pool => pool;

        public void Configure(ScrollRect scroll, RectTransform viewportRoot, RectTransform contentRoot, MercenaryCardView prefab)
        { scrollRect = scroll; viewport = viewportRoot; content = contentRoot; cardPrefab = prefab; }

        private void Awake() => scrollRect.onValueChanged.AddListener(OnScroll);
        private void OnDestroy()
        {
            scrollRect.onValueChanged.RemoveListener(OnScroll);
            foreach (MercenaryCardView card in pool) if (card != null) card.Selected -= Select;
        }
        private void OnScroll(Vector2 _) => RebindVisible();

        private void LateUpdate()
        {
            if (viewport == null || Mathf.Approximately(lastViewportWidth, viewport.rect.width)) return;
            firstVisibleRow = -1;
            RebindVisible();
        }

        public void SetDrawerOpen(bool value)
        {
            if (drawerOpen == value) return;
            drawerOpen = value; firstVisibleRow = -1; RebuildLayout(); RebindVisible();
        }

        public void SetItems(IReadOnlyList<MercenaryCardDto> values, Func<string, Sprite> portraitResolver)
        {
            items = values ?? Array.Empty<MercenaryCardDto>(); portraits = portraitResolver; EnsurePool(); firstVisibleRow = -1; RebuildLayout(); RebindVisible();
        }

        public void ScrollTo(string instanceId)
        {
            int index = -1;
            for (int i = 0; i < items.Count; i++) if (items[i].InstanceId == instanceId) { index = i; break; }
            if (index < 0) return;
            int columns = drawerOpen ? columnsOpen : columnsClosed;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, (index / columns) * (cardHeight + gap));
            RebindVisible();
        }

        public void Clear() => SetItems(Array.Empty<MercenaryCardDto>(), null);

        public void SetLargeText(bool value)
        {
            cardHeight = value ? 280f : 220f;
            firstVisibleRow = -1;
            RebuildLayout();
            RebindVisible();
        }

        private void EnsurePool()
        {
            int required = Mathf.Min(16, items.Count);
            while (pool.Count < required)
            {
                MercenaryCardView card = Instantiate(cardPrefab, content);
                card.name = "PooledCard_" + pool.Count;
                card.Selected += Select;
                pool.Add(card);
            }
        }

        private void Select(string id) => Selected?.Invoke(id);
        private void RebuildLayout()
        {
            int columns = drawerOpen ? columnsOpen : columnsClosed;
            int rows = Mathf.CeilToInt(items.Count / (float)columns);
            float height = rows == 0 ? cardHeight : rows * cardHeight + Mathf.Max(0, rows - 1) * gap;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        private void RebindVisible()
        {
            if (content == null || viewport == null) return;
            int columns = drawerOpen ? columnsOpen : columnsClosed;
            lastViewportWidth = viewport.rect.width;
            int row = Mathf.Max(0, Mathf.FloorToInt(content.anchoredPosition.y / (cardHeight + gap)));
            if (row == firstVisibleRow && BoundCount > 0) return;
            firstVisibleRow = row;
            int start = row * columns;
            float width = Mathf.Floor((Mathf.Max(1f, viewport.rect.width) - gap * (columns - 1)) / columns);
            int bound = 0;
            for (int poolIndex = 0; poolIndex < pool.Count; poolIndex++)
            {
                int itemIndex = start + poolIndex;
                MercenaryCardView card = pool[poolIndex];
                if (itemIndex >= items.Count) { card.ResetView(); continue; }
                int visualRow = itemIndex / columns;
                int visualColumn = itemIndex % columns;
                RectTransform rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.sizeDelta = new Vector2(width, cardHeight);
                rect.anchoredPosition = new Vector2(visualColumn * (width + gap), -visualRow * (cardHeight + gap));
                card.gameObject.SetActive(true);
                card.Bind(items[itemIndex], portraits?.Invoke(items[itemIndex].JobId));
                bound++;
            }
            BoundCount = bound;
        }
    }
}
