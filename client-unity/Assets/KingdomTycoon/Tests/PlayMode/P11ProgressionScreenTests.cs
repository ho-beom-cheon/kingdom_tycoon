using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P11ProgressionScreenTests
    {
        private static readonly string[] RequiredIds =
        {
            "P11_PROGRESSION_SCREEN", "P11_HEADER", "P11_CLOSE", "P11_META", "P11_GUILD_STATUS", "P11_MERCENARY_CARD", "P11_PREVIOUS", "P11_NEXT",
            "P11_RANK_JOURNEY", "P11_EXPERIENCE", "P11_REQUIREMENTS", "P11_COSTS", "P11_EQUIPMENT_SCORE", "P11_REVIEW_RESULT", "P11_PRIMARY", "P11_EMPTY_STATE", "P11_RESULT_TOAST"
        };
        [UnitySetUp] public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }
        [UnityTearDown] public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTest]
        public IEnumerator BootstrapContainsStableProgressionHierarchyAndEntryOpensScreen()
        {
            yield return Load(); ProgressionScreenPresenter presenter = Object.FindFirstObjectByType<ProgressionScreenPresenter>(FindObjectsInactive.Include); Assert.That(presenter, Is.Not.Null); Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true);
            foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id); ProgressionEntryButton entry = Object.FindFirstObjectByType<ProgressionEntryButton>(FindObjectsInactive.Include); Assert.That(entry, Is.Not.Null);
            entry.GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.True); Assert.That(nodes.Single(value => value.name == "P11_MERCENARY_CARD").gameObject.activeInHierarchy, Is.True); nodes.Single(value => value.name == "P11_CLOSE").GetComponent<Button>().onClick.Invoke(); yield return null; Assert.That(presenter.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSupportedAspects()
        {
            yield return Load(); ProgressionScreenPresenter presenter = Object.FindFirstObjectByType<ProgressionScreenPresenter>(FindObjectsInactive.Include); presenter.gameObject.SetActive(true);
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) }) { Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases(); foreach (Button button in presenter.GetComponentsInChildren<Button>(true)) { Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(64), button.name); Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(64), button.name); } }
        }
        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null; }
    }
}
