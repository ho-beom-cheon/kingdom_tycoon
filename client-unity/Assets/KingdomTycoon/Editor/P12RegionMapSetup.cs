using System;
using System.IO;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
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
    public static class P12RegionMapSetup
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P12Regions";
        public const string ScreenPath = GeneratedRoot + "/Prefabs/P12_REGION_MAP_SCREEN.prefab";
        public const string MarkerPath = GeneratedRoot + "/P12Regions.marker.asset";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "p12-regions-v1-content10-88-tables-five-region-mobile-safe";
        internal static readonly string[] RequiredIds =
        {
            "P12_REGION_MAP_SCREEN", "P12_HEADER", "P12_CLOSE", "P12_META", "P12_KINGDOM_STATUS", "P12_MAP_PANEL",
            "P12_REGION_R01", "P12_REGION_R02", "P12_REGION_R03", "P12_REGION_R04", "P12_REGION_R05", "P12_DETAIL_PANEL",
            "P12_PROGRESS_FILL", "P12_REQUIREMENTS", "P12_DEPLOYMENT", "P12_ACTIVITY", "P12_POLICY", "P12_HUNT", "P12_EMPTY_STATE", "P12_RESULT_TOAST"
        };

        [MenuItem("Kingdom Tycoon/P12/Run Complete Setup")]
        public static void Run() { ValidateContent(); GenerateScreen(); IntegrateBootstrapScene(); P12GeneratedAssetVerifier.Verify(); Debug.Log("P12 region map setup completed."); }

        [MenuItem("Kingdom Tycoon/P12/Validate Content Package .10")]
        public static void ValidateContent()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.10"); string manifest = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifest)) throw new BuildFailedException("P12_CONTENT_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifest), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 88 || result.Catalog.GetTable("regions.csv").Rows.Count != 5 || result.Catalog.GetTable("region_encounter_profiles.csv").Rows.Count != 25)
                throw new BuildFailedException("P12_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        public static void GenerateScreen()
        {
            EnsureFolder(GeneratedRoot + "/Prefabs"); AssetDatabase.DeleteAsset(ScreenPath);
            var root = new GameObject("P12_REGION_MAP_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RegionMapScreenPresenter));
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 97;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080)); CanvasGroup group = root.GetComponent<CanvasGroup>();
            Image backdrop = Panel("Backdrop", root.transform, new Color32(8, 13, 16, 252)); Stretch(backdrop.rectTransform);
            Image safe = Panel("SafeArea", backdrop.transform, new Color32(18, 26, 29, 255)); SetRect(safe.rectTransform, Vector2.zero, Vector2.one, new Vector2(44, 28), new Vector2(-44, -28));

            Image header = Panel("P12_HEADER", safe.transform, new Color32(54, 42, 29, 255)); SetRect(header.rectTransform, new Vector2(0, .865f), Vector2.one, Vector2.zero, Vector2.zero);
            Text("Title", header.transform, "왕국 개척 지도", 43, TextAlignmentOptions.Left, new Vector2(.025f, .18f), new Vector2(.35f, .88f), new Color32(247, 210, 120, 255));
            Text("Subtitle", header.transform, "FIVE FRONTIERS  ·  지역 개방과 파견 통제", 19, TextAlignmentOptions.Left, new Vector2(.27f, .20f), new Vector2(.61f, .82f), new Color32(177, 198, 190, 255));
            TMP_Text meta = Text("P12_META", header.transform, "content.10  ·  r0  ·  5개 지역", 20, TextAlignmentOptions.Right, new Vector2(.60f, .17f), new Vector2(.88f, .84f));
            Button close = Button("P12_CLOSE", header.transform, "닫기", new Color32(81, 68, 55, 255), 22); SetRect(close.GetComponent<RectTransform>(), new Vector2(.89f, .14f), new Vector2(.98f, .86f), Vector2.zero, Vector2.zero);
            TMP_Text kingdom = Text("P12_KINGDOM_STATUS", safe.transform, "초기 왕국  ·  개방 1/5  ·  파견 정책 정상", 22, TextAlignmentOptions.Left, new Vector2(.018f, .805f), new Vector2(.65f, .855f), new Color32(136, 225, 201, 255));

            Image map = Panel("P12_MAP_PANEL", safe.transform, new Color32(25, 38, 41, 255)); SetRect(map.rectTransform, new Vector2(0, .035f), new Vector2(.665f, .795f), Vector2.zero, Vector2.zero);
            Text("MapEyebrow", map.transform, "ROYAL EXPLORATION ROUTE", 16, TextAlignmentOptions.Left, new Vector2(.035f, .91f), new Vector2(.36f, .98f), new Color32(114, 218, 193, 255));
            Text("Compass", map.transform, "N\n↑", 18, TextAlignmentOptions.Center, new Vector2(.89f, .84f), new Vector2(.97f, .96f), new Color32(209, 177, 96, 255));
            Image route = Panel("ExpeditionRoute", map.transform, new Color32(113, 91, 57, 255)); SetRect(route.rectTransform, new Vector2(.12f, .485f), new Vector2(.88f, .502f), Vector2.zero, Vector2.zero);
            var nodes = new Button[5]; var nodeLabels = new TMP_Text[5];
            Vector2[] positions = { new(.04f, .57f), new(.22f, .31f), new(.40f, .58f), new(.59f, .31f), new(.77f, .57f) };
            string[] names = { "왕국 외곽 초원", "안개 낀 고대림", "폐광 심층부", "독안개 습지", "서리 왕국 유적" };
            for (int index = 0; index < 5; index++)
            {
                string id = $"P12_REGION_R0{index + 1}"; nodes[index] = Button(id, map.transform, $"<size=15>0{index + 1}</size>\n<b>{(index == 0 ? names[index] : "미개척 지역")}</b>\n<size=16>{(index == 0 ? "0% · 파견 허용" : "개방 조건 필요")}</size>", index == 0 ? new Color32(43, 84, 78, 255) : new Color32(49, 54, 58, 245), 21);
                SetRect(nodes[index].GetComponent<RectTransform>(), positions[index], positions[index] + new Vector2(.18f, .19f), Vector2.zero, Vector2.zero); nodeLabels[index] = nodes[index].GetComponentInChildren<TMP_Text>();
                Text("RouteIndex", map.transform, (index + 1).ToString(), 15, TextAlignmentOptions.Center, new Vector2(.135f + index * .185f, .48f), new Vector2(.165f + index * .185f, .53f), new Color32(235, 202, 113, 255));
            }
            Text("MapLegend", map.transform, "◆ 개방 조건 충족 시 자동 등록    ● 파견 허용    ■ 파견 중지", 17, TextAlignmentOptions.Left, new Vector2(.035f, .05f), new Vector2(.82f, .11f), new Color32(172, 183, 178, 255));

            Image detail = Panel("P12_DETAIL_PANEL", safe.transform, new Color32(36, 39, 39, 255)); SetRect(detail.rectTransform, new Vector2(.675f, .035f), new Vector2(1, .855f), Vector2.zero, Vector2.zero);
            TMP_Text title = Text("RegionTitle", detail.transform, "왕국 외곽 초원", 34, TextAlignmentOptions.Left, new Vector2(.06f, .865f), new Vector2(.94f, .96f), new Color32(246, 211, 124, 255));
            TMP_Text tier = Text("RegionTier", detail.transform, "TIER 1  ·  초원  ·  최소 수습  ·  권장 전투력 100", 17, TextAlignmentOptions.Left, new Vector2(.06f, .815f), new Vector2(.94f, .87f), new Color32(174, 195, 186, 255));
            TMP_Text progress = Text("ProgressText", detail.transform, "지역 조사도  <color=#F4D27A>0%</color>", 22, TextAlignmentOptions.Left, new Vector2(.06f, .752f), new Vector2(.94f, .81f));
            Image bar = Panel("ProgressTrack", detail.transform, new Color32(20, 27, 29, 255)); SetRect(bar.rectTransform, new Vector2(.06f, .72f), new Vector2(.94f, .747f), Vector2.zero, Vector2.zero);
            Image fill = Panel("P12_PROGRESS_FILL", bar.transform, new Color32(117, 214, 183, 255)); SetRect(fill.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            Text("RequirementsTitle", detail.transform, "개방 조건", 21, TextAlignmentOptions.Left, new Vector2(.06f, .66f), new Vector2(.94f, .71f), new Color32(129, 226, 199, 255));
            TMP_Text requirements = Text("P12_REQUIREMENTS", detail.transform, "<color=#83E0C5>✓</color> 왕국 단계 1   1 / 1", 18, TextAlignmentOptions.TopLeft, new Vector2(.06f, .51f), new Vector2(.94f, .66f));
            Image deploymentPanel = Panel("P12_DEPLOYMENT", detail.transform, new Color32(28, 48, 48, 255)); SetRect(deploymentPanel.rectTransform, new Vector2(.05f, .28f), new Vector2(.49f, .50f), Vector2.zero, Vector2.zero);
            TMP_Text deployment = Text("DeploymentText", deploymentPanel.transform, "권장 파티  2명\n회복 물약  2개\n가방 여유  4칸\n\n<size=19>출전 가능 용병 4명</size>", 18, TextAlignmentOptions.TopLeft, new Vector2(.08f, .08f), new Vector2(.92f, .92f));
            Image activityPanel = Panel("P12_ACTIVITY", detail.transform, new Color32(48, 42, 35, 255)); SetRect(activityPanel.rectTransform, new Vector2(.51f, .28f), new Vector2(.95f, .50f), Vector2.zero, Vector2.zero);
            TMP_Text activity = Text("ActivityText", activityPanel.transform, "누적 사냥  0회\n정예 처치  0회\n도달 최고 등급  -\n\n<size=18>개척대가 대기 중입니다.</size>", 18, TextAlignmentOptions.TopLeft, new Vector2(.08f, .08f), new Vector2(.92f, .92f));
            Button policy = Button("P12_POLICY", detail.transform, "파견 허용 중", new Color32(47, 92, 82, 255), 21); SetRect(policy.GetComponent<RectTransform>(), new Vector2(.05f, .17f), new Vector2(.95f, .26f), Vector2.zero, Vector2.zero); TMP_Text policyText = policy.GetComponentInChildren<TMP_Text>();
            Button hunt = Button("P12_HUNT", detail.transform, "추천 파티로 사냥 시작", new Color32(181, 111, 44, 255), 24); SetRect(hunt.GetComponent<RectTransform>(), new Vector2(.05f, .045f), new Vector2(.95f, .15f), Vector2.zero, Vector2.zero); TMP_Text huntText = hunt.GetComponentInChildren<TMP_Text>();

            Image state = Panel("P12_EMPTY_STATE", safe.transform, new Color32(30, 38, 41, 253)); SetRect(state.rectTransform, new Vector2(.20f, .27f), new Vector2(.80f, .71f), Vector2.zero, Vector2.zero);
            TMP_Text stateTitle = Text("StateTitle", state.transform, "왕국 지도를 펼치는 중", 36, TextAlignmentOptions.Center, new Vector2(.08f, .62f), new Vector2(.92f, .86f), new Color32(245, 207, 111, 255));
            TMP_Text stateBody = Text("StateBody", state.transform, "지역 개방 조건과 파견 정책을 불러오고 있습니다.", 23, TextAlignmentOptions.Center, new Vector2(.08f, .24f), new Vector2(.92f, .61f));
            Image toast = Panel("P12_RESULT_TOAST", safe.transform, new Color32(38, 105, 81, 252)); SetRect(toast.rectTransform, new Vector2(.31f, .007f), new Vector2(.69f, .085f), Vector2.zero, Vector2.zero);
            TMP_Text toastText = Text("ToastText", toast.transform, "파견 정책을 갱신했습니다.", 22, TextAlignmentOptions.Center, new Vector2(.04f, .08f), new Vector2(.96f, .92f)); toast.gameObject.SetActive(false);

            RegionMapScreenView view = safe.gameObject.AddComponent<RegionMapScreenView>(); view.Configure(group, meta, kingdom, nodes, nodeLabels, title, tier, progress, fill, requirements, deployment, activity, close, policy, policyText, hunt, huntText, state.gameObject, stateTitle, stateBody, toast.gameObject, toastText);
            root.GetComponent<RegionMapScreenPresenter>().Configure(view); PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root);
            P12GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P12GeneratedAssetMarker>(MarkerPath); if (marker == null) { AssetDatabase.DeleteAsset(MarkerPath); marker = ScriptableObject.CreateInstance<P12GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void IntegrateBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single); CommonUiRoot common = Object.FindFirstObjectByType<CommonUiRoot>(FindObjectsInactive.Include) ?? throw new BuildFailedException("P12_COMMON_UI_ROOT_MISSING");
            Transform screenCanvas = common.transform.Find("ScreenCanvas") ?? throw new BuildFailedException("P12_SCREEN_CANVAS_MISSING"); foreach (Transform old in screenCanvas.Cast<Transform>().Where(value => value.name == "P12_REGION_MAP_SCREEN").ToArray()) Object.DestroyImmediate(old.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P12_UI_PREFAB_MISSING"); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screenCanvas); root.name = "P12_REGION_MAP_SCREEN"; PrepareRoot(root);
            RegionMapScreenPresenter presenter = root.GetComponent<RegionMapScreenPresenter>(); root.SetActive(false);
            Transform hud = common.transform.Find("HudCanvas/SafeArea") ?? throw new BuildFailedException("P12_HUD_SAFE_AREA_MISSING"); Transform existing = hud.Find("P12_REGION_MAP_NAV_BUTTON"); if (existing != null) Object.DestroyImmediate(existing.gameObject);
            Button button = Button("P12_REGION_MAP_NAV_BUTTON", hud, "개척", new Color32(46, 111, 94, 255), 22); SetRect(button.GetComponent<RectTransform>(), new Vector2(.90f, 0), new Vector2(.995f, 0), new Vector2(0, 16), new Vector2(0, 96));
            RegionMapEntryButton entry = button.gameObject.AddComponent<RegionMapEntryButton>(); entry.Configure(presenter, button); UnityEventTools.AddPersistentListener(button.onClick, entry.OpenRegionMap);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        internal static void PrepareRoot(GameObject root) { RectTransform rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920, 1080); CanvasGroup group = root.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1; }
        private static Image Panel(string name, Transform parent, Color color) { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); Image image = value.GetComponent<Image>(); image.color = color; return image; }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color? color = null) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text label = go.GetComponent<TMP_Text>(); label.text = value; label.fontSize = size; label.alignment = alignment; label.color = color ?? new Color32(238, 231, 214, 255); label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string label, Color color, float size) { Image image = Panel(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; ColorBlock colors = button.colors; colors.highlightedColor = Color.Lerp(color, Color.white, .12f); colors.pressedColor = Color.Lerp(color, Color.black, .16f); colors.disabledColor = new Color(color.r * .55f, color.g * .55f, color.b * .55f, .72f); button.colors = colors; Text("Label", image.transform, label, size, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offsetMin; value.offsetMax = offsetMax; }
        private static void EnsureFolder(string path) { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P12GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P12/Verify Generated Assets")]
        public static void Verify()
        {
            P12RegionMapSetup.ValidateContent(); P12GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P12GeneratedAssetMarker>(P12RegionMapSetup.MarkerPath); if (marker == null || marker.fingerprint != P12RegionMapSetup.Fingerprint) throw new BuildFailedException("P12_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P12RegionMapSetup.ScreenPath) ?? throw new BuildFailedException("P12_UI_PREFAB_MISSING"); Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true); foreach (string id in P12RegionMapSetup.RequiredIds) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P12_UI_ID_INVALID: " + id);
            prefab.SetActive(true); P12RegionMapSetup.PrepareRoot(prefab); Canvas.ForceUpdateCanvases(); string[] invalid = prefab.GetComponentsInChildren<Button>(true).Where(value => value.GetComponent<RectTransform>().rect.width < 64 || value.GetComponent<RectTransform>().rect.height < 64).Select(value => value.name).ToArray(); prefab.SetActive(false); if (invalid.Length > 0) throw new BuildFailedException("P12_TOUCH_TARGET_INVALID: " + string.Join(",", invalid));
            Scene scene = EditorSceneManager.OpenScene(P12RegionMapSetup.BootstrapScenePath, OpenSceneMode.Single); RegionMapScreenPresenter[] screens = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<RegionMapScreenPresenter>(true)).ToArray(); RegionMapEntryButton[] entries = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<RegionMapEntryButton>(true)).ToArray(); if (screens.Length != 1 || entries.Length != 1) throw new BuildFailedException($"P12_SCENE_ENTRY_INVALID: screens={screens.Length}, entries={entries.Length}");
            RegionMapEntryButton entry = entries[0]; Button button = entry.GetComponent<Button>(); if (button == null || button.onClick.GetPersistentEventCount() != 1 || button.onClick.GetPersistentTarget(0) != entry || button.onClick.GetPersistentMethodName(0) != nameof(RegionMapEntryButton.OpenRegionMap)) throw new BuildFailedException("P12_SCENE_ENTRY_BINDING_INVALID");
        }
    }

    public static class P12CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P12/Generate Acceptance Captures")]
        public static void Run()
        {
            P12GeneratedAssetVerifier.Verify(); Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P12RegionMapSetup.ScreenPath); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); root.SetActive(true); P12RegionMapSetup.PrepareRoot(root);
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P12")); Directory.CreateDirectory(output); Transform[] nodes = root.GetComponentsInChildren<Transform>(true); nodes.Single(value => value.name == "P12_EMPTY_STATE").gameObject.SetActive(false);
            Capture(root, output, "p12_01_region_overview.png", 1920, 1080); nodes.Single(value => value.name == "P12_REGION_R02").GetComponent<Image>().color = new Color32(191, 133, 53, 255); nodes.Single(value => value.name == "RegionTitle").GetComponent<TMP_Text>().text = "봉인된 개척지"; Capture(root, output, "p12_02_locked_requirements.png", 1920, 1080);
            nodes.Single(value => value.name == "P12_POLICY").GetComponentInChildren<TMP_Text>().text = "파견 중지됨"; nodes.Single(value => value.name == "P12_POLICY").GetComponent<Image>().color = new Color32(111, 67, 54, 255); Capture(root, output, "p12_03_policy_closed.png", 1920, 1080);
            nodes.Single(value => value.name == "RegionTitle").GetComponent<TMP_Text>().text = "독안개 습지"; nodes.Single(value => value.name == "ProgressText").GetComponent<TMP_Text>().text = "지역 조사도  <color=#F4D27A>72%</color>"; nodes.Single(value => value.name == "P12_PROGRESS_FILL").GetComponent<RectTransform>().anchorMax = new Vector2(.72f, 1); Capture(root, output, "p12_04_progress.png", 1920, 1080);
            Capture(root, output, "p12_05_20x9.png", 2400, 1080); Debug.Log($"P12_CAPTURES={output}; count=5");
        }
        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            root.SetActive(true); P12RegionMapSetup.PrepareRoot(root); RectTransform rootRect = root.GetComponent<RectTransform>(); rootRect.sizeDelta = new Vector2(width, height); rootRect.position = Vector3.zero; Canvas canvas = root.GetComponent<Canvas>(); canvas.enabled = true;
            var cameraObject = new GameObject("P12CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(8, 13, 16, 255); camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false); string path = Path.Combine(directory, filename); File.WriteAllBytes(path, texture.EncodeToPNG()); RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; if (new FileInfo(path).Length <= 1024) throw new BuildFailedException("P12_CAPTURE_FAILED: " + filename);
        }
    }

    public static class P12AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P12/Build Android Development APK")]
        public static void Build()
        {
            P12GeneratedAssetVerifier.Verify(); string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P12-Development.apk")); Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P12 Android output invalid."));
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development });
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P12 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}"); Debug.Log($"P12_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    internal sealed class P12RegionBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 120;
        public void OnPreprocessBuild(BuildReport report) => P12GeneratedAssetVerifier.Verify();
    }
}
