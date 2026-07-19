using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Raids
{
    public sealed class RaidEntryButton : MonoBehaviour
    {
        [SerializeField] private RaidScreenPresenter presenter;
        [SerializeField] private Button button;
        public void Configure(RaidScreenPresenter value, Button source) { presenter = value; button = source; }
        public void OpenRaids() { presenter ??= FindFirstObjectByType<RaidScreenPresenter>(FindObjectsInactive.Include); presenter?.Open(); }
    }
}
