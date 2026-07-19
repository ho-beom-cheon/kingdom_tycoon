using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P09ProductionScreenTests
    {
        private static readonly string[] RequiredIds =
        {
            "P09_PRODUCTION_SCREEN", "P09_HEADER", "P09_CLOSE", "P09_META", "P09_FACILITY_LIST", "P09_FAC_BLACKSMITH",
            "P09_FAC_ALCHEMY", "P09_FAC_INFIRMARY", "P09_QUEUE_PANEL", "P09_QUEUE_TEXT", "P09_TARGET_PANEL", "P09_TARGET_LIST",
            "P09_SELECTED_TARGET", "P09_TARGET_MINUS", "P09_TARGET_PLUS", "P09_AUTOMATE", "P09_ADVANCE_TICKS", "P09_STATE_PANEL", "P09_RESULT_TOAST"
        };

        [UnitySetUp]
        public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTearDown]
        public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTest]
        public IEnumerator BootstrapContainsStableProductionHierarchyAndEntryOpensScreen()
        {
            yield return Load(); ProductionScreenPresenter presenter = Object.FindFirstObjectByType<ProductionScreenPresenter>(FindObjectsInactive.Include); Assert.That(presenter, Is.Not.Null);
            Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true); foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id);
            ProductionEntryButton entry = Object.FindFirstObjectByType<ProductionEntryButton>(FindObjectsInactive.Include); Assert.That(entry, Is.Not.Null); entry.GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(presenter.IsOpen, Is.True); nodes.Single(value => value.name == "P09_CLOSE").GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSupportedAspects()
        {
            yield return Load(); ProductionScreenPresenter presenter = Object.FindFirstObjectByType<ProductionScreenPresenter>(FindObjectsInactive.Include); presenter.gameObject.SetActive(true);
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                foreach (Button button in presenter.GetComponentsInChildren<Button>(true)) { Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name); Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name); }
            }
        }

        [UnityTest]
        public IEnumerator AutomationAndTickActionsKeepContentVisibleWhenFacilitiesAreLocked()
        {
            yield return Load(); ProductionEntryButton entry = Object.FindFirstObjectByType<ProductionEntryButton>(FindObjectsInactive.Include); entry.GetComponent<Button>().onClick.Invoke(); yield return null;
            ProductionScreenPresenter presenter = Object.FindFirstObjectByType<ProductionScreenPresenter>(FindObjectsInactive.Include); Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true);
            nodes.Single(value => value.name == "P09_AUTOMATE").GetComponent<Button>().onClick.Invoke(); yield return null; nodes.Single(value => value.name == "P09_ADVANCE_TICKS").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(nodes.Single(value => value.name == "P09_FACILITY_LIST").gameObject.activeInHierarchy, Is.True); Assert.That(nodes.Single(value => value.name == "P09_RESULT_TOAST").gameObject.activeInHierarchy, Is.True);
        }

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null;
        }
    }
}
