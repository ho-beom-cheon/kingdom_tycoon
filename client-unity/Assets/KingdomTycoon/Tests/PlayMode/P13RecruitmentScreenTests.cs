using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Recruitment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P13RecruitmentScreenTests
    {
        private static readonly string[] RequiredIds =
        {
            "P13_RECRUITMENT_SCREEN", "P13_HEADER", "P13_CLOSE", "P13_WALLET", "P13_CAPACITY", "P13_AUTHORITY",
            "P13_TAB_TAVERN", "P13_TAB_SPECIAL", "P13_TAB_HISTORY", "P13_TAVERN_ROOT", "P13_TAVERN_REFRESH",
            "P13_CANDIDATE_1", "P13_CANDIDATE_5", "P13_DETAIL_PANEL", "P13_CANDIDATE_LOCK", "P13_HIRE",
            "P13_SPECIAL_ROOT", "P13_PITY_S", "P13_PITY_S_FILL", "P13_PITY_SS", "P13_PITY_SS_FILL", "P13_SPECIAL_RECRUIT",
            "P13_HISTORY_ROOT", "P13_HISTORY_LIST", "P13_STATE_PANEL", "P13_RESULT_TOAST"
        };

        [UnitySetUp]
        public IEnumerator ResetRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTearDown]
        public IEnumerator CleanupRoot() { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; }

        [UnityTest]
        public IEnumerator BootstrapContainsStableHierarchyAndTabsAreOperable()
        {
            yield return Load();
            RecruitmentScreenPresenter presenter = Object.FindFirstObjectByType<RecruitmentScreenPresenter>(FindObjectsInactive.Include);
            Assert.That(presenter, Is.Not.Null);
            Transform[] nodes = presenter.GetComponentsInChildren<Transform>(true);
            foreach (string id in RequiredIds) Assert.That(nodes.Count(value => value.name == id), Is.EqualTo(1), id);
            RecruitmentEntryButton entry = Object.FindFirstObjectByType<RecruitmentEntryButton>(FindObjectsInactive.Include);
            Assert.That(entry, Is.Not.Null);
            entry.GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(presenter.gameObject.activeSelf, Is.True);
            Assert.That(nodes.Single(value => value.name == "P13_TAVERN_ROOT").gameObject.activeSelf, Is.True);
            nodes.Single(value => value.name == "P13_TAB_SPECIAL").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(nodes.Single(value => value.name == "P13_SPECIAL_ROOT").gameObject.activeSelf, Is.True);
            nodes.Single(value => value.name == "P13_TAB_HISTORY").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(nodes.Single(value => value.name == "P13_HISTORY_ROOT").gameObject.activeSelf, Is.True);
            nodes.Single(value => value.name == "P13_CLOSE").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(presenter.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TouchTargetsRemainAtLeastSixtyFourPixelsAtSupportedAspects()
        {
            yield return Load();
            RecruitmentScreenPresenter presenter = Object.FindFirstObjectByType<RecruitmentScreenPresenter>(FindObjectsInactive.Include);
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

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized);
            yield return null;
        }
    }
}
