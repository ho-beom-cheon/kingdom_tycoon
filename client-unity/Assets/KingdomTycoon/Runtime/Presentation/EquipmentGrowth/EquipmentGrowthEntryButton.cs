using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.EquipmentGrowth
{
    public sealed class EquipmentGrowthEntryButton : MonoBehaviour
    {
        [SerializeField] private EquipmentGrowthScreenPresenter presenter;
        [SerializeField] private Button button;
        public void Configure(EquipmentGrowthScreenPresenter value, Button trigger) { presenter = value; button = trigger; }
        public void OpenEquipmentGrowth() { if (presenter != null) presenter.Open(); }
    }
}
