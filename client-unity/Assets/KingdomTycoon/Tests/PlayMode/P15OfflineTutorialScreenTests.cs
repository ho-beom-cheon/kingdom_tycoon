using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.OfflineTutorial;
using KingdomTycoon.Presentation.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P15OfflineTutorialScreenTests
    {
        private static readonly string[] RequiredIds = { "P15_OFFLINE_TUTORIAL_HUB", "P15_HEADER", "P15_CLOSE", "P15_STATUS", "P15_REWARDS", "P15_JOURNEY", "P15_ACTION", "P15_ADVANCE", "P15_SKIP", "P15_SKIP_ALL" };
        [UnitySetUp] public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTearDown] public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTest]
        public IEnumerator BootstrapContainsStableHierarchyAndEntryCanOpenAdvanceAndClose()
        {
            yield return Load(); OfflineTutorialHubPresenter presenter = Object.FindFirstObjectByType<OfflineTutorialHubPresenter>(FindObjectsInactive.Include); Assert.That(presenter, Is.Not.Null);
            Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true); foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id);
            OfflineTutorialEntryButton entry = Object.FindFirstObjectByType<OfflineTutorialEntryButton>(FindObjectsInactive.Include); Assert.That(entry, Is.Not.Null); entry.GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.True);
            nodes.Single(value => value.name == "P15_ADVANCE").GetComponent<Button>().onClick.Invoke(); yield return null;
            nodes.Single(value => value.name == "P15_CLOSE").GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSupportedAspects()
        {
            yield return Load(); OfflineTutorialHubPresenter presenter = Object.FindFirstObjectByType<OfflineTutorialHubPresenter>(FindObjectsInactive.Include); presenter.gameObject.SetActive(true);
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) }) { Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases(); foreach (Button button in presenter.GetComponentsInChildren<Button>(true)) { Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name); Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name); } }
        }

        [UnityTest]
        public IEnumerator UnifiedNavigationHasSixNonOverlappingPrimaryButtons()
        {
            yield return Load(); UnifiedNavigationMenu navigation = Object.FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include);
            Assert.That(navigation, Is.Not.Null); Transform safe = navigation.transform.parent;
            string[] ids = { "NAV_KINGDOM", "NAV_MERCENARIES", "P09_CRAFT_NAV_BUTTON", "P12_REGION_MAP_NAV_BUTTON", "P13_RECRUITMENT_NAV_BUTTON", "P15_MENU_NAV_BUTTON" };
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                RectTransform[] buttons = ids.Select(id => id == "P15_MENU_NAV_BUTTON" ? navigation.transform.Find(id).GetComponent<RectTransform>() : safe.Find(id).GetComponent<RectTransform>()).ToArray();
                Assert.That(buttons.All(value => value.gameObject.activeSelf), Is.True);
                for (int index = 0; index < buttons.Length; index++)
                {
                    Assert.That(buttons[index].rect.width, Is.GreaterThanOrEqualTo(64), ids[index]); Assert.That(buttons[index].rect.height, Is.GreaterThanOrEqualTo(64), ids[index]);
                    for (int other = index + 1; other < buttons.Length; other++) Assert.That(RectTransformUtility.RectangleContainsScreenPoint(buttons[index], buttons[other].TransformPoint(buttons[other].rect.center)), Is.False, $"{ids[index]} overlaps {ids[other]}");
                }
            }
        }

        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null; }
    }
}
