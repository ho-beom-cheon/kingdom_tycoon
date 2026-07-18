using System;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Mercenaries.Views
{
    public sealed class MercenaryOptionModalView : MonoBehaviour
    {
        [SerializeField] private Button scrim;
        [SerializeField] private Button close;
        [SerializeField] private Button[] options;
        [SerializeField] private string[] values;

        public event Action<string> Selected;
        public event Action CloseRequested;

        public void Configure(Button scrimButton, Button closeButton, Button[] optionButtons, string[] optionValues)
        { scrim = scrimButton; close = closeButton; options = optionButtons; values = optionValues; }

        private void Awake()
        {
            scrim.onClick.AddListener(Close);
            close.onClick.AddListener(Close);
            for (int index = 0; index < options.Length; index++) { int captured = index; options[index].onClick.AddListener(() => Select(captured)); }
        }

        private void Close() => CloseRequested?.Invoke();
        private void Select(int index) { if (index < values.Length) Selected?.Invoke(values[index]); }
        public void Open() => gameObject.SetActive(true);
        public void CloseModal() => gameObject.SetActive(false);
        public void SelectForFixture(string value)
        {
            int index = Array.IndexOf(values, value);
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Select(index);
        }
    }
}
