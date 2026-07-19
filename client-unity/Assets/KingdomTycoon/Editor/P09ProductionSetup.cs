using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.Production;
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
    public static class P09ProductionSetup
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P09Production";
        public const string ScreenPath = GeneratedRoot + "/Prefabs/P09_PRODUCTION_SCREEN.prefab";
        public const string MarkerPath = GeneratedRoot + "/P09Production.marker.asset";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "p09-production-v1-content7-78-tables-three-facilities-six-targets";
        internal static readonly string[] RequiredIds =
        {
            "P09_PRODUCTION_SCREEN", "P09_HEADER", "P09_CLOSE", "P09_META", "P09_FACILITY_LIST", "P09_FAC_BLACKSMITH",
            "P09_FAC_ALCHEMY", "P09_FAC_INFIRMARY", "P09_QUEUE_PANEL", "P09_QUEUE_TEXT", "P09_TARGET_PANEL", "P09_TARGET_LIST",
            "P09_SELECTED_TARGET", "P09_TARGET_MINUS", "P09_TARGET_PLUS", "P09_AUTOMATE", "P09_ADVANCE_TICKS", "P09_STATE_PANEL", "P09_RESULT_TOAST"
        };

        [MenuItem("Kingdom Tycoon/P09/Run Complete Setup")]
        public static void Run()
        {
            ValidateContent(); GenerateScreen(); IntegrateBootstrapScene(); P09GeneratedAssetVerifier.Verify(); Debug.Log("P09 production setup completed.");
        }

        [MenuItem("Kingdom Tycoon/P09/Validate Content Package .7")]
        public static void ValidateContent()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.7"); string manifest = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifest)) throw new BuildFailedException("P09_CONTENT_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifest), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 78) throw new BuildFailedException("P09_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        public static void GenerateScreen()
        {
            EnsureFolder(GeneratedRoot + "/Prefabs"); AssetDatabase.DeleteAsset(ScreenPath);
            var root = new GameObject("P09_PRODUCTION_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(ProductionScreenPresenter));
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080)); CanvasGroup group = root.GetComponent<CanvasGroup>();
            Image backdrop = Panel("Backdrop", root.transform, new Color32(14, 12, 10, 248)); Stretch(backdrop.rectTransform);
            var viewObject = new GameObject("ProductionView", typeof(RectTransform), typeof(ProductionScreenView)); viewObject.transform.SetParent(backdrop.transform, false); Stretch(viewObject.GetComponent<RectTransform>());
            Image safe = Panel("SafeArea", viewObject.transform, new Color32(31, 28, 24, 255)); SetRect(safe.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 32), new Vector2(-48, -32));

            Image header = Panel("P09_HEADER", safe.transform, new Color32(66, 43, 29, 255)); SetRect(header.rectTransform, new Vector2(0, .86f), Vector2.one, Vector2.zero, Vector2.zero);
            Text("Title", header.transform, "왕국 생산", 44, TextAlignmentOptions.Left, new Vector2(.025f, .12f), new Vector2(.32f, .9f), new Color32(242, 212, 122, 255));
            Text("Subtitle", header.transform, "재료 → 제작 → 상점 입고 · 관리인 숙련 성장", 23, TextAlignmentOptions.Left, new Vector2(.22f, .15f), new Vector2(.65f, .84f), new Color32(202, 192, 177, 255));
            TMP_Text meta = Text("P09_META", header.transform, "콘텐츠 7 · 생산 진행 0 · 저장 0", 23, TextAlignmentOptions.Right, new Vector2(.63f, .15f), new Vector2(.88f, .84f));
            Button close = Button("P09_CLOSE", header.transform, "닫기", new Color32(86, 70, 57, 255)); SetRect(close.GetComponent<RectTransform>(), new Vector2(.89f, .14f), new Vector2(.98f, .86f), Vector2.zero, Vector2.zero);

            Image facilities = Panel("P09_FACILITY_LIST", safe.transform, new Color32(40, 36, 31, 255)); SetRect(facilities.rectTransform, new Vector2(0, .13f), new Vector2(.32f, .85f), Vector2.zero, Vector2.zero);
            Text("FacilityHeading", facilities.transform, "시설과 담당 관리인", 28, TextAlignmentOptions.Left, new Vector2(.05f, .9f), new Vector2(.95f, .99f), new Color32(242, 212, 122, 255));
            var facilityTexts = new TMP_Text[3]; string[] facilityIds = { "P09_FAC_BLACKSMITH", "P09_FAC_ALCHEMY", "P09_FAC_INFIRMARY" }; string[] facilityNames = { "대장간", "연금 공방", "진료소" };
            for (int index = 0; index < 3; index++)
            {
                float top = .88f - index * .285f; Image card = Panel(facilityIds[index], facilities.transform, new Color32(55, 50, 43, 255)); SetRect(card.rectTransform, new Vector2(.04f, top - .255f), new Vector2(.96f, top), Vector2.zero, Vector2.zero);
                facilityTexts[index] = Text("Status", card.transform, $"<size=30><color=#F2D47A>{facilityNames[index]}</color></size>  1레벨\n담당 관리인 없음\n대기열 0/4 · 남은 진행 0\n<color=#E7A74C>시설을 먼저 건설하세요</color>", 21, TextAlignmentOptions.TopLeft, new Vector2(.045f, .06f), new Vector2(.96f, .94f));
            }

            Image queuePanel = Panel("P09_QUEUE_PANEL", safe.transform, new Color32(47, 43, 37, 255)); SetRect(queuePanel.rectTransform, new Vector2(.33f, .31f), new Vector2(.66f, .85f), Vector2.zero, Vector2.zero);
            Text("QueueHeading", queuePanel.transform, "생산 대기열", 30, TextAlignmentOptions.Left, new Vector2(.06f, .86f), new Vector2(.94f, .97f), new Color32(242, 212, 122, 255));
            TMP_Text queueText = Text("P09_QUEUE_TEXT", queuePanel.transform, "가동 시설 0/3\n최근 이벤트 0\n\n자동 보충은 목표와 큐 출력을 함께 계산합니다.\n생산 완료품은 왕국 상점에 즉시 입고됩니다.", 24, TextAlignmentOptions.TopLeft, new Vector2(.06f, .13f), new Vector2(.94f, .84f));

            Image targetPanel = Panel("P09_TARGET_PANEL", safe.transform, new Color32(48, 43, 36, 255)); SetRect(targetPanel.rectTransform, new Vector2(.67f, .13f), new Vector2(1f, .85f), Vector2.zero, Vector2.zero);
            Text("TargetHeading", targetPanel.transform, "상점 재고 목표", 30, TextAlignmentOptions.Left, new Vector2(.055f, .89f), new Vector2(.94f, .98f), new Color32(242, 212, 122, 255));
            TMP_Text targetList = Text("P09_TARGET_LIST", targetPanel.transform, "▶ 소형 회복 물약  0+0/8 자동\n  전사 무기       0+0/1 자동\n  수호자 무기     0+0/1 자동\n  궁수 무기       0+0/1 자동\n  마법사 무기     0+0/1 자동\n  성직자 무기     0+0/1 자동", 22, TextAlignmentOptions.TopLeft, new Vector2(.055f, .47f), new Vector2(.95f, .88f));
            var targetButtons = new Button[6];
            for (int index = 0; index < 6; index++)
            {
                targetButtons[index] = Button("TargetSelect_" + index, targetPanel.transform, string.Empty, new Color32(255, 255, 255, 1)); float top = .88f - index * .071f;
                SetRect(targetButtons[index].GetComponent<RectTransform>(), new Vector2(.04f, top - .09f), new Vector2(.96f, top), Vector2.zero, Vector2.zero);
            }
            Image selected = Panel("P09_SELECTED_TARGET", targetPanel.transform, new Color32(61, 54, 44, 255)); SetRect(selected.rectTransform, new Vector2(.05f, .2f), new Vector2(.95f, .45f), Vector2.zero, Vector2.zero);
            TMP_Text selectedText = Text("SelectedText", selected.transform, "선택: 소형 회복 물약\n현재 0 · 대기 0 · 목표 <color=#F2D47A>8</color>\n생산 가능", 23, TextAlignmentOptions.TopLeft, new Vector2(.05f, .3f), new Vector2(.95f, .94f));
            Button minus = Button("P09_TARGET_MINUS", selected.transform, "−", new Color32(83, 74, 63, 255)); SetRect(minus.GetComponent<RectTransform>(), new Vector2(.05f, .04f), new Vector2(.31f, .42f), Vector2.zero, Vector2.zero);
            Button plus = Button("P09_TARGET_PLUS", selected.transform, "+", new Color32(83, 74, 63, 255)); SetRect(plus.GetComponent<RectTransform>(), new Vector2(.69f, .04f), new Vector2(.95f, .42f), Vector2.zero, Vector2.zero);

            Image actions = Panel("P09_ACTION_BAR", safe.transform, new Color32(52, 44, 35, 255)); SetRect(actions.rectTransform, new Vector2(.33f, .13f), new Vector2(.66f, .3f), Vector2.zero, Vector2.zero);
            Button automate = Button("P09_AUTOMATE", actions.transform, "자동 보충 1회", new Color32(172, 109, 47, 255)); SetRect(automate.GetComponent<RectTransform>(), new Vector2(.04f, .52f), new Vector2(.96f, .94f), Vector2.zero, Vector2.zero);
            Button advance = Button("P09_ADVANCE_TICKS", actions.transform, "생산 10회 진행", new Color32(80, 106, 75, 255)); SetRect(advance.GetComponent<RectTransform>(), new Vector2(.04f, .06f), new Vector2(.96f, .48f), Vector2.zero, Vector2.zero);

            Image hint = Panel("P09_HINT", safe.transform, new Color32(42, 38, 33, 255)); SetRect(hint.rectTransform, new Vector2(0, 0), new Vector2(1, .12f), Vector2.zero, Vector2.zero);
            Text("HintText", hint.transform, "중단 사유를 확인하고 시설 건설 → 관리인 배치 → 재료 확보 순으로 해결하세요. 강화·제련·분해는 장비 공방에서 할 수 있습니다.", 22, TextAlignmentOptions.Center, new Vector2(.03f, .1f), new Vector2(.97f, .9f), new Color32(202, 192, 177, 255));

            Image state = Panel("P09_STATE_PANEL", viewObject.transform, new Color32(45, 39, 33, 252)); SetRect(state.rectTransform, new Vector2(.27f, .28f), new Vector2(.73f, .7f), Vector2.zero, Vector2.zero);
            TMP_Text stateTitle = Text("StateTitle", state.transform, "생산 현황을 불러오는 중", 37, TextAlignmentOptions.Center, new Vector2(.07f, .65f), new Vector2(.93f, .88f), new Color32(242, 212, 122, 255));
            TMP_Text stateBody = Text("StateBody", state.transform, "대기열과 상점 재고를 검증하고 있습니다.", 25, TextAlignmentOptions.Center, new Vector2(.08f, .2f), new Vector2(.92f, .62f));
            Image toast = Panel("P09_RESULT_TOAST", viewObject.transform, new Color32(47, 91, 61, 252)); SetRect(toast.rectTransform, new Vector2(.34f, .04f), new Vector2(.66f, .13f), Vector2.zero, Vector2.zero);
            TMP_Text toastText = Text("ToastText", toast.transform, "생산을 진행했습니다.", 24, TextAlignmentOptions.Center, new Vector2(.04f, .08f), new Vector2(.96f, .92f)); toast.gameObject.SetActive(false);

            ProductionScreenView view = viewObject.GetComponent<ProductionScreenView>(); view.Configure(group, meta, facilityTexts, queueText, targetList, selectedText, state.gameObject, stateTitle, stateBody, toast.gameObject, toastText, close, automate, advance, minus, plus, targetButtons);
            root.GetComponent<ProductionScreenPresenter>().Configure(view); PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root);
            P09GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P09GeneratedAssetMarker>(MarkerPath); if (marker == null) { marker = ScriptableObject.CreateInstance<P09GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void IntegrateBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single); CommonUiRoot common = Object.FindFirstObjectByType<CommonUiRoot>(FindObjectsInactive.Include) ?? throw new BuildFailedException("P09_COMMON_UI_ROOT_MISSING");
            Transform screenCanvas = common.transform.Find("ScreenCanvas") ?? throw new BuildFailedException("P09_SCREEN_CANVAS_MISSING");
            foreach (Transform old in screenCanvas.Cast<Transform>().Where(value => value.name == "P09_PRODUCTION_SCREEN").ToArray()) Object.DestroyImmediate(old.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P09_UI_PREFAB_MISSING"); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screenCanvas); root.name = "P09_PRODUCTION_SCREEN"; PrepareRoot(root);
            ProductionScreenPresenter presenter = root.GetComponent<ProductionScreenPresenter>(); root.SetActive(false);
            Transform hud = common.transform.Find("HudCanvas/SafeArea") ?? throw new BuildFailedException("P09_HUD_SAFE_AREA_MISSING"); Transform existing = hud.Find("P09_CRAFT_NAV_BUTTON"); if (existing != null) Object.DestroyImmediate(existing.gameObject);
            Button craft = Button("P09_CRAFT_NAV_BUTTON", hud, "제작", new Color32(123, 78, 42, 255)); SetRect(craft.GetComponent<RectTransform>(), new Vector2(.76f, 0), new Vector2(.91f, 0), new Vector2(0, 16), new Vector2(0, 96));
            ProductionEntryButton entry = craft.gameObject.AddComponent<ProductionEntryButton>(); entry.Configure(presenter, craft); UnityEventTools.AddPersistentListener(craft.onClick, entry.OpenProduction);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        internal static void PrepareRoot(GameObject root)
        {
            RectTransform rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920, 1080);
            CanvasGroup group = root.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1;
        }
        private static Image Panel(string name, Transform parent, Color color) { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); Image image = value.GetComponent<Image>(); image.color = color; return image; }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color? color = null)
        { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text label = go.GetComponent<TMP_Text>(); label.text = value; label.fontSize = size; label.alignment = alignment; label.color = color ?? new Color32(238, 231, 214, 255); label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string text, Color color) { Image image = Panel(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; Text("Label", image.transform, text, 24, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offsetMin; value.offsetMax = offsetMax; }
        private static void EnsureFolder(string path) { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P09GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P09/Verify Generated Assets")]
        public static void Verify()
        {
            P09ProductionSetup.ValidateContent(); P09GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P09GeneratedAssetMarker>(P09ProductionSetup.MarkerPath);
            if (marker == null || marker.fingerprint != P09ProductionSetup.Fingerprint) throw new BuildFailedException("P09_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P09ProductionSetup.ScreenPath) ?? throw new BuildFailedException("P09_UI_PREFAB_MISSING"); Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true);
            foreach (string id in P09ProductionSetup.RequiredIds) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P09_UI_ID_INVALID: " + id);
            prefab.SetActive(true); P09ProductionSetup.PrepareRoot(prefab); Canvas.ForceUpdateCanvases(); string[] invalid = prefab.GetComponentsInChildren<Button>(true).Where(value => value.GetComponent<RectTransform>().rect.width < 64 || value.GetComponent<RectTransform>().rect.height < 64).Select(value => value.name).ToArray(); prefab.SetActive(false);
            if (invalid.Length > 0) throw new BuildFailedException("P09_TOUCH_TARGET_INVALID: " + string.Join(",", invalid));
            Scene scene = EditorSceneManager.OpenScene(P09ProductionSetup.BootstrapScenePath, OpenSceneMode.Single);
            ProductionScreenPresenter[] screens = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<ProductionScreenPresenter>(true)).ToArray();
            ProductionEntryButton[] entries = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<ProductionEntryButton>(true)).ToArray();
            if (screens.Length != 1 || entries.Length != 1) throw new BuildFailedException($"P09_SCENE_ENTRY_INVALID: screens={screens.Length}, entries={entries.Length}");
            ProductionEntryButton entry = Object.FindFirstObjectByType<ProductionEntryButton>(FindObjectsInactive.Include); Button button = entry.GetComponent<Button>();
            bool hasEntryBinding = button != null && Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index => button.onClick.GetPersistentTarget(index) == entry && button.onClick.GetPersistentMethodName(index) == nameof(ProductionEntryButton.OpenProduction));
            if (!hasEntryBinding) throw new BuildFailedException("P09_SCENE_ENTRY_BINDING_INVALID");
        }
    }

    public static class P09CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P09/Generate Acceptance Captures")]
        public static void Run()
        {
            P09GeneratedAssetVerifier.Verify(); Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P09ProductionSetup.ScreenPath); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); root.SetActive(true); P09ProductionSetup.PrepareRoot(root);
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P09")); Directory.CreateDirectory(output); Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            nodes.Single(value => value.name == "P09_STATE_PANEL").gameObject.SetActive(false); Capture(root, output, "p09_01_overview.png", 1920, 1080); nodes.Single(value => value.name == "P09_STATE_PANEL").gameObject.SetActive(true); nodes.Single(value => value.name == "StateTitle").GetComponent<TMP_Text>().text = "시설이 아직 잠겨 있습니다"; nodes.Single(value => value.name == "StateBody").GetComponent<TMP_Text>().text = "대장간이나 연금 공방을 건설하고\n담당 NPC를 배치하세요.\n\nP09_FACILITY_LOCKED"; Capture(root, output, "p09_02_locked.png", 1920, 1080);
            nodes.Single(value => value.name == "P09_STATE_PANEL").gameObject.SetActive(false); TMP_Text queue = nodes.Single(value => value.name == "P09_QUEUE_TEXT").GetComponent<TMP_Text>(); queue.text = "대장간 작업 중\n\n전사 무기  14 / 20 tick\n██████████░░░░ 70%\n\n예약 재료\n부드러운 목재 4 · 늑대 송곳니 2"; Capture(root, output, "p09_03_running.png", 1920, 1080);
            TMP_Text selected = nodes.Single(value => value.name == "SelectedText").GetComponent<TMP_Text>(); selected.text = "선택: 소형 회복 물약\n현재 2 · 대기 4 · 목표 <color=#F2D47A>8</color>\n<color=#E7A74C>야생 약초가 부족합니다</color>"; Capture(root, output, "p09_04_material_stop.png", 1920, 1080);
            nodes.Single(value => value.name == "P09_RESULT_TOAST").gameObject.SetActive(true); nodes.Single(value => value.name == "ToastText").GetComponent<TMP_Text>().text = "생산 완료 · 소형 회복 물약 +1 · 상점 입고"; Capture(root, output, "p09_05_complete.png", 1920, 1080); nodes.Single(value => value.name == "P09_RESULT_TOAST").gameObject.SetActive(false);
            Capture(root, output, "p09_06_20x9.png", 2400, 1080); Debug.Log($"P09_CAPTURES={output}; count=6");
        }
        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            root.SetActive(true); P09ProductionSetup.PrepareRoot(root); Canvas canvas = root.GetComponent<Canvas>(); canvas.enabled = true; var cameraObject = new GameObject("P09CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(12, 10, 9, 255); camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100f; camera.enabled = true;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false); string path = Path.Combine(directory, filename); File.WriteAllBytes(path, texture.EncodeToPNG()); RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            if (new FileInfo(path).Length <= 1024) throw new BuildFailedException("P09_CAPTURE_FAILED: " + filename);
        }
    }

    public static class P09AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P09/Build Android Development APK")]
        public static void Build()
        {
            P09GeneratedAssetVerifier.Verify(); string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P09-Development.apk")); Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P09 Android output invalid."));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P09 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}"); Debug.Log($"P09_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    internal sealed class P09ProductionBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 90;
        public void OnPreprocessBuild(BuildReport report) => P09GeneratedAssetVerifier.Verify();
    }
}
