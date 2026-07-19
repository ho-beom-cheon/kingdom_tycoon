using System;
using System.IO;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.EquipmentGrowth;
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
    public static class P10EquipmentGrowthSetup
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P10EquipmentGrowth";
        public const string ScreenPath = GeneratedRoot + "/Prefabs/P10_EQUIPMENT_GROWTH_SCREEN.prefab";
        public const string MarkerPath = GeneratedRoot + "/P10EquipmentGrowth.marker.asset";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "p10-growth-v1-content8-80-tables-mobile-safe-atomic-growth";
        internal static readonly string[] RequiredIds =
        {
            "P10_EQUIPMENT_GROWTH_SCREEN", "P10_HEADER", "P10_CLOSE", "P10_META", "P10_FACILITY_STATUS", "P10_ITEM_CARD", "P10_ITEM_TITLE",
            "P10_ITEM_STATS", "P10_PREVIOUS", "P10_NEXT", "P10_ENHANCE", "P10_REFINE_PANEL", "P10_REFINE_PREVIOUS", "P10_REFINE_NEXT",
            "P10_ROLL_REFINE", "P10_ACCEPT", "P10_KEEP", "P10_DISMANTLE", "P10_RESULT_PANEL", "P10_EMPTY_STATE", "P10_RESULT_TOAST"
        };

        [MenuItem("Kingdom Tycoon/P10/Run Complete Setup")]
        public static void Run() { ValidateContent(); GenerateScreen(); IntegrateBootstrapScene(); P10GeneratedAssetVerifier.Verify(); Debug.Log("P10 equipment growth setup completed."); }

        [MenuItem("Kingdom Tycoon/P10/Validate Content Package .8")]
        public static void ValidateContent()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.8"); string manifest = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifest)) throw new BuildFailedException("P10_CONTENT_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifest), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 80) throw new BuildFailedException("P10_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        public static void GenerateScreen()
        {
            EnsureFolder(GeneratedRoot + "/Prefabs"); AssetDatabase.DeleteAsset(ScreenPath);
            var root = new GameObject("P10_EQUIPMENT_GROWTH_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(EquipmentGrowthScreenPresenter));
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 95;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080)); CanvasGroup group = root.GetComponent<CanvasGroup>();
            Image backdrop = Panel("Backdrop", root.transform, new Color32(10, 13, 16, 250)); Stretch(backdrop.rectTransform);
            Image safe = Panel("SafeArea", backdrop.transform, new Color32(24, 28, 31, 255)); SetRect(safe.rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 28), new Vector2(-44, -28));

            Image header = Panel("P10_HEADER", safe.transform, new Color32(54, 39, 27, 255)); SetRect(header.rectTransform, new Vector2(0, .865f), Vector2.one, Vector2.zero, Vector2.zero);
            Text("Title", header.transform, "대장간 · 장비 공방", 43, TextAlignmentOptions.Left, new Vector2(.025f, .17f), new Vector2(.38f, .88f), new Color32(245, 207, 111, 255));
            Text("Subtitle", header.transform, "강화 · 재련 · 안전 분해", 22, TextAlignmentOptions.Left, new Vector2(.31f, .21f), new Vector2(.58f, .82f), new Color32(201, 190, 170, 255));
            TMP_Text meta = Text("P10_META", header.transform, "콘텐츠 8  ·  저장 0  ·  개인 골드 500", 22, TextAlignmentOptions.Right, new Vector2(.58f, .17f), new Vector2(.88f, .84f));
            Button close = Button("P10_CLOSE", header.transform, "닫기", new Color32(83, 69, 57, 255), 23); SetRect(close.GetComponent<RectTransform>(), new Vector2(.89f, .14f), new Vector2(.98f, .86f), Vector2.zero, Vector2.zero);

            TMP_Text facility = Text("P10_FACILITY_STATUS", safe.transform, "대장간 3레벨  ·  <color=#8ED6A3>가동 중</color>", 23, TextAlignmentOptions.Left, new Vector2(.018f, .80f), new Vector2(.43f, .855f), new Color32(213, 203, 184, 255));
            Image itemCard = Panel("P10_ITEM_CARD", safe.transform, new Color32(38, 43, 46, 255)); SetRect(itemCard.rectTransform, new Vector2(0, .14f), new Vector2(.42f, .79f), Vector2.zero, Vector2.zero);
            Image itemAccent = Panel("ItemAccent", itemCard.transform, new Color32(197, 145, 56, 255)); SetRect(itemAccent.rectTransform, new Vector2(0, 0), new Vector2(.012f, 1), Vector2.zero, Vector2.zero);
            Text("CardEyebrow", itemCard.transform, "선택한 장비", 18, TextAlignmentOptions.Left, new Vector2(.055f, .86f), new Vector2(.94f, .95f), new Color32(113, 210, 190, 255));
            TMP_Text itemTitle = Text("P10_ITEM_TITLE", itemCard.transform, "EQ_T3_WARRIOR_WEAPON  <color=#F1C96A>+6</color>", 32, TextAlignmentOptions.Left, new Vector2(.055f, .68f), new Vector2(.95f, .87f));
            TMP_Text itemStats = Text("P10_ITEM_STATS", itemCard.transform, "3단계  ·  희귀\n전투력 <size=42><color=#F4E5B2>472</color></size>\n강화 천장 12%  ·  1/4", 25, TextAlignmentOptions.TopLeft, new Vector2(.055f, .30f), new Vector2(.95f, .67f));
            Button previous = Button("P10_PREVIOUS", itemCard.transform, "‹ 이전", new Color32(66, 72, 73, 255), 23); SetRect(previous.GetComponent<RectTransform>(), new Vector2(.055f, .16f), new Vector2(.46f, .29f), Vector2.zero, Vector2.zero);
            Button next = Button("P10_NEXT", itemCard.transform, "다음 ›", new Color32(66, 72, 73, 255), 23); SetRect(next.GetComponent<RectTransform>(), new Vector2(.54f, .16f), new Vector2(.945f, .29f), Vector2.zero, Vector2.zero);
            Button enhance = Button("P10_ENHANCE", itemCard.transform, "강화 시도", new Color32(184, 112, 45, 255), 27); SetRect(enhance.GetComponent<RectTransform>(), new Vector2(.055f, .035f), new Vector2(.945f, .145f), Vector2.zero, Vector2.zero);

            Image refinePanel = Panel("P10_REFINE_PANEL", safe.transform, new Color32(35, 48, 49, 255)); SetRect(refinePanel.rectTransform, new Vector2(.43f, .39f), new Vector2(.75f, .79f), Vector2.zero, Vector2.zero);
            Text("RefineHeading", refinePanel.transform, "재련 후보", 28, TextAlignmentOptions.Left, new Vector2(.06f, .84f), new Vector2(.94f, .96f), new Color32(116, 220, 197, 255));
            TMP_Text refine = Text("RefineText", refinePanel.transform, "현재 옵션  공격력 +8.2%\n새 후보  <color=#74D8C5>치명타 +5.4%</color>\n\n선택 옵션  <color=#F1C96A>공격력</color>", 23, TextAlignmentOptions.TopLeft, new Vector2(.06f, .38f), new Vector2(.94f, .82f));
            Button refinePrevious = Button("P10_REFINE_PREVIOUS", refinePanel.transform, "‹", new Color32(63, 80, 80, 255), 26); SetRect(refinePrevious.GetComponent<RectTransform>(), new Vector2(.06f, .25f), new Vector2(.25f, .42f), Vector2.zero, Vector2.zero);
            Button roll = Button("P10_ROLL_REFINE", refinePanel.transform, "후보 생성", new Color32(44, 131, 117, 255), 23); SetRect(roll.GetComponent<RectTransform>(), new Vector2(.27f, .25f), new Vector2(.73f, .42f), Vector2.zero, Vector2.zero);
            Button refineNext = Button("P10_REFINE_NEXT", refinePanel.transform, "›", new Color32(63, 80, 80, 255), 26); SetRect(refineNext.GetComponent<RectTransform>(), new Vector2(.75f, .25f), new Vector2(.94f, .42f), Vector2.zero, Vector2.zero);
            Button accept = Button("P10_ACCEPT", refinePanel.transform, "새 옵션 적용", new Color32(51, 135, 98, 255), 21); SetRect(accept.GetComponent<RectTransform>(), new Vector2(.06f, .045f), new Vector2(.48f, .22f), Vector2.zero, Vector2.zero);
            Button keep = Button("P10_KEEP", refinePanel.transform, "기존 옵션 유지", new Color32(91, 83, 70, 255), 21); SetRect(keep.GetComponent<RectTransform>(), new Vector2(.52f, .045f), new Vector2(.94f, .22f), Vector2.zero, Vector2.zero);

            Image resultPanel = Panel("P10_RESULT_PANEL", safe.transform, new Color32(44, 40, 36, 255)); SetRect(resultPanel.rectTransform, new Vector2(.76f, .39f), new Vector2(1, .79f), Vector2.zero, Vector2.zero);
            Text("ResultHeading", resultPanel.transform, "작업 기록", 28, TextAlignmentOptions.Left, new Vector2(.075f, .84f), new Vector2(.93f, .96f), new Color32(245, 207, 111, 255));
            TMP_Text result = Text("ResultText", resultPanel.transform, "최근 결과\n<color=#DCCCA9>강화 실패 · 천장 누적</color>\n\n실패해도 장비가 파괴되거나\n강화 단계가 내려가지 않습니다.", 22, TextAlignmentOptions.TopLeft, new Vector2(.075f, .26f), new Vector2(.93f, .82f));
            Button dismantle = Button("P10_DISMANTLE", resultPanel.transform, "분해 · 70% 재료 회수", new Color32(118, 59, 50, 255), 21); SetRect(dismantle.GetComponent<RectTransform>(), new Vector2(.075f, .06f), new Vector2(.925f, .22f), Vector2.zero, Vector2.zero);

            Image guide = Panel("Guide", safe.transform, new Color32(31, 35, 38, 255)); SetRect(guide.rectTransform, new Vector2(.43f, .14f), new Vector2(1, .375f), Vector2.zero, Vector2.zero);
            Text("GuideTitle", guide.transform, "성장 안전 계약", 25, TextAlignmentOptions.Left, new Vector2(.04f, .70f), new Vector2(.95f, .92f), new Color32(245, 207, 111, 255));
            Text("GuideBody", guide.transform, "+1 ~ +5 확정 강화  ·  +6부터 확률 및 천장 적용\n재련은 새 후보를 확인한 뒤 적용  ·  잠금/착용 장비는 보호\n모든 소비와 결과는 한 번에 저장되며 재시도해도 중복 차감되지 않습니다.", 21, TextAlignmentOptions.TopLeft, new Vector2(.04f, .12f), new Vector2(.96f, .69f), new Color32(202, 194, 179, 255));

            Image state = Panel("P10_EMPTY_STATE", safe.transform, new Color32(34, 39, 42, 252)); SetRect(state.rectTransform, new Vector2(.22f, .28f), new Vector2(.78f, .72f), Vector2.zero, Vector2.zero);
            TMP_Text stateTitle = Text("EmptyTitle", state.transform, "장비 공방을 준비하는 중", 36, TextAlignmentOptions.Center, new Vector2(.08f, .62f), new Vector2(.92f, .86f), new Color32(245, 207, 111, 255));
            TMP_Text stateBody = Text("EmptyBody", state.transform, "장비·재료·대장간 상태를 확인하고 있습니다.", 24, TextAlignmentOptions.Center, new Vector2(.08f, .22f), new Vector2(.92f, .61f));
            Image toast = Panel("P10_RESULT_TOAST", safe.transform, new Color32(41, 100, 77, 252)); SetRect(toast.rectTransform, new Vector2(.31f, .025f), new Vector2(.69f, .115f), Vector2.zero, Vector2.zero);
            TMP_Text toastText = Text("ToastText", toast.transform, "강화 결과를 저장했습니다.", 23, TextAlignmentOptions.Center, new Vector2(.04f, .08f), new Vector2(.96f, .92f)); toast.gameObject.SetActive(false);

            EquipmentGrowthScreenView view = safe.gameObject.AddComponent<EquipmentGrowthScreenView>(); view.Configure(group, meta, facility, itemTitle, itemStats, refine, result, state.gameObject, stateTitle, stateBody, toast.gameObject, toastText,
                close, previous, next, enhance, refinePrevious, refineNext, roll, accept, keep, dismantle);
            root.GetComponent<EquipmentGrowthScreenPresenter>().Configure(view); PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root);
            P10GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P10GeneratedAssetMarker>(MarkerPath); if (marker == null) { marker = ScriptableObject.CreateInstance<P10GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void IntegrateBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single); CommonUiRoot common = Object.FindFirstObjectByType<CommonUiRoot>(FindObjectsInactive.Include) ?? throw new BuildFailedException("P10_COMMON_UI_ROOT_MISSING");
            Transform screenCanvas = common.transform.Find("ScreenCanvas") ?? throw new BuildFailedException("P10_SCREEN_CANVAS_MISSING"); foreach (Transform old in screenCanvas.Cast<Transform>().Where(value => value.name == "P10_EQUIPMENT_GROWTH_SCREEN").ToArray()) Object.DestroyImmediate(old.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P10_UI_PREFAB_MISSING"); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screenCanvas); root.name = "P10_EQUIPMENT_GROWTH_SCREEN"; PrepareRoot(root);
            EquipmentGrowthScreenPresenter presenter = root.GetComponent<EquipmentGrowthScreenPresenter>(); root.SetActive(false);
            Transform hud = common.transform.Find("HudCanvas/SafeArea") ?? throw new BuildFailedException("P10_HUD_SAFE_AREA_MISSING"); Transform existing = hud.Find("P10_GROWTH_NAV_BUTTON"); if (existing != null) Object.DestroyImmediate(existing.gameObject);
            Button button = Button("P10_GROWTH_NAV_BUTTON", hud, "공방", new Color32(52, 113, 101, 255), 23); SetRect(button.GetComponent<RectTransform>(), new Vector2(.59f, 0), new Vector2(.74f, 0), new Vector2(0, 16), new Vector2(0, 96));
            EquipmentGrowthEntryButton entry = button.gameObject.AddComponent<EquipmentGrowthEntryButton>(); entry.Configure(presenter, button); UnityEventTools.AddPersistentListener(button.onClick, entry.OpenEquipmentGrowth);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        internal static void PrepareRoot(GameObject root) { RectTransform rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920, 1080); CanvasGroup group = root.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1; }
        private static Image Panel(string name, Transform parent, Color color) { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); Image image = value.GetComponent<Image>(); image.color = color; return image; }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color? color = null) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text label = go.GetComponent<TMP_Text>(); label.text = value; label.fontSize = size; label.alignment = alignment; label.color = color ?? new Color32(238, 231, 214, 255); label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string label, Color color, float size) { Image image = Panel(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; ColorBlock colors = button.colors; colors.highlightedColor = Color.Lerp(color, Color.white, .12f); colors.pressedColor = Color.Lerp(color, Color.black, .16f); colors.disabledColor = new Color(color.r * .55f, color.g * .55f, color.b * .55f, .7f); button.colors = colors; Text("Label", image.transform, label, size, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offsetMin; value.offsetMax = offsetMax; }
        private static void EnsureFolder(string path) { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P10GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P10/Verify Generated Assets")]
        public static void Verify()
        {
            P10EquipmentGrowthSetup.ValidateContent(); P10GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P10GeneratedAssetMarker>(P10EquipmentGrowthSetup.MarkerPath); if (marker == null || marker.fingerprint != P10EquipmentGrowthSetup.Fingerprint) throw new BuildFailedException("P10_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P10EquipmentGrowthSetup.ScreenPath) ?? throw new BuildFailedException("P10_UI_PREFAB_MISSING"); Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true); foreach (string id in P10EquipmentGrowthSetup.RequiredIds) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P10_UI_ID_INVALID: " + id);
            prefab.SetActive(true); P10EquipmentGrowthSetup.PrepareRoot(prefab); Canvas.ForceUpdateCanvases(); string[] invalid = prefab.GetComponentsInChildren<Button>(true).Where(value => value.GetComponent<RectTransform>().rect.width < 64 || value.GetComponent<RectTransform>().rect.height < 64).Select(value => value.name).ToArray(); prefab.SetActive(false); if (invalid.Length > 0) throw new BuildFailedException("P10_TOUCH_TARGET_INVALID: " + string.Join(",", invalid));
            Scene scene = EditorSceneManager.OpenScene(P10EquipmentGrowthSetup.BootstrapScenePath, OpenSceneMode.Single); EquipmentGrowthScreenPresenter[] screens = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<EquipmentGrowthScreenPresenter>(true)).ToArray(); EquipmentGrowthEntryButton[] entries = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<EquipmentGrowthEntryButton>(true)).ToArray(); if (screens.Length != 1 || entries.Length != 1) throw new BuildFailedException($"P10_SCENE_ENTRY_INVALID: screens={screens.Length}, entries={entries.Length}");
            EquipmentGrowthEntryButton entry = entries[0]; Button button = entry.GetComponent<Button>(); if (button == null || button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != entry || button.onClick.GetPersistentMethodName(0) != nameof(EquipmentGrowthEntryButton.OpenEquipmentGrowth)) throw new BuildFailedException("P10_SCENE_ENTRY_BINDING_INVALID");
        }
    }

    public static class P10CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P10/Generate Acceptance Captures")]
        public static void Run()
        {
            P10GeneratedAssetVerifier.Verify(); Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P10EquipmentGrowthSetup.ScreenPath); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); root.SetActive(true); P10EquipmentGrowthSetup.PrepareRoot(root);
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P10")); Directory.CreateDirectory(output); Transform[] nodes = root.GetComponentsInChildren<Transform>(true); nodes.Single(value => value.name == "P10_EMPTY_STATE").gameObject.SetActive(false);
            Capture(root, output, "p10_01_overview.png", 1920, 1080); nodes.Single(value => value.name == "P10_RESULT_TOAST").gameObject.SetActive(true); nodes.Single(value => value.name == "ToastText").GetComponent<TMP_Text>().text = "강화 성공 · +7 · 비용과 결과를 저장했습니다."; Capture(root, output, "p10_02_enhance_success.png", 1920, 1080);
            nodes.Single(value => value.name == "P10_RESULT_TOAST").gameObject.SetActive(false); nodes.Single(value => value.name == "P10_ITEM_TITLE").GetComponent<TMP_Text>().text = "EQ_T3_WARRIOR_WEAPON  <color=#F1C96A>+7</color>"; nodes.Single(value => value.name == "RefineText").GetComponent<TMP_Text>().text = "현재 옵션  공격력 +8.2%\n새 후보  <color=#74D8C5>치명타 +6.4%</color>\n\n적용 또는 유지를 선택하세요"; Capture(root, output, "p10_03_refine_candidate.png", 1920, 1080);
            Capture(root, output, "p10_04_20x9.png", 2400, 1080); Debug.Log($"P10_CAPTURES={output}; count=4");
        }
        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            root.SetActive(true); P10EquipmentGrowthSetup.PrepareRoot(root); Canvas canvas = root.GetComponent<Canvas>(); canvas.enabled = true; var cameraObject = new GameObject("P10CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(9, 11, 14, 255); camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.enabled = true;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false); string path = Path.Combine(directory, filename); File.WriteAllBytes(path, texture.EncodeToPNG()); RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; if (new FileInfo(path).Length <= 1024) throw new BuildFailedException("P10_CAPTURE_FAILED: " + filename);
        }
    }

    public static class P10AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P10/Build Android Development APK")]
        public static void Build()
        {
            P10GeneratedAssetVerifier.Verify(); string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P10-Development.apk")); Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P10 Android output invalid."));
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P10 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}"); Debug.Log($"P10_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    internal sealed class P10EquipmentGrowthBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;
        public void OnPreprocessBuild(BuildReport report) => P10GeneratedAssetVerifier.Verify();
    }
}
