using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Regions
{
    public sealed class RegionMapEntryButton : MonoBehaviour
    {
        [SerializeField] private RegionMapScreenPresenter presenter;
        [SerializeField] private Button button;
        public void Configure(RegionMapScreenPresenter value, Button trigger) { presenter = value; button = trigger; }
        public void OpenRegionMap() { if (presenter != null) presenter.Open(); }
    }
}
