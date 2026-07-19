using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Combat;
using KingdomTycoon.Presentation.Kingdom.Views;
using KingdomTycoon.Presentation.Navigation;
using KingdomTycoon.Presentation.Store;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P08StoreScreenTests
    {
        private static readonly string[] RequiredIds =
        {
            "P08_STORE_SCREEN", "P08_STORE_HEADER", "P08_STORE_BACK", "P08_KINGDOM_GOLD", "P08_MERC_SELECTOR", "P08_CATEGORY_TABS",
            "P08_TAB_BUY", "P08_TAB_SELL", "P08_TAB_HISTORY", "P08_FILTER", "P08_SORT", "P08_PRODUCT_VIEWPORT", "P08_PRODUCT_LIST",
            "P08_PRODUCT_ROW_POOL", "P08_DETAIL_PANEL", "P08_COMPARE_PANEL", "P08_QUANTITY_MINUS", "P08_QUANTITY_PLUS", "P08_PRIMARY_ACTION",
            "P08_POLICY_CONTROL", "P08_ACTIVITY_STRIP", "P08_STATE_PANEL", "P08_CONFIRM_MODAL", "P08_CONFIRM", "P08_CANCEL", "P08_RESULT_TOAST", "P08_DEBUG_PANEL"
        };

        [UnitySetUp]
        public IEnumerator ResetRoot()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CleanupRoot()
        {
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator KingdomSceneContainsOneStoreEntryAndStableScreenHierarchy()
        {
            StoreScreenPresenter presenter = null;
            yield return LoadKingdom(value => presenter = value);
            Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true);
            foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id);
            Assert.That(presenter.View.ProductList.PoolSize, Is.EqualTo(24));
            Assert.That(Object.FindObjectsByType<StoreEntryButton>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Camera.allCamerasCount, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator SelectingStoreRevealsEntryAndOpensStoreScreen()
        {
            StoreScreenPresenter presenter = null;
            yield return LoadKingdom(value => presenter = value);
            StoreEntryButton entry = Object.FindFirstObjectByType<StoreEntryButton>(FindObjectsInactive.Include);
            Assert.That(entry.gameObject.activeSelf, Is.True);
            Assert.That(entry.IsVisible, Is.False);

            FacilityWorldView store = Object.FindObjectsByType<FacilityWorldView>(FindObjectsSortMode.None).Single(value => value.FacilityId == "FAC_STORE");
            store.GetComponentInChildren<Button>(true).onClick.Invoke();
            yield return null;

            Assert.That(entry.IsVisible, Is.True);
            Assert.That(entry.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            entry.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(presenter.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator PersistentNavigationLoadsRegionAndReturnsToKingdom()
        {
            StoreScreenPresenter presenter = null;
            yield return LoadKingdom(value => presenter = value);
            SceneNavigationButton hunt = Object.FindObjectsByType<SceneNavigationButton>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(value => value.TargetScene == "Region");
            hunt.GetComponent<Button>().onClick.Invoke();
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Region");
            Assert.That(Object.FindFirstObjectByType<RegionCombatPresenter>(), Is.Not.Null);
            Assert.That(Camera.allCamerasCount, Is.GreaterThanOrEqualTo(1));

            SceneNavigationButton kingdom = Object.FindObjectsByType<SceneNavigationButton>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(value => value.TargetScene == "Kingdom");
            yield return null;
            Assert.That(kingdom.GetComponent<Button>().interactable, Is.True);
            kingdom.GetComponent<Button>().onClick.Invoke();
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom");
            Assert.That(Object.FindFirstObjectByType<StoreScreenPresenter>(FindObjectsInactive.Include), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator LockedStoreOpensStatePanelAndBackClosesScreen()
        {
            StoreScreenPresenter presenter = null;
            yield return LoadKingdom(value => presenter = value);
            presenter.Open(); yield return null;
            Assert.That(presenter.IsOpen, Is.True);
            presenter.View.ShowState(StoreUiState.Locked, "P08_STORE_LOCKED");
            Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true);
            Assert.That(nodes.Single(value => value.name == "P08_STATE_PANEL").gameObject.activeSelf, Is.True);
            nodes.Single(value => value.name == "P08_STORE_BACK").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(presenter.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator StoreExposesAllSixVisualStatesWithoutLosingContent()
        {
            StoreScreenPresenter presenter = null;
            yield return LoadKingdom(value => presenter = value);
            presenter.gameObject.SetActive(true); StoreScreenView view = presenter.View;
            foreach (StoreUiState state in new[] { StoreUiState.Loading, StoreUiState.Content, StoreUiState.Empty, StoreUiState.Error, StoreUiState.Locked, StoreUiState.Offline })
            {
                view.ShowState(state, "P08_TEST"); yield return null;
                bool panelExpected = state is StoreUiState.Loading or StoreUiState.Error or StoreUiState.Locked;
                Transform panel = presenter.GetComponentsInChildren<Transform>(true).Single(value => value.name == "P08_STATE_PANEL");
                Assert.That(panel.gameObject.activeSelf, Is.EqualTo(panelExpected), state.ToString());
            }
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSixteenNineAndTwentyNine()
        {
            StoreScreenPresenter presenter = null;
            yield return LoadKingdom(value => presenter = value);
            presenter.gameObject.SetActive(true);
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                foreach (Button button in presenter.GetComponentsInChildren<Button>(true))
                {
                    Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name);
                    Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name);
                }
            }
        }

        private static IEnumerator LoadKingdom(System.Action<StoreScreenPresenter> loaded)
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized);
            StoreScreenPresenter presenter = Object.FindFirstObjectByType<StoreScreenPresenter>(FindObjectsInactive.Include);
            Assert.That(presenter, Is.Not.Null);
            loaded(presenter); yield return null;
        }
    }
}
