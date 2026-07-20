using System.Collections;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Presentation.Combat;
using KingdomTycoon.Presentation.Regions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KingdomTycoon.Tests.PlayMode
{
    public sealed class ContinuousHuntScreenTests
    {
        [UnitySetUp]
        public IEnumerator ResetRoot()
        {
            WorldHuntFeedbackPreferences.ResetForTests();
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CleanupRoot()
        {
            WorldHuntFeedbackPreferences.ResetForTests();
            if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator IntegratedWorldAllowsTwoAssignmentsAndShowsRealActorsInKorean()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            Transform[] nodes = screen.GetComponentsInChildren<Transform>(true);
            Button[] members = nodes.Where(value => value.name.StartsWith("용병_")).Select(value => value.GetComponent<Button>()).Where(value => value != null && value.gameObject.activeSelf).Take(2).ToArray();
            Assert.That(members.Length, Is.EqualTo(2)); members[0].onClick.Invoke(); members[1].onClick.Invoke(); yield return null;
            ContinuousHuntOverviewDto overview = AppRoot.Instance.Services.Get<ContinuousHuntGameService>().GetOverview();
            Assert.That(overview.Members.Count(value => value.AssignedRegionId == "REGION_R01"), Is.GreaterThanOrEqualTo(2));
            Assert.That(nodes.Count(value => value.name.StartsWith("용병동작_") && value.gameObject.activeSelf), Is.GreaterThanOrEqualTo(2));
            string text = string.Join(" ", screen.GetComponentsInChildren<TMP_Text>(true).Select(value => value.text));
            Assert.That(text, Does.Contain("상시 자동 사냥")); Assert.That(text, Does.Contain("통합 사냥 월드")); Assert.That(text, Does.Contain("왕국 외곽 초원")); Assert.That(text, Does.Contain("가방"));
            Assert.That(text.IndexOf('\uFFFD'), Is.EqualTo(-1), "replacement glyph must never be visible");
            ContinuousHuntGameService service = AppRoot.Instance.Services.Get<ContinuousHuntGameService>();
            foreach (ContinuousHuntMemberDto member in service.GetOverview().Members.Where(value => value.AssignedRegionId != null).ToArray()) service.Unassign(member.InstanceId);
            service.AdvanceTo(System.DateTimeOffset.UtcNow.AddMinutes(2));
        }

        [UnityTest]
        public IEnumerator WorldContainsKingdomFiveGroundsMonstersHpBarsAndClampedDrag()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(screen.WorldContent.Find("왕국거점"), Is.Not.Null);
            Assert.That(screen.WorldContent.Cast<Transform>().Count(value => value.name.StartsWith("월드지역_")), Is.EqualTo(5));
            Assert.That(screen.GetComponentsInChildren<Transform>(true).Count(value => value.name.StartsWith("몬스터동작_")), Is.EqualTo(25));
            Assert.That(screen.GetComponentsInChildren<Image>(true).Count(value => value.name == "몬스터현재체력"), Is.EqualTo(25));
            float before = screen.WorldContent.anchoredPosition.x; screen.DragSurface.PanBy(-500f); yield return null;
            Assert.That(screen.WorldContent.anchoredPosition.x, Is.LessThan(before));
            screen.DragSurface.PanBy(-10000f); float minimum = screen.WorldViewport.rect.width - screen.WorldContent.rect.width;
            Assert.That(screen.WorldContent.anchoredPosition.x, Is.EqualTo(minimum).Within(1f));
            screen.DragSurface.PanBy(10000f); Assert.That(screen.WorldContent.anchoredPosition.x, Is.EqualTo(0f).Within(1f));
        }

        [UnityTest]
        public IEnumerator GameFeelShowsRegionalDifferenceGrowthAndAccessiblePreferences()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            string text = string.Join(" ", screen.GetComponentsInChildren<TMP_Text>(true).Select(value => value.text));
            Assert.That(text, Does.Contain("안정")); Assert.That(text, Does.Contain("주요")); Assert.That(text, Does.Contain("장비"));
            Assert.That(text, Does.Contain("스킬")); Assert.That(text, Does.Contain("강화")); Assert.That(text, Does.Contain("전투 · 보상 소식"));

            Button sound = screen.GetComponentsInChildren<Button>(true).Single(value => value.name == "효과음설정");
            Button motion = screen.GetComponentsInChildren<Button>(true).Single(value => value.name == "모션설정");
            Assert.That(screen.SoundEnabled, Is.True); Assert.That(screen.ReducedMotion, Is.False);
            sound.onClick.Invoke(); motion.onClick.Invoke(); yield return null;
            Assert.That(screen.SoundEnabled, Is.False); Assert.That(screen.ReducedMotion, Is.True);
            Assert.That(sound.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("효과음 끔"));
            Assert.That(motion.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("모션 감소"));
        }

        [UnityTest]
        public IEnumerator CombatAdvanceCreatesDamageFeedbackAndKeepsFeedBounded()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            ContinuousHuntGameService service = AppRoot.Instance.Services.Get<ContinuousHuntGameService>();
            ContinuousHuntMemberDto member = service.GetOverview().Members.First(); service.Assign(member.InstanceId, "REGION_R01"); yield return null;
            for (int step = 1; step <= 8; step++)
            {
                service.AdvanceTo(System.DateTimeOffset.UtcNow.AddSeconds(step * 12));
                yield return null;
            }

            TMP_Text[] damage = screen.GetComponentsInChildren<TMP_Text>(true).Where(value => value.name == "피해표시").ToArray();
            Assert.That(damage.Any(value => !string.IsNullOrWhiteSpace(value.text)), Is.True, "combat must produce visible damage or defeat feedback");
            Assert.That(screen.FeedbackTexts.Count(value => value.gameObject.activeSelf), Is.LessThanOrEqualTo(3));
            Assert.That(screen.FeedbackTexts.Any(value => !string.IsNullOrWhiteSpace(value.text)), Is.True, "reward and combat news must reach the bounded feed");
            service.Unassign(member.InstanceId); service.AdvanceTo(System.DateTimeOffset.UtcNow.AddMinutes(3));
        }

        [UnityTest]
        public IEnumerator SupportedAspectsKeepAllTouchTargetsInsideSafeAreaWithoutOverlap()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open();
            foreach ((int width, int height) in new[] { (1920, 1080), (2400, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                Button[] buttons = screen.GetComponentsInChildren<Button>(true).Where(value => value.gameObject.activeSelf).ToArray();
                Assert.That(buttons.All(value => value.GetComponent<RectTransform>().rect.width >= 64 && value.GetComponent<RectTransform>().rect.height >= 64), Is.True);
                for (int i = 0; i < buttons.Length; i++) for (int j = i + 1; j < buttons.Length; j++)
                {
                    if (buttons[i].transform.parent != buttons[j].transform.parent) continue;
                    Rect a = WorldRect(buttons[i].GetComponent<RectTransform>()), b = WorldRect(buttons[j].GetComponent<RectTransform>());
                    Assert.That(a.Overlaps(b), Is.False, $"{buttons[i].name} overlaps {buttons[j].name}");
                }
            }
        }

        [UnityTest]
        public IEnumerator VisibleHuntingEntryPointOpensContinuousHuntScreen()
        {
            yield return Load();
            RegionMapEntryButton hunting = GameObject.Find("P12_REGION_MAP_NAV_BUTTON").GetComponent<RegionMapEntryButton>();

            hunting.OpenRegionMap();
            yield return null;

            ContinuousHuntScreenPresenter screen = Object.FindFirstObjectByType<ContinuousHuntScreenPresenter>(FindObjectsInactive.Include);
            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.gameObject.activeInHierarchy, Is.True);
            Canvas canvas = screen.GetComponent<Canvas>();
            Assert.That(canvas.isRootCanvas, Is.True);
            Assert.That(canvas.sortingOrder, Is.EqualTo(850));
            RegionMapScreenPresenter legacyMap = Object.FindFirstObjectByType<RegionMapScreenPresenter>(FindObjectsInactive.Include);
            Assert.That(legacyMap.gameObject.activeSelf, Is.False);
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4]; rect.GetWorldCorners(corners); return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null; }
    }
}
