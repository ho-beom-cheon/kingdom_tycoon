using System;
using System.IO;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.Progression;
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
    public static class P11ProgressionSetup
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P11Progression";
        public const string ScreenPath = GeneratedRoot + "/Prefabs/P11_PROGRESSION_SCREEN.prefab";
        public const string MarkerPath = GeneratedRoot + "/P11Progression.marker.asset";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "p11-progression-v1-content9-84-tables-xp-promotion-mobile-safe";
        internal static readonly string[] RequiredIds =
        {
            "P11_PROGRESSION_SCREEN", "P11_HEADER", "P11_CLOSE", "P11_META", "P11_GUILD_STATUS", "P11_MERCENARY_CARD",
            "P11_PREVIOUS", "P11_NEXT", "P11_RANK_JOURNEY", "P11_EXPERIENCE", "P11_REQUIREMENTS", "P11_COSTS",
            "P11_EQUIPMENT_SCORE", "P11_REVIEW_RESULT", "P11_PRIMARY", "P11_EMPTY_STATE", "P11_RESULT_TOAST"
        };

        [MenuItem("Kingdom Tycoon/P11/Run Complete Setup")]
        public static void Run() { ValidateContent(); GenerateScreen(); IntegrateBootstrapScene(); P11GeneratedAssetVerifier.Verify(); Debug.Log("P11 progression setup completed."); }

        [MenuItem("Kingdom Tycoon/P11/Validate Content Package .9")]
        public static void ValidateContent()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.9"); string manifest = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifest)) throw new BuildFailedException("P11_CONTENT_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifest), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 84 || result.Catalog.GetTable("mercenary_level_curves.csv").Rows.Count != 270)
                throw new BuildFailedException("P11_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        public static void GenerateScreen()
        {
            EnsureFolder(GeneratedRoot + "/Prefabs"); AssetDatabase.DeleteAsset(ScreenPath);
            var root = new GameObject("P11_PROGRESSION_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(ProgressionScreenPresenter));
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 96;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080)); CanvasGroup group = root.GetComponent<CanvasGroup>();
            Image backdrop = Panel("Backdrop", root.transform, new Color32(8, 12, 15, 252)); Stretch(backdrop.rectTransform);
            Image safe = Panel("SafeArea", backdrop.transform, new Color32(22, 27, 30, 255)); SetRect(safe.rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 28), new Vector2(-44, -28));

            Image header = Panel("P11_HEADER", safe.transform, new Color32(50, 38, 27, 255)); SetRect(header.rectTransform, new Vector2(0, .865f), Vector2.one, Vector2.zero, Vector2.zero);
            Text("Title", header.transform, "모험가 길드 · 성장 심사", 43, TextAlignmentOptions.Left, new Vector2(.025f, .17f), new Vector2(.43f, .88f), new Color32(245, 207, 111, 255));
            Text("Subtitle", header.transform, "수습부터 전설까지 · 실패 없는 승급", 21, TextAlignmentOptions.Left, new Vector2(.35f, .20f), new Vector2(.62f, .82f), new Color32(196, 189, 173, 255));
            TMP_Text meta = Text("P11_META", header.transform, "콘텐츠 9  ·  저장 0  ·  왕국 골드 5,000", 21, TextAlignmentOptions.Right, new Vector2(.60f, .17f), new Vector2(.88f, .84f));
            Button close = Button("P11_CLOSE", header.transform, "닫기", new Color32(80, 68, 57, 255), 23); SetRect(close.GetComponent<RectTransform>(), new Vector2(.89f, .14f), new Vector2(.98f, .86f), Vector2.zero, Vector2.zero);

            TMP_Text guild = Text("P11_GUILD_STATUS", safe.transform, "모험가 길드 1레벨  ·  <color=#82DEC5>심사 접수 중</color>", 23, TextAlignmentOptions.Left, new Vector2(.018f, .80f), new Vector2(.46f, .855f), new Color32(217, 207, 188, 255));
            Image card = Panel("P11_MERCENARY_CARD", safe.transform, new Color32(37, 43, 46, 255)); SetRect(card.rectTransform, new Vector2(0, .14f), new Vector2(.23f, .79f), Vector2.zero, Vector2.zero);
            Panel("CardAccent", card.transform, new Color32(200, 145, 54, 255)).rectTransform.anchorMax = new Vector2(.015f, 1);
            Text("CardEyebrow", card.transform, "길드 기록", 17, TextAlignmentOptions.Left, new Vector2(.07f, .86f), new Vector2(.93f, .95f), new Color32(112, 216, 194, 255));
            TMP_Text mercenary = Text("MercenaryText", card.transform, "리안\n<size=24>고급 등급 · 수습 · 20레벨</size>\n<size=19>1/4 · 심사 준비</size>", 34, TextAlignmentOptions.TopLeft, new Vector2(.07f, .48f), new Vector2(.93f, .85f));
            Text("PortraitSeal", card.transform, "길드\n<size=20>모험가</size>", 36, TextAlignmentOptions.Center, new Vector2(.22f, .25f), new Vector2(.78f, .49f), new Color32(224, 190, 101, 255));
            Button previous = Button("P11_PREVIOUS", card.transform, "‹ 이전", new Color32(61, 71, 73, 255), 22); SetRect(previous.GetComponent<RectTransform>(), new Vector2(.07f, .07f), new Vector2(.47f, .20f), Vector2.zero, Vector2.zero);
            Button next = Button("P11_NEXT", card.transform, "다음 ›", new Color32(61, 71, 73, 255), 22); SetRect(next.GetComponent<RectTransform>(), new Vector2(.53f, .07f), new Vector2(.93f, .20f), Vector2.zero, Vector2.zero);

            Image journeyPanel = Panel("P11_RANK_JOURNEY", safe.transform, new Color32(32, 47, 48, 255)); SetRect(journeyPanel.rectTransform, new Vector2(.24f, .65f), new Vector2(1, .79f), Vector2.zero, Vector2.zero);
            Text("JourneyLabel", journeyPanel.transform, "승급 여정", 16, TextAlignmentOptions.Left, new Vector2(.025f, .67f), new Vector2(.16f, .93f), new Color32(112, 216, 194, 255));
            TMP_Text journey = Text("JourneyText", journeyPanel.transform, "<color=#F3D071><b>수습</b></color>   ›   정식   ›   숙련   ›   정예   ›   영웅   ›   전설", 25, TextAlignmentOptions.Center, new Vector2(.15f, .12f), new Vector2(.98f, .88f));

            Image experiencePanel = Panel("P11_EXPERIENCE", safe.transform, new Color32(42, 39, 35, 255)); SetRect(experiencePanel.rectTransform, new Vector2(.24f, .40f), new Vector2(.48f, .64f), Vector2.zero, Vector2.zero);
            TMP_Text experience = Text("ExperienceText", experiencePanel.transform, "현재 성장\n<size=35><color=#F4E6BD>20레벨</color> / 20레벨</size>\n경험치 0 / 0  ·  100%", 22, TextAlignmentOptions.TopLeft, new Vector2(.07f, .12f), new Vector2(.93f, .90f));
            Image requirementsPanel = Panel("P11_REQUIREMENTS", safe.transform, new Color32(34, 45, 43, 255)); SetRect(requirementsPanel.rectTransform, new Vector2(.49f, .27f), new Vector2(.75f, .64f), Vector2.zero, Vector2.zero);
            Text("RequirementsTitle", requirementsPanel.transform, "승급 조건", 27, TextAlignmentOptions.Left, new Vector2(.07f, .82f), new Vector2(.93f, .96f), new Color32(129, 226, 199, 255));
            TMP_Text requirements = Text("RequirementsText", requirementsPanel.transform, "<color=#82DEC5>✓</color> 레벨  20 / 20\n<color=#82DEC5>✓</color> 기여도  120 / 100\n<color=#82DEC5>✓</color> 전투 실적  3 / 3\n<color=#82DEC5>✓</color> 길드 레벨  1 / 1", 21, TextAlignmentOptions.TopLeft, new Vector2(.07f, .12f), new Vector2(.94f, .79f));
            Image costPanel = Panel("P11_COSTS", safe.transform, new Color32(48, 42, 35, 255)); SetRect(costPanel.rectTransform, new Vector2(.76f, .40f), new Vector2(1, .64f), Vector2.zero, Vector2.zero);
            TMP_Text costs = Text("CostsText", costPanel.transform, "정식 심사 비용\n개인 골드  500 / 550\n왕국 골드  5,000 / 220", 21, TextAlignmentOptions.TopLeft, new Vector2(.07f, .12f), new Vector2(.93f, .90f));

            Image equipmentPanel = Panel("P11_EQUIPMENT_SCORE", safe.transform, new Color32(34, 39, 43, 255)); SetRect(equipmentPanel.rectTransform, new Vector2(.24f, .14f), new Vector2(.48f, .39f), Vector2.zero, Vector2.zero);
            TMP_Text equipment = Text("EquipmentText", equipmentPanel.transform, "장비 준비도\n<size=32><color=#E7A15C>320</color></size> / 권장 400\n권장 미달이어도 승급은 가능합니다.", 21, TextAlignmentOptions.TopLeft, new Vector2(.07f, .12f), new Vector2(.93f, .90f));
            Image reviewPanel = Panel("P11_REVIEW_RESULT", safe.transform, new Color32(43, 38, 34, 255)); SetRect(reviewPanel.rectTransform, new Vector2(.76f, .14f), new Vector2(1, .39f), Vector2.zero, Vector2.zero);
            TMP_Text result = Text("ResultText", reviewPanel.transform, "최근 기록\n승급 조건 달성", 23, TextAlignmentOptions.TopLeft, new Vector2(.07f, .34f), new Vector2(.93f, .90f), new Color32(232, 221, 197, 255));
            Button primary = Button("P11_PRIMARY", reviewPanel.transform, "심사 시작", new Color32(180, 109, 42, 255), 27); SetRect(primary.GetComponent<RectTransform>(), new Vector2(.07f, .06f), new Vector2(.93f, .34f), Vector2.zero, Vector2.zero); TMP_Text primaryText = primary.GetComponentInChildren<TMP_Text>();

            Image state = Panel("P11_EMPTY_STATE", safe.transform, new Color32(32, 38, 41, 253)); SetRect(state.rectTransform, new Vector2(.22f, .28f), new Vector2(.78f, .72f), Vector2.zero, Vector2.zero);
            TMP_Text stateTitle = Text("EmptyTitle", state.transform, "길드 기록을 확인하는 중", 36, TextAlignmentOptions.Center, new Vector2(.08f, .62f), new Vector2(.92f, .86f), new Color32(245, 207, 111, 255));
            TMP_Text stateBody = Text("EmptyBody", state.transform, "레벨·기여도·승급 심사 상태를 불러오고 있습니다.", 24, TextAlignmentOptions.Center, new Vector2(.08f, .22f), new Vector2(.92f, .61f));
            Image toast = Panel("P11_RESULT_TOAST", safe.transform, new Color32(39, 104, 80, 252)); SetRect(toast.rectTransform, new Vector2(.31f, .025f), new Vector2(.69f, .115f), Vector2.zero, Vector2.zero);
            TMP_Text toastText = Text("ToastText", toast.transform, "길드 심사를 접수했습니다.", 23, TextAlignmentOptions.Center, new Vector2(.04f, .08f), new Vector2(.96f, .92f)); toast.gameObject.SetActive(false);

            ProgressionScreenView view = safe.gameObject.AddComponent<ProgressionScreenView>(); view.Configure(group, meta, guild, mercenary, journey, experience, requirements, costs, equipment, result,
                state.gameObject, stateTitle, stateBody, toast.gameObject, toastText, close, previous, next, primary, primaryText);
            root.GetComponent<ProgressionScreenPresenter>().Configure(view); PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root);
            P11GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P11GeneratedAssetMarker>(MarkerPath); if (marker == null) { marker = ScriptableObject.CreateInstance<P11GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void IntegrateBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single); CommonUiRoot common = Object.FindFirstObjectByType<CommonUiRoot>(FindObjectsInactive.Include) ?? throw new BuildFailedException("P11_COMMON_UI_ROOT_MISSING");
            Transform screenCanvas = common.transform.Find("ScreenCanvas") ?? throw new BuildFailedException("P11_SCREEN_CANVAS_MISSING"); foreach (Transform old in screenCanvas.Cast<Transform>().Where(value => value.name == "P11_PROGRESSION_SCREEN").ToArray()) Object.DestroyImmediate(old.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P11_UI_PREFAB_MISSING"); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screenCanvas); root.name = "P11_PROGRESSION_SCREEN"; PrepareRoot(root);
            ProgressionScreenPresenter presenter = root.GetComponent<ProgressionScreenPresenter>(); root.SetActive(false);
            Transform hud = common.transform.Find("HudCanvas/SafeArea") ?? throw new BuildFailedException("P11_HUD_SAFE_AREA_MISSING"); Transform existing = hud.Find("P11_PROGRESSION_NAV_BUTTON"); if (existing != null) Object.DestroyImmediate(existing.gameObject);
            Button button = Button("P11_PROGRESSION_NAV_BUTTON", hud, "승급", new Color32(177, 108, 42, 255), 23); SetRect(button.GetComponent<RectTransform>(), new Vector2(.75f, 0), new Vector2(.90f, 0), new Vector2(0, 16), new Vector2(0, 96));
            ProgressionEntryButton entry = button.gameObject.AddComponent<ProgressionEntryButton>(); entry.Configure(presenter, button); UnityEventTools.AddPersistentListener(button.onClick, entry.OpenProgression);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        internal static void PrepareRoot(GameObject root) { RectTransform rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920, 1080); CanvasGroup group = root.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1; }
        private static Image Panel(string name, Transform parent, Color color) { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); Image image = value.GetComponent<Image>(); image.color = color; return image; }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color? color = null) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text label = go.GetComponent<TMP_Text>(); label.text = value; label.fontSize = size; label.alignment = alignment; label.color = color ?? new Color32(238, 231, 214, 255); label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string label, Color color, float size) { Image image = Panel(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; ColorBlock colors = button.colors; colors.highlightedColor = Color.Lerp(color, Color.white, .12f); colors.pressedColor = Color.Lerp(color, Color.black, .16f); colors.disabledColor = new Color(color.r * .55f, color.g * .55f, color.b * .55f, .72f); button.colors = colors; Text("Label", image.transform, label, size, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offsetMin; value.offsetMax = offsetMax; }
        private static void EnsureFolder(string path) { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P11GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P11/Verify Generated Assets")]
        public static void Verify()
        {
            P11ProgressionSetup.ValidateContent(); P11GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P11GeneratedAssetMarker>(P11ProgressionSetup.MarkerPath); if (marker == null || marker.fingerprint != P11ProgressionSetup.Fingerprint) throw new BuildFailedException("P11_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P11ProgressionSetup.ScreenPath) ?? throw new BuildFailedException("P11_UI_PREFAB_MISSING"); Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true); foreach (string id in P11ProgressionSetup.RequiredIds) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P11_UI_ID_INVALID: " + id);
            prefab.SetActive(true); P11ProgressionSetup.PrepareRoot(prefab); Canvas.ForceUpdateCanvases(); string[] invalid = prefab.GetComponentsInChildren<Button>(true).Where(value => value.GetComponent<RectTransform>().rect.width < 64 || value.GetComponent<RectTransform>().rect.height < 64).Select(value => value.name).ToArray(); prefab.SetActive(false); if (invalid.Length > 0) throw new BuildFailedException("P11_TOUCH_TARGET_INVALID: " + string.Join(",", invalid));
            Scene scene = EditorSceneManager.OpenScene(P11ProgressionSetup.BootstrapScenePath, OpenSceneMode.Single); ProgressionScreenPresenter[] screens = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<ProgressionScreenPresenter>(true)).ToArray(); ProgressionEntryButton[] entries = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<ProgressionEntryButton>(true)).ToArray(); if (screens.Length != 1 || entries.Length != 1) throw new BuildFailedException($"P11_SCENE_ENTRY_INVALID: screens={screens.Length}, entries={entries.Length}");
            ProgressionEntryButton entry = entries[0]; Button button = entry.GetComponent<Button>(); if (button == null || button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != entry || button.onClick.GetPersistentMethodName(0) != nameof(ProgressionEntryButton.OpenProgression)) throw new BuildFailedException("P11_SCENE_ENTRY_BINDING_INVALID");
        }
    }

    public static class P11CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P11/Generate Acceptance Captures")]
        public static void Run()
        {
            P11GeneratedAssetVerifier.Verify(); Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P11ProgressionSetup.ScreenPath); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); root.SetActive(true); P11ProgressionSetup.PrepareRoot(root);
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P11")); Directory.CreateDirectory(output); Transform[] nodes = root.GetComponentsInChildren<Transform>(true); nodes.Single(value => value.name == "P11_EMPTY_STATE").gameObject.SetActive(false);
            Capture(root, output, "p11_01_overview.png", 1920, 1080); nodes.Single(value => value.name == "ResultText").GetComponent<TMP_Text>().text = "길드 심사 진행 중\n<color=#F3D071>04:18</color> 남음"; nodes.Single(value => value.name == "P11_PRIMARY").GetComponentInChildren<TMP_Text>().text = "심사 중 · 04:18"; Capture(root, output, "p11_02_review.png", 1920, 1080);
            nodes.Single(value => value.name == "ResultText").GetComponent<TMP_Text>().text = "심사 완료\n승급 결과를 적용할 수 있습니다."; nodes.Single(value => value.name == "P11_PRIMARY").GetComponentInChildren<TMP_Text>().text = "승급 완료"; Capture(root, output, "p11_03_completed.png", 1920, 1080);
            Capture(root, output, "p11_04_20x9.png", 2400, 1080); Debug.Log($"P11_CAPTURES={output}; count=4");
        }
        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            root.SetActive(true); P11ProgressionSetup.PrepareRoot(root); RectTransform rootRect = root.GetComponent<RectTransform>(); rootRect.sizeDelta = new Vector2(width, height); rootRect.position = Vector3.zero;
            Canvas canvas = root.GetComponent<Canvas>(); canvas.enabled = true; var cameraObject = new GameObject("P11CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(8, 11, 14, 255); camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false); string path = Path.Combine(directory, filename); File.WriteAllBytes(path, texture.EncodeToPNG()); RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; if (new FileInfo(path).Length <= 1024) throw new BuildFailedException("P11_CAPTURE_FAILED: " + filename);
        }
    }

    public static class P11AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P11/Build Android Development APK")]
        public static void Build()
        {
            P11GeneratedAssetVerifier.Verify(); string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P11-Development.apk")); Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P11 Android output invalid."));
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P11 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}"); Debug.Log($"P11_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    internal sealed class P11ProgressionBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 110;
        public void OnPreprocessBuild(BuildReport report) => P11GeneratedAssetVerifier.Verify();
    }
}
