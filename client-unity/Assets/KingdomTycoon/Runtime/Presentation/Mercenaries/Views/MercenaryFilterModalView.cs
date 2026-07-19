using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Application.Mercenaries;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Mercenaries.Views
{
    public sealed class MercenaryFilterSelection
    {
        public MercenaryFilterSelection(IEnumerable<string> jobs, IEnumerable<string> grades, IEnumerable<string> ranks,
            IEnumerable<string> states, string active, bool promotionReadyOnly, bool injuredOnly)
        {
            JobIds = (jobs ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            GradeIds = (grades ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            RankIds = (ranks ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            StateIds = (states ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            ActiveFilter = active ?? "ALL";
            PromotionReadyOnly = promotionReadyOnly;
            InjuredOnly = injuredOnly;
        }

        public IReadOnlyList<string> JobIds { get; }
        public IReadOnlyList<string> GradeIds { get; }
        public IReadOnlyList<string> RankIds { get; }
        public IReadOnlyList<string> StateIds { get; }
        public string ActiveFilter { get; }
        public bool PromotionReadyOnly { get; }
        public bool InjuredOnly { get; }
    }

    public sealed class MercenaryFilterModalView : MonoBehaviour
    {
        private static readonly Color32 NormalColor = new(60, 79, 94, 255);
        private static readonly Color32 SelectedColor = new(181, 113, 49, 255);

        [SerializeField] private Button scrim;
        [SerializeField] private Button close;
        [SerializeField] private Button apply;
        [SerializeField] private Button reset;
        [SerializeField] private Button[] options;
        [SerializeField] private string[] dimensions;
        [SerializeField] private string[] values;

        private readonly HashSet<string> jobs = new(StringComparer.Ordinal);
        private readonly HashSet<string> grades = new(StringComparer.Ordinal);
        private readonly HashSet<string> ranks = new(StringComparer.Ordinal);
        private readonly HashSet<string> states = new(StringComparer.Ordinal);
        private string active = "ALL";
        private bool promotionReadyOnly;
        private bool injuredOnly;

        public event Action<MercenaryFilterSelection> Applied;
        public event Action CloseRequested;

        public void Configure(Button scrimButton, Button closeButton, Button applyButton, Button resetButton,
            Button[] optionButtons, string[] optionDimensions, string[] optionValues)
        {
            scrim = scrimButton;
            close = closeButton;
            apply = applyButton;
            reset = resetButton;
            options = optionButtons;
            dimensions = optionDimensions;
            values = optionValues;
        }

        private void Awake()
        {
            scrim.onClick.AddListener(Close);
            close.onClick.AddListener(Close);
            apply.onClick.AddListener(Apply);
            reset.onClick.AddListener(ResetAll);
            for (int index = 0; index < options.Length; index++)
            {
                int captured = index;
                options[index].onClick.AddListener(() => Toggle(captured));
            }
        }

        public void Open(MercenaryRosterQueryDto query)
        {
            query ??= new MercenaryRosterQueryDto();
            Copy(jobs, query.JobIds);
            Copy(grades, query.GradeIds);
            Copy(ranks, query.RankIds);
            Copy(states, query.StateIds);
            active = query.ActiveFilter;
            promotionReadyOnly = query.PromotionReadyOnly;
            injuredOnly = query.InjuredOnly;
            RefreshVisuals();
            gameObject.SetActive(true);
        }

        public void CloseModal() => gameObject.SetActive(false);

        public void SelectForFixture(string dimension, string value)
        {
            int index = -1;
            for (int i = 0; i < values.Length; i++) if (values[i] == value && dimensions[i] == dimension) { index = i; break; }
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Toggle(index);
        }

        public void ApplyForFixture() => Apply();
        public void ResetForFixture() => ResetAll();

        private static void Copy(ISet<string> destination, IEnumerable<string> source)
        {
            destination.Clear();
            foreach (string value in source) destination.Add(value);
        }

        private void Toggle(int index)
        {
            if (index < 0 || index >= options.Length || index >= dimensions.Length || index >= values.Length) return;
            string dimension = dimensions[index];
            string value = values[index];
            switch (dimension)
            {
                case "JOB": Toggle(jobs, value); break;
                case "GRADE": Toggle(grades, value); break;
                case "RANK": Toggle(ranks, value); break;
                case "STATE": Toggle(states, value); break;
                case "ACTIVE": active = value; break;
                case "PROMOTION": promotionReadyOnly = !promotionReadyOnly; break;
                case "INJURY": injuredOnly = !injuredOnly; break;
                default: throw new InvalidOperationException("P05_FILTER_DIMENSION_INVALID: " + dimension);
            }
            RefreshVisuals();
        }

        private static void Toggle(ISet<string> selected, string value)
        {
            if (!selected.Add(value)) selected.Remove(value);
        }

        private void ResetAll()
        {
            jobs.Clear();
            grades.Clear();
            ranks.Clear();
            states.Clear();
            active = "ALL";
            promotionReadyOnly = false;
            injuredOnly = false;
            RefreshVisuals();
        }

        private void Apply()
        {
            Applied?.Invoke(new MercenaryFilterSelection(jobs, grades, ranks, states, active, promotionReadyOnly, injuredOnly));
        }

        private void Close() => CloseRequested?.Invoke();

        private void RefreshVisuals()
        {
            for (int index = 0; index < options.Length; index++)
            {
                bool selected = dimensions[index] switch
                {
                    "JOB" => jobs.Contains(values[index]),
                    "GRADE" => grades.Contains(values[index]),
                    "RANK" => ranks.Contains(values[index]),
                    "STATE" => states.Contains(values[index]),
                    "ACTIVE" => active == values[index],
                    "PROMOTION" => promotionReadyOnly,
                    "INJURY" => injuredOnly,
                    _ => false
                };
                if (options[index].targetGraphic is Graphic graphic) graphic.color = selected ? SelectedColor : NormalColor;
            }
        }
    }
}
