using System;
using System.Collections.Generic;
using System.Text;
using KingdomTycoon.Application.Mercenaries;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Mercenaries.Views
{
    public sealed class MercenaryRosterView : MonoBehaviour
    {
        [SerializeField] private TMP_Text ownedCount;
        [SerializeField] private TMP_Text activeCount;
        [SerializeField] private TMP_InputField search;
        [SerializeField] private Button filterButton;
        [SerializeField] private Button sortButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text stateMessage;
        [SerializeField] private GameObject statePanel;
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private VirtualizedMercenaryGrid grid;
        [SerializeField] private TMP_Text noResults;
        [SerializeField] private TMP_Text filterSummary;
        [SerializeField] private TMP_Text offlineBadge;
        [SerializeField] private Button retryButton;
        private RectTransform rootRect;
        private Vector2 baseOffsetMax;

        public event Action<string> SearchChanged;
        public event Action FilterRequested;
        public event Action SortRequested;
        public event Action CloseRequested;
        public event Action RetryRequested;
        public event Action<string> CardSelected;
        public MercenaryRosterUiState CurrentState { get; private set; }
        public int BoundCardCount => grid?.BoundCount ?? 0;
        public int PoolCount => grid?.PoolCount ?? 0;
        public string OwnedCountText => ownedCount?.text;
        public string ActiveCountText => activeCount?.text;
        public string FilterSummaryText => filterSummary?.text;
        public string StateMessageText => stateMessage?.text;
        public TMP_InputField SearchInput => search;
        public Button FilterButton => filterButton;
        public Button SortButton => sortButton;

        public void Configure(TMP_Text owned, TMP_Text active, TMP_InputField searchInput, Button filter, Button sort, Button close,
            TMP_Text message, GameObject states, GameObject content, VirtualizedMercenaryGrid virtualGrid, TMP_Text noResult,
            TMP_Text filters, TMP_Text offline, Button retry)
        {
            ownedCount = owned; activeCount = active; search = searchInput; filterButton = filter; sortButton = sort; closeButton = close;
            stateMessage = message; statePanel = states; contentPanel = content; grid = virtualGrid; noResults = noResult;
            filterSummary = filters; offlineBadge = offline; retryButton = retry;
        }

        private void Awake()
        {
            rootRect = (RectTransform)transform;
            baseOffsetMax = rootRect.offsetMax;
            search.onValueChanged.AddListener(Search);
            filterButton.onClick.AddListener(Filter);
            sortButton.onClick.AddListener(Sort);
            closeButton.onClick.AddListener(Close);
            retryButton.onClick.AddListener(Retry);
            grid.Selected += SelectCard;
        }

        private void OnDestroy()
        {
            search.onValueChanged.RemoveListener(Search);
            filterButton.onClick.RemoveListener(Filter);
            sortButton.onClick.RemoveListener(Sort);
            closeButton.onClick.RemoveListener(Close);
            retryButton.onClick.RemoveListener(Retry);
            grid.Selected -= SelectCard;
        }

        private void Search(string value)
        {
            string clamped = ClampScalars(value, 20);
            if (!string.Equals(value, clamped, StringComparison.Ordinal)) search.SetTextWithoutNotify(clamped);
            SearchChanged?.Invoke(clamped);
        }
        private void Filter() => FilterRequested?.Invoke();
        private void Sort() => SortRequested?.Invoke();
        private void Close() => CloseRequested?.Invoke();
        private void Retry() => RetryRequested?.Invoke();
        private void SelectCard(string id) => CardSelected?.Invoke(id);

        public void BindState(MercenaryRosterUiState state, string message)
        {
            CurrentState = state;
            bool showContent = state is MercenaryRosterUiState.CONTENT or MercenaryRosterUiState.OFFLINE;
            contentPanel.SetActive(showContent);
            statePanel.SetActive(!showContent);
            stateMessage.text = message ?? string.Empty;
            offlineBadge.gameObject.SetActive(state == MercenaryRosterUiState.OFFLINE);
            retryButton.gameObject.SetActive(state == MercenaryRosterUiState.ERROR);
            Transform skeletons = statePanel.transform.Find("LoadingSkeletons");
            if (skeletons != null) skeletons.gameObject.SetActive(state == MercenaryRosterUiState.LOADING);
        }

        public void BindRoster(MercenaryRosterResultDto dto, Func<string, Sprite> portraitResolver)
        {
            ownedCount.text = $"보유 {dto.TotalOwned}/{dto.OwnedLimit}";
            activeCount.text = $"활동 {dto.TotalActive}/{dto.ActiveLimit}";
            noResults.gameObject.SetActive(dto.TotalOwned > 0 && dto.FilteredCount == 0);
            grid.SetItems(dto.Cards, portraitResolver);
        }

        public void BindFilterSummary(MercenaryRosterQueryDto query)
        {
            query ??= new MercenaryRosterQueryDto();
            var values = new List<string>();
            if (query.JobIds.Count > 0) values.Add($"직업 {query.JobIds.Count}");
            if (query.GradeIds.Count > 0) values.Add($"등급 {query.GradeIds.Count}");
            if (query.RankIds.Count > 0) values.Add($"랭크 {query.RankIds.Count}");
            if (query.StateIds.Count > 0) values.Add($"상태 {query.StateIds.Count}");
            if (query.ActiveFilter != "ALL") values.Add(query.ActiveFilter == "ACTIVE" ? "활동" : "대기");
            if (query.PromotionReadyOnly) values.Add("승급 가능");
            if (query.InjuredOnly) values.Add("부상");
            filterSummary.text = values.Count == 0 ? "필터 없음" : string.Join(" · ", values);
        }

        public void SetDrawerOpen(bool value)
        {
            rootRect.offsetMax = new Vector2(baseOffsetMax.x + (value ? -720f : 0f), baseOffsetMax.y);
            Canvas.ForceUpdateCanvases();
            grid.SetDrawerOpen(value);
        }

        public void ClearRoster() => grid.Clear();
        public void ScrollTo(string instanceId) => grid.ScrollTo(instanceId);
        public IReadOnlyList<MercenaryCardView> PooledCards => grid.Pool;
        public void SetLargeTextForFixture(bool value)
        {
            grid.SetLargeText(value);
        }
        public void SetSearchWithoutNotify(string value) => search.SetTextWithoutNotify(value ?? string.Empty);

        private static string ClampScalars(string value, int limit)
        {
            value ??= string.Empty;
            int scalars = 0;
            int length = 0;
            while (length < value.Length && scalars < limit)
            {
                int width = char.IsHighSurrogate(value[length]) && length + 1 < value.Length && char.IsLowSurrogate(value[length + 1]) ? 2 : 1;
                length += width;
                scalars++;
            }
            return value.Substring(0, length).Trim().Normalize(NormalizationForm.FormC);
        }
    }
}
