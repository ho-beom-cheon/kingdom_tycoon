using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.EquipmentGrowth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P10EquipmentGrowthScreenTests
    {
        private static readonly string[] RequiredIds =
        {
            "P10_EQUIPMENT_GROWTH_SCREEN", "P10_HEADER", "P10_CLOSE", "P10_META", "P10_FACILITY_STATUS", "P10_ITEM_CARD", "P10_ITEM_TITLE", "P10_ITEM_STATS",
            "P10_PREVIOUS", "P10_NEXT", "P10_ENHANCE", "P10_REFINE_PANEL", "P10_REFINE_PREVIOUS", "P10_REFINE_NEXT", "P10_ROLL_REFINE", "P10_ACCEPT", "P10_KEEP",
            "P10_DISMANTLE", "P10_RESULT_PANEL", "P10_EMPTY_STATE", "P10_RESULT_TOAST"
        };
        [UnitySetUp] public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTearDown] public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTest]
        public IEnumerator BootstrapContainsStableGrowthHierarchyAndEntryOpensScreen()
        {
            yield return Load(); EquipmentGrowthScreenPresenter presenter = Object.FindFirstObjectByType<EquipmentGrowthScreenPresenter>(FindObjectsInactive.Include); Assert.That(presenter, Is.Not.Null); Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true);
            foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id); EquipmentGrowthEntryButton entry = Object.FindFirstObjectByType<EquipmentGrowthEntryButton>(FindObjectsInactive.Include); Assert.That(entry, Is.Not.Null);
            entry.GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.True, "entry opens growth screen"); Assert.That(nodes.Single(value => value.name == "P10_ITEM_CARD").gameObject.activeInHierarchy, Is.True, "growth content remains visible"); nodes.Single(value => value.name == "P10_CLOSE").GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.False, "close hides growth screen");
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSupportedAspects()
        {
            yield return Load(); EquipmentGrowthScreenPresenter presenter = Object.FindFirstObjectByType<EquipmentGrowthScreenPresenter>(FindObjectsInactive.Include); presenter.gameObject.SetActive(true);
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) }) { Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases(); foreach (Button button in presenter.GetComponentsInChildren<Button>(true)) { Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name); Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name); } }
        }
        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null; }
    }
}
