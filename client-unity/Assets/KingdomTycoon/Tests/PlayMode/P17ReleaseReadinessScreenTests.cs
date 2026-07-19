using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Presentation.Navigation;
using KingdomTycoon.Presentation.OfflineTutorial;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class P17ReleaseReadinessScreenTests
    {
        private static readonly string[] Forbidden = { "KINGDOM OPERATIONS", "RELEASE 1.0", "CONTENT.", "SAVE.", "OFFLINE 8H", "REGION_R", " tick", "PLACEHOLDER" };

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
        public IEnumerator ReportIsKoreanOnlyAndUsesAssignedFonts()
        {
            yield return Load();
            OfflineTutorialHubPresenter report = Object.FindFirstObjectByType<OfflineTutorialHubPresenter>(FindObjectsInactive.Include);
            report.Open();
            yield return null;
            foreach (TMP_Text text in report.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(text.font, Is.Not.Null, text.name);
                string plain = Regex.Replace(text.text ?? string.Empty, "<[^>]+>", string.Empty);
                foreach (string token in Forbidden) Assert.That(plain, Does.Not.Contain(token).IgnoreCase, $"{text.name}: {plain}");
            }
        }

        [UnityTest]
        public IEnumerator ReportAndUnifiedNavigationStayInsideSafeAreaWithoutOverlap()
        {
            yield return Load();
            OfflineTutorialHubPresenter report = Object.FindFirstObjectByType<OfflineTutorialHubPresenter>(FindObjectsInactive.Include);
            report.Open();
            UnifiedNavigationMenu navigation = Object.FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include);
            string[] navIds = { "NAV_KINGDOM", "NAV_MERCENARIES", "P09_CRAFT_NAV_BUTTON", "P12_REGION_MAP_NAV_BUTTON", "P13_RECRUITMENT_NAV_BUTTON", "P15_MENU_NAV_BUTTON" };
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                Button[] reportButtons = report.GetComponentsInChildren<Button>(true).Where(value => value.gameObject.activeInHierarchy).ToArray();
                AssertButtons(reportButtons, width, height);
                Transform safe = navigation.transform.parent;
                Button[] navButtons = navIds.Select(id => (id == "P15_MENU_NAV_BUTTON" ? navigation.transform.Find(id) : safe.Find(id)).GetComponent<Button>()).ToArray();
                AssertButtons(navButtons, width, height);
            }
        }

        [UnityTest]
        public IEnumerator MenuConnectsEverySecondaryProcessAndCanReturnToKingdom()
        {
            yield return Load();
            UnifiedNavigationMenu navigation = Object.FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include);
            Button menu = navigation.transform.Find("P15_MENU_NAV_BUTTON").GetComponent<Button>();
            foreach (string id in new[] { "P15_MENU_GROWTH", "P15_MENU_PROGRESSION", "P15_MENU_RAID", "P15_MENU_REPORT" })
            {
                menu.onClick.Invoke(); yield return null;
                Transform target = navigation.transform.Find("P15_MENU_PANEL/" + id);
                Assert.That(target, Is.Not.Null, id);
                target.GetComponent<Button>().onClick.Invoke(); yield return null;
                Assert.That(navigation.transform.Find("P15_MENU_PANEL").gameObject.activeSelf, Is.False, id);
            }
            navigation.CloseAll();
            yield return SceneManager.LoadSceneAsync("Kingdom", LoadSceneMode.Single);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Kingdom"));
        }

        private static void AssertButtons(Button[] buttons, int width, int height)
        {
            Assert.That(buttons, Is.Not.Empty);
            for (int index = 0; index < buttons.Length; index++)
            {
                RectTransform rect = buttons[index].GetComponent<RectTransform>();
                Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(64), buttons[index].name + " width");
                Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(64), buttons[index].name + " height");
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Assert.That(corner.x, Is.InRange(-1f, width + 1f), buttons[index].name + " x");
                    Assert.That(corner.y, Is.InRange(-1f, height + 1f), buttons[index].name + " y");
                }
                for (int other = index + 1; other < buttons.Length; other++)
                {
                    RectTransform second = buttons[other].GetComponent<RectTransform>();
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rect, second.TransformPoint(second.rect.center)), Is.False, $"{buttons[index].name} overlaps {buttons[other].name}");
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
