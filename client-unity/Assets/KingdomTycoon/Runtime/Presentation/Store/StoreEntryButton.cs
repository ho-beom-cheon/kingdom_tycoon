using KingdomTycoon.Presentation.Kingdom.Views;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Store
{
    public sealed class StoreEntryButton : MonoBehaviour
    {
        [SerializeField] private FacilityDrawerView drawer;
        [SerializeField] private StoreScreenPresenter store;
        [SerializeField] private Button button;

        public void Configure(FacilityDrawerView drawerView, StoreScreenPresenter presenter, Button value)
        {
            drawer = drawerView;
            store = presenter;
            button = value;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(store.Open);
        }

        private void Update()
        {
            if (button != null) button.gameObject.SetActive(drawer?.Current?.World?.FacilityId == "FAC_STORE");
        }
    }
}
