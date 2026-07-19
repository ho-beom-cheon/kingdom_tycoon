using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Raids;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P14RaidScreenTests
    {
        private static readonly string[] RequiredIds = { "P14_RAID_SCREEN", "P14_HEADER", "P14_CLOSE", "P14_RAID_1", "P14_RAID_6", "P14_MEMBER_1", "P14_MEMBER_8", "P14_PART_1", "P14_PART_4", "P14_BOSS_HP_FILL", "P14_DEPLOY", "P14_WARNING", "P14_RESULT", "P14_TRACE" };
        [UnitySetUp] public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTearDown] public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTest]
        public IEnumerator BootstrapContainsStableRaidHierarchyAndEntryCanOpenAndClose()
        {
            yield return Load(); RaidScreenPresenter presenter = Object.FindFirstObjectByType<RaidScreenPresenter>(FindObjectsInactive.Include); Assert.That(presenter, Is.Not.Null); Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true); foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id); RaidEntryButton entry = Object.FindFirstObjectByType<RaidEntryButton>(FindObjectsInactive.Include); Assert.That(entry, Is.Not.Null); entry.GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.True); nodes.Single(value => value.name == "P14_CLOSE").GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.False);
        }
        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSupportedAspects()
        {
            yield return Load(); RaidScreenPresenter presenter = Object.FindFirstObjectByType<RaidScreenPresenter>(FindObjectsInactive.Include); presenter.gameObject.SetActive(true); foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) }) { Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases(); foreach (Button button in presenter.GetComponentsInChildren<Button>(true)) { Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name); Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name); } }
        }
        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null; }
    }
}
