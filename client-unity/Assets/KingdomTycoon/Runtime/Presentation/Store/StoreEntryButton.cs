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
        [SerializeField] private CanvasGroup visibility;

        public bool IsVisible { get; private set; }

        public void Configure(FacilityDrawerView drawerView, StoreScreenPresenter presenter, Button value)
        {
            drawer = drawerView;
            store = presenter;
            button = value;
            visibility = GetComponent<CanvasGroup>();
            if (visibility == null) visibility = gameObject.AddComponent<CanvasGroup>();
            BindButton();
            ApplyVisibility(false);
        }

        private void Awake() => BindButton();

        private void OnEnable()
        {
            BindButton();
            RefreshVisibility();
        }

        private void Update() => RefreshVisibility();

        private void BindButton()
        {
            if (button == null) button = GetComponent<Button>();
            if (visibility == null) visibility = GetComponent<CanvasGroup>();
            if (button == null) return;

            button.onClick.RemoveListener(OpenStore);
            if (!HasPersistentBinding()) button.onClick.AddListener(OpenStore);
        }

        private bool HasPersistentBinding()
        {
            for (var index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            {
                if (button.onClick.GetPersistentTarget(index) == this
                    && button.onClick.GetPersistentMethodName(index) == nameof(OpenStore)) return true;
            }

            return false;
        }

        public void OpenStore()
        {
            store?.Open();
        }

        private void RefreshVisibility()
        {
            ApplyVisibility(drawer?.Current?.World?.FacilityId == "FAC_STORE");
        }

        private void ApplyVisibility(bool visible)
        {
            IsVisible = visible;
            if (visibility != null)
            {
                visibility.alpha = visible ? 1f : 0f;
                visibility.interactable = visible;
                visibility.blocksRaycasts = visible;
            }

            if (button != null) button.interactable = visible;
        }
    }
}
