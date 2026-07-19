using System;
using System.IO;
using System.Linq;
using KingdomTycoon.Presentation.EquipmentGrowth;
using KingdomTycoon.Presentation.Mercenaries;
using KingdomTycoon.Presentation.Navigation;
using KingdomTycoon.Presentation.OfflineTutorial;
using KingdomTycoon.Presentation.Production;
using KingdomTycoon.Presentation.Progression;
using KingdomTycoon.Presentation.Raids;
using KingdomTycoon.Presentation.Recruitment;
using KingdomTycoon.Presentation.Regions;
using KingdomTycoon.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomTycoon.Editor
{
    public static class P15OfflineTutorialSetup
    {
        public const string Root = "Assets/KingdomTycoon/ContentGenerated/P15OfflineTutorial";
        public const string ScreenPath = Root + "/Prefabs/P15_OFFLINE_TUTORIAL_HUB.prefab";
        public const string MarkerPath = Root + "/P15OfflineTutorial.marker.asset";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "P15-INTEGRATION-STABILIZATION-UI-v1.1.0";
        public static readonly string[] RequiredIds = { "P15_OFFLINE_TUTORIAL_HUB", "P15_HEADER", "P15_CLOSE", "P15_STATUS", "P15_REWARDS", "P15_JOURNEY", "P15_ACTION", "P15_ADVANCE", "P15_SKIP", "P15_SKIP_ALL" };

        [MenuItem("Kingdom Tycoon/P15/Generate Offline Tutorial Assets")]
        public static void Run()
        {
            ValidateContent(); GenerateScreen(); IntegrateBootstrapScene(); P15GeneratedAssetVerifier.Verify();
            Debug.Log("P15_SETUP_COMPLETED");
        }

        public static void ValidateContent()
        {
            string directory = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.13"));
            string manifest = Path.Combine(directory, "content_manifest.json");
            if (!File.Exists(manifest)) throw new BuildFailedException("P15_CONTENT_MANIFEST_MISSING");
            string text = File.ReadAllText(manifest);
            if (!text.Contains("\"contentVersion\":\"1.0.0-content.13\"", StringComparison.Ordinal) || !text.Contains("\"csvSchemaSetVersion\":11", StringComparison.Ordinal))
                throw new BuildFailedException("P15_CONTENT_MANIFEST_INVALID");
            foreach (string file in new[] { "offline_reward_rules.csv", "tutorial_steps.csv", "tutorial_grants.csv", "runtime_config.csv" })
                if (!File.Exists(Path.Combine(directory, file))) throw new BuildFailedException("P15_CONTENT_TABLE_MISSING: " + file);
        }

        private static void GenerateScreen()
        {
            EnsureFolder(Root); EnsureFolder(Root + "/Prefabs");
            var root = new GameObject("P15_OFFLINE_TUTORIAL_HUB", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(OfflineTutorialHubPresenter));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1920, 1080);
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 98;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            Image background = Panel("Background", root.transform, new Color32(8, 16, 20, 255)); Stretch(background.rectTransform);
            Image glow = Panel("BronzeGlow", background.transform, new Color32(56, 43, 27, 255)); Rect(glow.rectTransform, new Vector2(0, .91f), Vector2.one);
            Image safe = Panel("SafeArea", root.transform, new Color32(15, 26, 29, 250)); Rect(safe.rectTransform, new Vector2(.018f, .028f), new Vector2(.982f, .972f));
            Text("Eyebrow", safe.transform, "왕국 운영  ·  정식 출시 준비", 17, TextAlignmentOptions.Left, new Vector2(.025f, .938f), new Vector2(.65f, .988f), new Color32(121, 184, 170, 255));
            Text("P15_HEADER", safe.transform, "돌아온 영주를 위한 왕국 보고", 41, TextAlignmentOptions.Left, new Vector2(.025f, .86f), new Vector2(.74f, .945f), new Color32(224, 183, 104, 255));
            Text("Release", safe.transform, "콘텐츠 13  ·  저장 형식 11  ·  오프라인 8시간", 18, TextAlignmentOptions.Right, new Vector2(.55f, .875f), new Vector2(.935f, .94f), new Color32(145, 163, 159, 255));
            Button close = Button("P15_CLOSE", safe.transform, "×", new Color32(76, 58, 42, 255), 34); Rect(close.GetComponent<RectTransform>(), new Vector2(.94f, .885f), new Vector2(.985f, .972f));

            Image offline = Panel("OfflineCard", safe.transform, new Color32(27, 41, 43, 255)); Rect(offline.rectTransform, new Vector2(.025f, .10f), new Vector2(.40f, .84f));
            Text("OfflineTitle", offline.transform, "오프라인 왕국 정산", 28, TextAlignmentOptions.Left, new Vector2(.06f, .885f), new Vector2(.94f, .97f), new Color32(224, 183, 104, 255));
            TMP_Text status = Text("P15_STATUS", offline.transform, "<color=#79B8AA>정산 완료</color>  ·  1시간 00분 적용", 21, TextAlignmentOptions.Left, new Vector2(.06f, .78f), new Vector2(.94f, .87f));
            Image divider = Panel("OfflineDivider", offline.transform, new Color32(80, 101, 97, 150)); Rect(divider.rectTransform, new Vector2(.06f, .755f), new Vector2(.94f, .759f));
            TMP_Text rewards = Text("P15_REWARDS", offline.transform,
                "자동 사냥 수익                         +1,080\n\n회복 물약 사용                           -2\n\n시설 생산 진행                         +27,000회\n\n관리인 숙련도                           +9\n\n부상 회복                                 1명\n\n승급 심사 완료                           0명",
                20, TextAlignmentOptions.TopLeft, new Vector2(.06f, .25f), new Vector2(.94f, .735f), new Color32(225, 220, 207, 255));
            Text("OfflineRule", offline.transform, "최대 8시간 · 요약 정산 · 레이드 제외", 17, TextAlignmentOptions.Left, new Vector2(.06f, .08f), new Vector2(.94f, .19f), new Color32(139, 159, 154, 255));

            Image journey = Panel("JourneyCard", safe.transform, new Color32(24, 37, 40, 255)); Rect(journey.rectTransform, new Vector2(.415f, .10f), new Vector2(.70f, .84f));
            Text("JourneyTitle", journey.transform, "첫 25분 왕국 여정", 28, TextAlignmentOptions.Left, new Vector2(.07f, .885f), new Vector2(.93f, .97f), new Color32(224, 183, 104, 255));
            TMP_Text journeyText = Text("P15_JOURNEY", journey.transform,
                "왕국 여정  3 / 10\n\n✓  01  왕국 둘러보기\n✓  02  첫 용병 고용\n✓  03  사냥 지역 허가\n◆  04  자동 사냥 관찰\n·  05  귀환 후 전리품 판매\n·  06  시설과 무기 제작\n·  07  용병 구매와 장비 장착\n·  08  회복 물약 제작\n·  09  첫 승급 완료\n·  10  다음 지역 해금",
                20, TextAlignmentOptions.TopLeft, new Vector2(.07f, .08f), new Vector2(.93f, .85f));

            Image action = Panel("ActionCard", safe.transform, new Color32(29, 43, 43, 255)); Rect(action.rectTransform, new Vector2(.715f, .10f), new Vector2(.975f, .84f));
            Text("ActionTitle", action.transform, "영주의 다음 명령", 28, TextAlignmentOptions.Left, new Vector2(.07f, .885f), new Vector2(.93f, .97f), new Color32(224, 183, 104, 255));
            Image seal = Panel("Seal", action.transform, new Color32(41, 82, 74, 255)); Rect(seal.rectTransform, new Vector2(.12f, .57f), new Vector2(.88f, .82f));
            Text("SealGlyph", seal.transform, "왕국\n<size=18>영주의 길잡이</size>", 55, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Color32(238, 200, 112, 255));
            TMP_Text actionText = Text("P15_ACTION", action.transform, "<b>다음 목표</b>  자동 사냥 관찰\n왕국 외곽 초원으로 이동하세요.", 21, TextAlignmentOptions.TopLeft, new Vector2(.07f, .40f), new Vector2(.93f, .55f));
            Button advance = Button("P15_ADVANCE", action.transform, "해당 기능으로 이동", new Color32(49, 119, 98, 255), 22); Rect(advance.GetComponent<RectTransform>(), new Vector2(.07f, .27f), new Vector2(.93f, .38f));
            Button skip = Button("P15_SKIP", action.transform, "이 단계 건너뛰기", new Color32(64, 76, 73, 255), 19); Rect(skip.GetComponent<RectTransform>(), new Vector2(.07f, .15f), new Vector2(.93f, .25f));
            Button skipAll = Button("P15_SKIP_ALL", action.transform, "가이드 전체 종료", new Color32(69, 50, 44, 255), 18); Rect(skipAll.GetComponent<RectTransform>(), new Vector2(.07f, .035f), new Vector2(.93f, .135f));

            OfflineTutorialHubView view = safe.gameObject.AddComponent<OfflineTutorialHubView>();
            view.Configure(group, close, advance, skip, skipAll, status, rewards, journeyText, actionText);
            root.GetComponent<OfflineTutorialHubPresenter>().Configure(view);
            PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root);
            P15GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P15GeneratedAssetMarker>(MarkerPath);
            if (marker == null) { marker = ScriptableObject.CreateInstance<P15GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void IntegrateBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            CommonUiRoot common = Object.FindFirstObjectByType<CommonUiRoot>(FindObjectsInactive.Include) ?? throw new BuildFailedException("P15_COMMON_UI_ROOT_MISSING");
            Transform screens = common.transform.Find("ScreenCanvas") ?? throw new BuildFailedException("P15_SCREEN_CANVAS_MISSING");
            foreach (Transform old in screens.Cast<Transform>().Where(value => value.name == "P15_OFFLINE_TUTORIAL_HUB").ToArray()) Object.DestroyImmediate(old.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P15_UI_PREFAB_MISSING");
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screens); root.name = "P15_OFFLINE_TUTORIAL_HUB"; PrepareRoot(root);
            OfflineTutorialHubPresenter presenter = root.GetComponent<OfflineTutorialHubPresenter>(); root.SetActive(false);
            Transform hud = common.transform.Find("HudCanvas/SafeArea") ?? throw new BuildFailedException("P15_HUD_SAFE_AREA_MISSING");
            Transform existing = hud.Find("P15_JOURNEY_NAV_BUTTON");
            Button legacyButton;
            OfflineTutorialEntryButton entry;
            if (existing == null)
            {
                legacyButton = Button("P15_JOURNEY_NAV_BUTTON", hud, "왕국 보고", new Color32(55, 108, 94, 255), 21);
                entry = legacyButton.gameObject.AddComponent<OfflineTutorialEntryButton>();
                UnityEventTools.AddPersistentListener(legacyButton.onClick, entry.OpenHub);
            }
            else
            {
                legacyButton = existing.GetComponent<Button>();
                entry = existing.GetComponent<OfflineTutorialEntryButton>();
            }
            entry.Configure(presenter);
            legacyButton.gameObject.SetActive(false);
            IntegrateUnifiedNavigation(hud, presenter);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private static void IntegrateUnifiedNavigation(Transform hud, OfflineTutorialHubPresenter report)
        {
            Transform previous = hud.Find("P15_UNIFIED_NAVIGATION");
            if (previous != null)
            {
                UnifiedNavigationMenu oldMenu = previous.GetComponent<UnifiedNavigationMenu>();
                foreach (Button button in hud.GetComponentsInChildren<Button>(true)) RemovePersistentTarget(button, oldMenu);
                Object.DestroyImmediate(previous.gameObject);
            }

            string[] inactive = { "NAV_HUNT", "P10_GROWTH_NAV_BUTTON", "P11_PROGRESSION_NAV_BUTTON", "P14_RAID_NAV_BUTTON", "P15_JOURNEY_NAV_BUTTON" };
            foreach (string name in inactive)
            {
                Transform value = hud.Find(name);
                if (value != null) value.gameObject.SetActive(false);
            }

            string[] primary = { "NAV_KINGDOM", "NAV_MERCENARIES", "P09_CRAFT_NAV_BUTTON", "P12_REGION_MAP_NAV_BUTTON", "P13_RECRUITMENT_NAV_BUTTON" };
            string[] labels = { "왕국", "용병", "제작", "지역", "모집" };
            for (int index = 0; index < primary.Length; index++)
            {
                Transform item = hud.Find(primary[index]) ?? throw new BuildFailedException("P15_PRIMARY_NAV_MISSING: " + primary[index]);
                item.gameObject.SetActive(true); LayoutPrimary(item.GetComponent<RectTransform>(), index); SetButtonLabel(item, labels[index]);
            }

            var navigationRoot = new GameObject("P15_UNIFIED_NAVIGATION", typeof(RectTransform), typeof(UnifiedNavigationMenu));
            navigationRoot.transform.SetParent(hud, false); Stretch(navigationRoot.GetComponent<RectTransform>());
            UnifiedNavigationMenu menu = navigationRoot.GetComponent<UnifiedNavigationMenu>();
            Image panel = Panel("P15_MENU_PANEL", navigationRoot.transform, new Color32(20, 31, 33, 252)); Rect(panel.rectTransform, new Vector2(.67f, .13f), new Vector2(.98f, .66f));
            Text("P15_MENU_TITLE", panel.transform, "왕국 관리 메뉴", 28, TextAlignmentOptions.Left, new Vector2(.07f, .84f), new Vector2(.73f, .96f), new Color32(224, 183, 104, 255));
            Button close = Button("P15_MENU_CLOSE", panel.transform, "닫기", new Color32(74, 63, 52, 255), 19); Rect(close.GetComponent<RectTransform>(), new Vector2(.75f, .84f), new Vector2(.94f, .96f));
            Button inventory = Button("P15_MENU_INVENTORY", panel.transform, "창고", new Color32(55, 83, 104, 255), 20); Rect(inventory.GetComponent<RectTransform>(), new Vector2(.07f, .64f), new Vector2(.48f, .78f));
            Button store = Button("P15_MENU_STORE", panel.transform, "상점", new Color32(130, 91, 46, 255), 20); Rect(store.GetComponent<RectTransform>(), new Vector2(.52f, .64f), new Vector2(.93f, .78f));
            Button growth = Button("P15_MENU_GROWTH", panel.transform, "장비 공방", new Color32(50, 104, 93, 255), 20); Rect(growth.GetComponent<RectTransform>(), new Vector2(.07f, .47f), new Vector2(.48f, .61f));
            Button progression = Button("P15_MENU_PROGRESSION", panel.transform, "승급 심사", new Color32(137, 82, 37, 255), 20); Rect(progression.GetComponent<RectTransform>(), new Vector2(.52f, .47f), new Vector2(.93f, .61f));
            Button raid = Button("P15_MENU_RAID", panel.transform, "레이드", new Color32(124, 57, 48, 255), 20); Rect(raid.GetComponent<RectTransform>(), new Vector2(.07f, .30f), new Vector2(.48f, .44f));
            Button reportButton = Button("P15_MENU_REPORT", panel.transform, "왕국 보고", new Color32(55, 108, 94, 255), 20); Rect(reportButton.GetComponent<RectTransform>(), new Vector2(.52f, .30f), new Vector2(.93f, .44f));

            Button menuButton = Button("P15_MENU_NAV_BUTTON", navigationRoot.transform, "메뉴", new Color32(70, 57, 82, 255), 22); LayoutPrimary(menuButton.GetComponent<RectTransform>(), 5);
            MercenaryRosterPresenter mercenaries = Object.FindFirstObjectByType<MercenaryRosterPresenter>(FindObjectsInactive.Include);
            ProductionScreenPresenter production = Object.FindFirstObjectByType<ProductionScreenPresenter>(FindObjectsInactive.Include);
            EquipmentGrowthScreenPresenter equipmentGrowth = Object.FindFirstObjectByType<EquipmentGrowthScreenPresenter>(FindObjectsInactive.Include);
            ProgressionScreenPresenter progressionScreen = Object.FindFirstObjectByType<ProgressionScreenPresenter>(FindObjectsInactive.Include);
            RegionMapScreenPresenter regions = Object.FindFirstObjectByType<RegionMapScreenPresenter>(FindObjectsInactive.Include);
            RecruitmentScreenPresenter recruitment = Object.FindFirstObjectByType<RecruitmentScreenPresenter>(FindObjectsInactive.Include);
            RaidScreenPresenter raids = Object.FindFirstObjectByType<RaidScreenPresenter>(FindObjectsInactive.Include);
            if (new Object[] { mercenaries, production, equipmentGrowth, progressionScreen, regions, recruitment, raids, report }.Any(value => value == null))
                throw new BuildFailedException("P15_NAVIGATION_SCREEN_MISSING");
            GameObject[] primaryItems = primary.Select(name => hud.Find(name).gameObject).Append(menuButton.gameObject).ToArray();
            menu.Configure(panel.gameObject, primaryItems, mercenaries, production, equipmentGrowth, progressionScreen, regions, recruitment, raids, report);

            UnityEventTools.AddPersistentListener(menuButton.onClick, menu.ToggleMenu);
            UnityEventTools.AddPersistentListener(close.onClick, menu.CloseMenu);
            UnityEventTools.AddPersistentListener(inventory.onClick, menu.OpenInventory);
            UnityEventTools.AddPersistentListener(store.onClick, menu.OpenStore);
            UnityEventTools.AddPersistentListener(growth.onClick, menu.OpenEquipmentGrowth);
            UnityEventTools.AddPersistentListener(progression.onClick, menu.OpenProgression);
            UnityEventTools.AddPersistentListener(raid.onClick, menu.OpenRaids);
            UnityEventTools.AddPersistentListener(reportButton.onClick, menu.OpenReport);
            UnityEventTools.AddPersistentListener(hud.Find("NAV_KINGDOM").GetComponent<Button>().onClick, menu.CloseAll);
            UnityEventTools.AddPersistentListener(hud.Find("NAV_MERCENARIES").GetComponent<Button>().onClick, menu.PrepareMercenaries);
            UnityEventTools.AddPersistentListener(hud.Find("P09_CRAFT_NAV_BUTTON").GetComponent<Button>().onClick, menu.PrepareProduction);
            UnityEventTools.AddPersistentListener(hud.Find("P12_REGION_MAP_NAV_BUTTON").GetComponent<Button>().onClick, menu.PrepareRegions);
            UnityEventTools.AddPersistentListener(hud.Find("P13_RECRUITMENT_NAV_BUTTON").GetComponent<Button>().onClick, menu.PrepareRecruitment);
        }

        private static void LayoutPrimary(RectTransform rect, int index)
        {
            const float start = .02f;
            const float end = .98f;
            const float gap = .008f;
            float width = (end - start - gap * 5) / 6f;
            float min = start + index * (width + gap);
            rect.anchorMin = new Vector2(min, 0); rect.anchorMax = new Vector2(min + width, 0);
            rect.offsetMin = new Vector2(0, 16); rect.offsetMax = new Vector2(0, 96);
        }

        private static void SetButtonLabel(Transform button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
        }

        private static void RemovePersistentTarget(Button button, Object target)
        {
            if (button == null || target == null) return;
            for (int index = button.onClick.GetPersistentEventCount() - 1; index >= 0; index--)
                if (button.onClick.GetPersistentTarget(index) == target) UnityEventTools.RemovePersistentListener(button.onClick, index);
        }

        internal static void PrepareRoot(GameObject root)
        {
            RectTransform rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920, 1080);
            CanvasGroup group = root.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1;
        }

        internal static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            root.SetActive(true); PrepareRoot(root); RectTransform rootRect = root.GetComponent<RectTransform>(); rootRect.sizeDelta = new Vector2(width, height); rootRect.position = Vector3.zero;
            Canvas canvas = root.GetComponent<Canvas>(); canvas.enabled = true;
            var cameraObject = new GameObject("P15CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(8, 16, 20, 255); camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100f; camera.aspect = width / (float)height; camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target; GL.Clear(true, true, camera.backgroundColor); camera.Render();
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false);
            string path = Path.Combine(directory, filename); File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            if (new FileInfo(path).Length <= 20000) throw new BuildFailedException("P15_CAPTURE_FAILED: " + filename);
        }

        private static Image Panel(string name, Transform parent, Color color) { var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = color; return image; }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color? color = null) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text text = go.GetComponent<TMP_Text>(); text.text = value; text.fontSize = size; text.alignment = alignment; text.color = color ?? new Color32(238, 231, 214, 255); text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; text.raycastTarget = false; Rect(text.rectTransform, min, max); return text; }
        private static Button Button(string name, Transform parent, string label, Color color, float size) { Image image = Panel(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; ColorBlock colors = button.colors; colors.highlightedColor = Color.Lerp(color, Color.white, .12f); colors.pressedColor = Color.Lerp(color, Color.black, .16f); colors.disabledColor = new Color(color.r * .55f, color.g * .55f, color.b * .55f, .72f); button.colors = colors; Text("Label", image.transform, label, size, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => Rect(value, Vector2.zero, Vector2.one);
        private static void Rect(RectTransform value, Vector2 min, Vector2 max) { value.anchorMin = min; value.anchorMax = max; value.offsetMin = value.offsetMax = Vector2.zero; }
        private static void EnsureFolder(string path) { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P15GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P15/Verify Generated Assets")]
        public static void Verify()
        {
            P15OfflineTutorialSetup.ValidateContent(); P15GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P15GeneratedAssetMarker>(P15OfflineTutorialSetup.MarkerPath);
            if (marker == null || marker.fingerprint != P15OfflineTutorialSetup.Fingerprint) throw new BuildFailedException("P15_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P15OfflineTutorialSetup.ScreenPath) ?? throw new BuildFailedException("P15_UI_PREFAB_MISSING");
            Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true); foreach (string id in P15OfflineTutorialSetup.RequiredIds) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P15_UI_ID_INVALID: " + id);
            prefab.SetActive(true); P15OfflineTutorialSetup.PrepareRoot(prefab); Canvas.ForceUpdateCanvases(); string[] invalid = prefab.GetComponentsInChildren<Button>(true).Where(value => value.GetComponent<RectTransform>().rect.width < 64 || value.GetComponent<RectTransform>().rect.height < 64).Select(value => value.name).ToArray(); prefab.SetActive(false);
            if (invalid.Length > 0) throw new BuildFailedException("P15_TOUCH_TARGET_INVALID: " + string.Join(",", invalid));
            Scene scene = EditorSceneManager.OpenScene(P15OfflineTutorialSetup.BootstrapScenePath, OpenSceneMode.Single); OfflineTutorialHubPresenter[] screens = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<OfflineTutorialHubPresenter>(true)).ToArray(); OfflineTutorialEntryButton[] entries = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<OfflineTutorialEntryButton>(true)).ToArray();
            if (screens.Length != 1 || entries.Length != 1) throw new BuildFailedException($"P15_SCENE_ENTRY_INVALID: screens={screens.Length}, entries={entries.Length}");
            Button button = entries[0].GetComponent<Button>(); if (button == null || button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != entries[0]) throw new BuildFailedException("P15_SCENE_ENTRY_BINDING_INVALID");
            Debug.Log("P15_ASSETS_VERIFIED");
        }
    }

    public static class P15CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P15/Generate Acceptance Captures")]
        public static void Run()
        {
            P15GeneratedAssetVerifier.Verify(); Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P15OfflineTutorialSetup.ScreenPath), scene); root.SetActive(true); P15OfflineTutorialSetup.PrepareRoot(root);
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true); string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P15")); Directory.CreateDirectory(output);
            P15OfflineTutorialSetup.Capture(root, output, "p15_01_new_install.png", 1920, 1080);
            nodes.Single(value => value.name == "P15_STATUS").GetComponent<TMP_Text>().text = "<color=#79B8AA>정산 완료</color>  ·  1시간 00분 적용"; P15OfflineTutorialSetup.Capture(root, output, "p15_02_offline_1h.png", 1920, 1080);
            nodes.Single(value => value.name == "P15_STATUS").GetComponent<TMP_Text>().text = "<color=#E0B768>8시간 한도 적용</color>  ·  8시간 00분"; nodes.Single(value => value.name == "P15_REWARDS").GetComponent<TMP_Text>().text = "자동 사냥 수익                         +8,640\n\n회복 물약 사용                           -16\n\n시설 생산 진행                         +216,000 tick\n\n관리인 숙련도                           +72\n\n부상 회복                                 2명\n\n승급 심사 완료                           1명"; P15OfflineTutorialSetup.Capture(root, output, "p15_03_offline_8h.png", 1920, 1080);
            nodes.Single(value => value.name == "P15_ACTION").GetComponent<TMP_Text>().text = "<b>다음 목표</b>  자동 사냥 관찰\n대상: REGION_R01"; P15OfflineTutorialSetup.Capture(root, output, "p15_04_tutorial_active.png", 1920, 1080);
            nodes.Single(value => value.name == "P15_JOURNEY").GetComponent<TMP_Text>().text = "왕국 여정  10 / 10\n\n✓  01  왕국 둘러보기\n✓  02  첫 용병 고용\n✓  03  사냥 지역 허가\n✓  04  자동 사냥 관찰\n✓  05  귀환 후 전리품 판매\n✓  06  시설과 무기 제작\n✓  07  용병 구매와 장비 장착\n✓  08  회복 물약 제작\n✓  09  첫 승급 완료\n✓  10  다음 지역 해금"; nodes.Single(value => value.name == "P15_ACTION").GetComponent<TMP_Text>().text = "<color=#79B8AA><b>왕국 운영 준비 완료</b></color>\n자동 사냥·시설·성장·레이드까지 자유롭게 운영할 수 있습니다."; P15OfflineTutorialSetup.Capture(root, output, "p15_05_tutorial_complete.png", 1920, 1080);
            nodes.Single(value => value.name == "P15_STATUS").GetComponent<TMP_Text>().text = "<color=#E98274>기기 시간 변경 감지</color>  ·  보상 미적용"; nodes.Single(value => value.name == "P15_REWARDS").GetComponent<TMP_Text>().text = "시간 역행 구간에서는 보상을 지급하지 않았습니다.\n온라인 상태에서 신뢰 시각을 다시 동기화합니다."; P15OfflineTutorialSetup.Capture(root, output, "p15_06_clock_rollback.png", 1920, 1080);
            P15OfflineTutorialSetup.Capture(root, output, "p15_07_20x9.png", 2400, 1080); Debug.Log($"P15_CAPTURES={output}; count=7");
        }
    }

    public static class P15AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P15/Build Android Development APK")]
        public static void Build()
        {
            P15GeneratedAssetVerifier.Verify(); string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P15-Development.apk")); Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P15_ANDROID_OUTPUT_INVALID"));
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P15 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}"); Debug.Log($"P15_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }
}
