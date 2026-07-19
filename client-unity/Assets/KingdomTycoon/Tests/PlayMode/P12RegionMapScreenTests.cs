using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Regions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P12RegionMapScreenTests
    {
        private static readonly string[] RequiredIds =
        {
            "P12_REGION_MAP_SCREEN", "P12_HEADER", "P12_CLOSE", "P12_META", "P12_KINGDOM_STATUS", "P12_MAP_PANEL", "P12_REGION_R01", "P12_REGION_R02",
            "P12_REGION_R03", "P12_REGION_R04", "P12_REGION_R05", "P12_DETAIL_PANEL", "P12_PROGRESS_FILL", "P12_REQUIREMENTS", "P12_DEPLOYMENT", "P12_ACTIVITY",
            "P12_POLICY", "P12_HUNT", "P12_EMPTY_STATE", "P12_RESULT_TOAST"
        };
        [UnitySetUp] public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTearDown] public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTest]
        public IEnumerator BootstrapContainsStableRegionHierarchyAndEntryOpensScreen()
        {
            yield return Load(); RegionMapScreenPresenter presenter = Object.FindFirstObjectByType<RegionMapScreenPresenter>(FindObjectsInactive.Include); Assert.That(presenter, Is.Not.Null); Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true);
            foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id); RegionMapEntryButton entry = Object.FindFirstObjectByType<RegionMapEntryButton>(FindObjectsInactive.Include); Assert.That(entry, Is.Not.Null);
            entry.GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.True); Assert.That(nodes.Single(value => value.name == "P12_MAP_PANEL").gameObject.activeInHierarchy, Is.True);
            nodes.Single(value => value.name == "P12_CLOSE").GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSupportedAspects()
        {
            yield return Load(); RegionMapScreenPresenter presenter = Object.FindFirstObjectByType<RegionMapScreenPresenter>(FindObjectsInactive.Include); presenter.gameObject.SetActive(true);
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                foreach (Button button in presenter.GetComponentsInChildren<Button>(true)) { Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name); Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name); }
            }
        }
        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null; }
    }
}
