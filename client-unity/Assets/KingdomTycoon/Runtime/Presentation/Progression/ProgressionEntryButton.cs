using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Progression
{
    public sealed class ProgressionEntryButton : MonoBehaviour
    {
        [SerializeField] private ProgressionScreenPresenter presenter;
        [SerializeField] private Button button;
        public void Configure(ProgressionScreenPresenter value, Button trigger) { presenter = value; button = trigger; }
        public void OpenProgression() { if (presenter != null) presenter.Open(); }
    }
}
