using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Production
{
    public sealed class ProductionEntryButton : MonoBehaviour
    {
        [SerializeField] private ProductionScreenPresenter presenter;
        [SerializeField] private Button button;

        public void Configure(ProductionScreenPresenter value, Button trigger)
        {
            presenter = value;
            button = trigger;
        }

        public void OpenProduction()
        {
            if (presenter != null) presenter.Open();
        }
    }
}
