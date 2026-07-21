using System.Collections;
using System.IO;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Presentation.Combat;
using KingdomTycoon.Presentation.Regions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
            Assert.That(members.Length, Is.EqualTo(2));
            members[0].onClick.Invoke(); yield return null;
            members[1].onClick.Invoke(); yield return null;
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
        public IEnumerator WorldContainsCentralKingdomFourDirectionsMonstersZoomAndClampedTwoAxisDrag()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(screen.WorldContent.Find("왕국거점"), Is.Not.Null);
            Assert.That(screen.WorldContent.Cast<Transform>().Count(value => value.name.StartsWith("월드지역_")), Is.EqualTo(5));
            Assert.That(screen.GetComponentsInChildren<Transform>(true).Count(value => value.name.StartsWith("몬스터동작_")), Is.EqualTo(25));
            Assert.That(screen.GetComponentsInChildren<Image>(true).Count(value => value.name == "몬스터현재체력"), Is.EqualTo(25));
            Assert.That(screen.IsolatedCanvasCount, Is.GreaterThanOrEqualTo(8), "월드, 사냥터, 용병 동적 레이어는 Canvas 갱신 범위를 분리해야 합니다.");
            Vector2 north = screen.WorldContent.Find("월드지역_REGION_R01").GetComponent<RectTransform>().anchoredPosition;
            Vector2 east = screen.WorldContent.Find("월드지역_REGION_R02").GetComponent<RectTransform>().anchoredPosition;
            Vector2 south = screen.WorldContent.Find("월드지역_REGION_R03").GetComponent<RectTransform>().anchoredPosition;
            Vector2 west = screen.WorldContent.Find("월드지역_REGION_R04").GetComponent<RectTransform>().anchoredPosition;
            Assert.That(north.y, Is.GreaterThan(0f)); Assert.That(east.x, Is.GreaterThan(0f));
            Assert.That(south.y, Is.LessThan(0f)); Assert.That(west.x, Is.LessThan(0f));

            screen.DragSurface.PanBy(new Vector2(-500f, 420f)); yield return null;
            Assert.That(screen.WorldContent.anchoredPosition.x, Is.LessThan(0f));
            Assert.That(screen.WorldContent.anchoredPosition.y, Is.GreaterThan(0f));
            screen.DragSurface.ZoomBy(10f);
            Assert.That(screen.DragSurface.Zoom, Is.EqualTo(WorldMapDragSurface.MaximumZoom).Within(.001f));
            screen.DragSurface.PanBy(new Vector2(-10000f, 10000f));
            float maximumX = (screen.WorldContent.rect.width * screen.DragSurface.Zoom - screen.WorldViewport.rect.width) * .5f;
            float maximumY = (screen.WorldContent.rect.height * screen.DragSurface.Zoom - screen.WorldViewport.rect.height) * .5f;
            Assert.That(screen.WorldContent.anchoredPosition.x, Is.EqualTo(-maximumX).Within(1f));
            Assert.That(screen.WorldContent.anchoredPosition.y, Is.EqualTo(maximumY).Within(1f));
            screen.DragSurface.ResetView();
            Assert.That(screen.WorldContent.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(screen.DragSurface.Zoom, Is.EqualTo(WorldMapDragSurface.StartingZoom).Within(.001f));
        }

        [UnityTest]
        public IEnumerator CameraDragCoalescesPointerInputAndTracksTheFingerResponsively()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null; Canvas.ForceUpdateCanvases();
            Vector2 before = screen.WorldContent.anchoredPosition;
            float canvasScale = screen.GetComponent<Canvas>().scaleFactor;
            var pointer = new PointerEventData(EventSystem.current);
            screen.DragSurface.OnBeginDrag(pointer);
            for (int index = 0; index < 12; index++)
            {
                pointer.delta = new Vector2(-8f, 5f);
                screen.DragSurface.OnDrag(pointer);
            }

            Assert.That(screen.DragSurface.IsCameraMoving, Is.True);
            Assert.That(screen.WorldContent.anchoredPosition, Is.EqualTo(before), "pointer bursts must be applied once per rendered frame");
            yield return null;

            Vector2 expected = before + new Vector2(-96f, 60f) / canvasScale * WorldMapDragSurface.DirectManipulationGain;
            Assert.That(screen.WorldContent.anchoredPosition.x, Is.EqualTo(expected.x).Within(1f));
            Assert.That(screen.WorldContent.anchoredPosition.y, Is.EqualTo(expected.y).Within(1f));
            screen.DragSurface.OnEndDrag(pointer);
            Assert.That(screen.DragSurface.TapSuppressed, Is.True);
        }

        [UnityTest]
        public IEnumerator CameraDragDoesNotConsumeTheNextIntentionalBuildingTap()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            var pointer = new PointerEventData(EventSystem.current) { delta = new Vector2(64f, 0f) };
            screen.DragSurface.OnBeginDrag(pointer);
            screen.DragSurface.OnDrag(pointer);
            screen.DragSurface.OnEndDrag(pointer);
            Assert.That(screen.DragSurface.TapSuppressed, Is.True);

            yield return new WaitForSecondsRealtime(WorldMapDragSurface.TapSuppressionLifetime + .02f);
            Assert.That(screen.DragSurface.TapSuppressed, Is.False);
            Button tavern = screen.WorldContent.Find("왕국거점/시설_FAC_TAVERN").GetComponent<Button>();
            tavern.onClick.Invoke();
            yield return null;

            Assert.That(screen.FacilityPanelOpen, Is.True);
            Assert.That(screen.SelectedFacilityId, Is.EqualTo("FAC_TAVERN"));
        }

        [UnityTest]
        public IEnumerator HuntingGroundTapOpensAssignmentSheetAndSupportsSeveralMercenaries()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            Button ground = screen.WorldContent.Find("월드지역_REGION_R01").GetComponent<Button>();
            ground.onClick.Invoke(); yield return null;
            Assert.That(screen.AssignmentSheetOpen, Is.True);
            Button[] members = screen.GetComponentsInChildren<Button>(true).Where(value => value.name.StartsWith("용병_") && value.gameObject.activeSelf).Take(3).ToArray();
            Assert.That(members.Length, Is.EqualTo(3));
            foreach (Button member in members) { member.onClick.Invoke(); yield return null; }
            ContinuousHuntGameService service = AppRoot.Instance.Services.Get<ContinuousHuntGameService>();
            Assert.That(service.GetOverview().Members.Count(value => value.AssignedRegionId == "REGION_R01"), Is.GreaterThanOrEqualTo(3));
            foreach (ContinuousHuntMemberDto member in service.GetOverview().Members.Where(value => value.AssignedRegionId != null).ToArray()) service.Unassign(member.InstanceId);
        }

        [UnityTest]
        public IEnumerator KingdomBuildingsOpenContextualKoreanFeaturePanels()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            Button tavern = screen.WorldContent.Find("왕국거점/시설_FAC_TAVERN").GetComponent<Button>();
            tavern.onClick.Invoke(); yield return null;
            Assert.That(screen.FacilityPanelOpen, Is.True);
            Assert.That(screen.SelectedFacilityId, Is.EqualTo("FAC_TAVERN"));
            string tavernText = string.Join(" ", screen.GetComponentsInChildren<TMP_Text>(true).Where(value => value.gameObject.activeInHierarchy).Select(value => value.text));
            Assert.That(tavernText, Does.Contain("황금 사슴 주점"));
            Assert.That(tavernText, Does.Contain("용병 모집"));

            Button blacksmith = screen.WorldContent.Find("왕국거점/시설_FAC_BLACKSMITH").GetComponent<Button>();
            blacksmith.onClick.Invoke(); yield return null;
            Assert.That(screen.SelectedFacilityId, Is.EqualTo("FAC_BLACKSMITH"));
            Assert.That(string.Join(" ", screen.GetComponentsInChildren<TMP_Text>(true).Where(value => value.gameObject.activeInHierarchy).Select(value => value.text)), Does.Contain("장비 공방"));
        }

        [UnityTest]
        public IEnumerator MercenaryTapOpensRealStatusEquipmentAndActivityDetail()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            Button actor = screen.GetComponentsInChildren<Button>(true).First(value => value.name == "용병선택_0" && value.gameObject.activeInHierarchy);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = -101, position = new Vector2(400f, 700f) };
            ExecuteEvents.Execute(actor.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            pointer.position += new Vector2(4f, 3f);
            ExecuteEvents.Execute(actor.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            yield return null;
            Assert.That(screen.CharacterDetailOpen, Is.True);
            Assert.That(screen.SelectedMemberInstanceId, Is.Not.Null.And.Not.Empty);
            string text = string.Join(" ", screen.GetComponentsInChildren<TMP_Text>(true).Where(value => value.gameObject.activeInHierarchy).Select(value => value.text));
            Assert.That(text, Does.Contain("장비·기록"));
            Assert.That(text, Does.Contain("현재 활동"));
            Assert.That(text, Does.Contain("무기"));
            Assert.That(text, Does.Contain("개인 골드"));
            Assert.That(text.IndexOf('\uFFFD'), Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator TownMercenaryTouchTargetsUseSeparatedLivingPositions()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            RectTransform[] actors = screen.GetComponentsInChildren<RectTransform>(true)
                .Where(value => value.name.StartsWith("용병동작_") && value.gameObject.activeInHierarchy).ToArray();
            Assert.That(actors.Length, Is.GreaterThanOrEqualTo(4));
            for (int i = 0; i < actors.Length; i++) for (int j = i + 1; j < actors.Length; j++)
            {
                Rect a = new(actors[i].anchoredPosition - actors[i].sizeDelta * .5f, actors[i].sizeDelta);
                Rect b = new(actors[j].anchoredPosition - actors[j].sizeDelta * .5f, actors[j].sizeDelta);
                Assert.That(a.Overlaps(b), Is.False, $"{actors[i].name} overlaps {actors[j].name}");
            }
        }

        [UnityTest]
        public IEnumerator MobileWorldOpensAsKingdomHomeAndReplacesBottomNavigationWithTopMenu()
        {
            yield return Load(); yield return null;
            ContinuousHuntScreenPresenter screen = Object.FindFirstObjectByType<ContinuousHuntScreenPresenter>(FindObjectsInactive.Include);
            Assert.That(screen, Is.Not.Null);
            Assert.That(screen.gameObject.activeInHierarchy, Is.True);
            Assert.That(screen.WorldContent.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(screen.DragSurface.Zoom, Is.EqualTo(WorldMapDragSurface.StartingZoom).Within(.001f));
            Button menu = screen.GetComponentsInChildren<Button>(true).Single(value => value.name == "통합메뉴버튼");
            menu.onClick.Invoke(); yield return null;
            Assert.That(screen.MobileMenuOpen, Is.True);
            Assert.That(screen.GetComponentsInChildren<Button>(true).Count(value => value.name.StartsWith("메뉴_")), Is.EqualTo(10));
            Assert.That(Object.FindFirstObjectByType<KingdomTycoon.Presentation.Navigation.UnifiedNavigationMenu>(FindObjectsInactive.Include).IsPrimaryNavigationVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PortraitWorldCaptureProducesVisualEvidence()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("visual evidence requires a graphics device");
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            Screen.SetResolution(1080, 1920, false); yield return null; Canvas.ForceUpdateCanvases();
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "artifacts"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "mobile-living-world-portrait.png");
            int distinctColors = Capture(screen, path, 1080, 1920);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(20000));
            Assert.That(distinctColors, Is.GreaterThan(12), "rendered evidence must contain the living world, not a blank frame");
        }

        [UnityTest]
        public IEnumerator CharacterDetailCaptureProducesVisualEvidence()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("visual evidence requires a graphics device");
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            Screen.SetResolution(1080, 1920, false); yield return null; Canvas.ForceUpdateCanvases();
            Button actor = screen.GetComponentsInChildren<Button>(true).First(value => value.name == "용병선택_0" && value.gameObject.activeInHierarchy);
            actor.onClick.Invoke(); yield return null; Canvas.ForceUpdateCanvases();
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "artifacts"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "world-gameplay-polish-character-detail.png");
            int distinctColors = Capture(screen, path, 1080, 1920);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(new FileInfo(path).Length, Is.GreaterThan(20000));
            Assert.That(distinctColors, Is.GreaterThan(12));
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
        public IEnumerator PixelArtReplacesFlatActorsMonstersEnvironmentAndKingdomBlocks()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open(); yield return null;
            Assert.That(screen.PixelArtSpriteCount, Is.EqualTo(26));
            Image[] images = screen.GetComponentsInChildren<Image>(true);
            Image[] jobIcons = images.Where(value => value.name == "직업아이콘").ToArray();
            Assert.That(jobIcons.Length, Is.EqualTo(8)); Assert.That(jobIcons.All(value => value.sprite != null && value.sprite.texture.filterMode == FilterMode.Point), Is.True);
            ContinuousHuntGameService service = AppRoot.Instance.Services.Get<ContinuousHuntGameService>();
            int expectedVisibleJobs = service.GetOverview().Members.Take(jobIcons.Length).Select(value => value.JobId).Distinct().Count();
            Assert.That(jobIcons.Where(value => value.transform.parent.gameObject.activeSelf).Select(value => value.sprite.name).Distinct().Count(), Is.EqualTo(expectedVisibleJobs));

            Image[] monsterBodies = images.Where(value => value.name == "몬스터몸").ToArray();
            Assert.That(monsterBodies.Length, Is.EqualTo(25)); Assert.That(monsterBodies.All(value => value.sprite != null), Is.True);
            Assert.That(monsterBodies.Select(value => value.sprite.name).Distinct().Count(), Is.GreaterThanOrEqualTo(5));

            Image[] decorations = images.Where(value => value.name.StartsWith("환경장식_")).ToArray();
            Assert.That(decorations.Length, Is.EqualTo(15)); Assert.That(decorations.All(value => value.sprite != null && !value.raycastTarget), Is.True);
            Assert.That(screen.ExternalSpriteCount, Is.GreaterThanOrEqualTo(10));
            Assert.That(decorations.Any(value => value.sprite.name.StartsWith("CC0_NinjaAdventure_")), Is.True);
            Assert.That(images.Single(value => value.name == "왕국픽셀랜드마크").sprite, Is.Not.Null);
            Image[] facilities = images.Where(value => value.name == "시설픽셀아트").ToArray();
            Assert.That(facilities.Length, Is.EqualTo(5));
            Assert.That(facilities.Select(value => value.sprite.name).Distinct().Count(), Is.EqualTo(5));
            Assert.That(screen.GetComponentsInChildren<Transform>(true).Any(value => value.name is "성채" or "상점" or "대장간"), Is.False);

            ContinuousHuntMemberDto member = service.GetOverview().Members.First();
            service.Assign(member.InstanceId, "REGION_R01"); yield return null;
            Image activeActor = screen.GetComponentsInChildren<Image>(true).First(value => value.name == "용병몸" && value.gameObject.activeInHierarchy);
            Assert.That(activeActor.sprite, Is.SameAs(jobIcons.First(value => value.transform.parent.gameObject.activeSelf).sprite));
            service.Unassign(member.InstanceId); service.AdvanceTo(System.DateTimeOffset.UtcNow.AddMinutes(2));
        }

        [UnityTest]
        public IEnumerator SupportedAspectsKeepAllTouchTargetsInsideSafeAreaWithoutOverlap()
        {
            yield return Load(); ContinuousHuntScreenPresenter screen = ContinuousHuntScreenPresenter.Install(); screen.Open();
            foreach ((int width, int height) in new[] { (1080, 1920), (1080, 2400), (1920, 1080) })
            {
                Screen.SetResolution(width, height, false); yield return null; Canvas.ForceUpdateCanvases();
                Button[] buttons = screen.GetComponentsInChildren<Button>(true).Where(value => value.gameObject.activeInHierarchy).ToArray();
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
            RegionMapEntryButton hunting = Object.FindFirstObjectByType<RegionMapEntryButton>(FindObjectsInactive.Include);
            Assert.That(hunting, Is.Not.Null);

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

        private static int Capture(ContinuousHuntScreenPresenter screen, string path, int width, int height)
        {
            Canvas canvas = screen.GetComponent<Canvas>();
            RectTransform root = screen.GetComponent<RectTransform>();
            RenderMode oldMode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            Vector2 oldSize = root.sizeDelta;
            Vector3 oldPosition = root.position;
            Vector3 oldScale = root.localScale;
            var cameraObject = new GameObject("생활월드증적카메라", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(8, 18, 20, 255);
            camera.orthographic = true;
            camera.orthographicSize = height / 2f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100f;
            camera.aspect = width / (float)height;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f);
            root.sizeDelta = new Vector2(width, height);
            root.position = Vector3.zero;
            root.localScale = Vector3.one;
            RectTransform safe = screen.transform.Find("월드배경/안전영역").GetComponent<RectTransform>();
            safe.anchorMin = Vector2.zero;
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            screen.DragSurface.ResetView();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, camera.backgroundColor);
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false, false);
            var colors = new System.Collections.Generic.HashSet<Color32>();
            for (int y = 0; y < height; y += 40)
            for (int x = 0; x < width; x += 40)
                colors.Add(texture.GetPixel(x, y));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            root.sizeDelta = oldSize;
            root.position = oldPosition;
            root.localScale = oldScale;
            return colors.Count;
        }

        private static IEnumerator Load() { yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single); yield return new WaitUntil(() => SceneManager.GetActiveScene().name == "Kingdom" && AppRoot.Instance != null && AppRoot.Instance.IsInitialized); yield return null; }
    }
}
