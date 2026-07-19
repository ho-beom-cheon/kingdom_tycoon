using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomTycoon.Editor
{
    public static class P06CombatSetup
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P06Combat";
        public const string ScreenPath = GeneratedRoot + "/RegionCombatScreen.prefab";
        public const string MarkerPath = GeneratedRoot + "/P06Combat.marker.asset";
        public const string RegionScenePath = "Assets/KingdomTycoon/Scenes/Region.unity";
        public const string Fingerprint = "p06-combat-p17-release-art-v1.0.0";
        private const string GroupName = "Content-P06-Combat-v1";

        private static readonly (string Name, string Address, Color Color)[] Assets =
        {
            ("JOB_WARRIOR", "P06/Characters/JOB_WARRIOR", new Color32(180, 80, 62, 255)),
            ("JOB_GUARDIAN", "P06/Characters/JOB_GUARDIAN", new Color32(70, 110, 165, 255)),
            ("JOB_ARCHER", "P06/Characters/JOB_ARCHER", new Color32(80, 150, 90, 255)),
            ("JOB_MAGE", "P06/Characters/JOB_MAGE", new Color32(140, 85, 175, 255)),
            ("JOB_CLERIC", "P06/Characters/JOB_CLERIC", new Color32(225, 205, 130, 255)),
            ("STATUS_POISON", "P06/Status/STATUS_POISON", new Color32(90, 165, 75, 255)),
            ("STATUS_BURN", "P06/Status/STATUS_BURN", new Color32(220, 85, 45, 255)),
            ("STATUS_SLOW", "P06/Status/STATUS_SLOW", new Color32(90, 180, 220, 255)),
            ("STATUS_TAUNT", "P06/Status/STATUS_TAUNT", new Color32(190, 65, 65, 255)),
            ("STATUS_BARRIER", "P06/Status/STATUS_BARRIER", new Color32(100, 145, 220, 255)),
            ("STATUS_BLESSING", "P06/Status/STATUS_BLESSING", new Color32(240, 210, 100, 255)),
            ("MONSTER_MELEE", "P06/Monsters/MELEE", new Color32(135, 75, 55, 255)),
            ("MONSTER_ELITE", "P06/Monsters/ELITE", new Color32(170, 55, 60, 255)),
            ("PROJECTILE", "P06/Projectile", new Color32(245, 225, 140, 255)),
            ("TILE_MEADOW", "P06/Tiles/Meadow", new Color32(74, 105, 66, 255)),
            ("MISSING", "P06/Missing", new Color32(255, 0, 255, 255))
        };

        [MenuItem("Kingdom Tycoon/P06/Run Complete Setup")]
        public static void Run()
        {
            ValidateContent();
            GenerateAssets();
            GenerateScreen();
            AttachToRegionScene();
            P06GeneratedAssetVerifier.VerifyAll();
            Debug.Log("P06 combat setup completed.");
        }

        [MenuItem("Kingdom Tycoon/P06/Validate Content Package .4")]
        public static void ValidateContent()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.4");
            string manifestPath = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifestPath)) throw new BuildFailedException("CONTENT_P06_MANIFEST_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifestPath), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 66)
                throw new BuildFailedException("CONTENT_P06_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        [MenuItem("Kingdom Tycoon/P06/Generate Combat Assets")]
        public static void GenerateAssets()
        {
            EnsureFolder(GeneratedRoot);
            EnsureFolder(GeneratedRoot + "/Sprites");
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("P06_ADDRESSABLES_MISSING");
            AddressableAssetGroup group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            foreach ((string name, string address, Color color) in Assets)
            {
                string path = GeneratedRoot + "/Sprites/" + name + ".asset";
                AssetDatabase.DeleteAsset(path);
                var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color[256];
                for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) pixels[y * 16 + x] = ReleasePixel(name, x, y, color);
                texture.SetPixels(pixels); texture.Apply(false, true);
                AssetDatabase.CreateAsset(texture, path);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                entry.address = address; entry.SetLabel("P06-Combat", true, true);
            }
            P06GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P06GeneratedAssetMarker>(MarkerPath);
            if (marker == null) { marker = ScriptableObject.CreateInstance<P06GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static Color ReleasePixel(string name, int x, int y, Color baseColor)
        {
            Color clear = new(0, 0, 0, 0);
            Color outline = new(.11f, .09f, .09f, 1f);
            Color light = Color.Lerp(baseColor, Color.white, .24f);
            Color dark = Color.Lerp(baseColor, Color.black, .34f);
            int dx = x - 8;
            int dy = y - 8;

            if (name == "TILE_MEADOW")
            {
                if ((x * 5 + y * 3) % 11 == 0) return light;
                if ((x + y * 2) % 13 == 0) return dark;
                return baseColor;
            }
            if (name == "PROJECTILE") return Math.Abs(dx) + Math.Abs(dy) <= 4 ? (Math.Abs(dx) + Math.Abs(dy) <= 2 ? Color.white : baseColor) : clear;
            if (name == "MISSING") return (x / 4 + y / 4) % 2 == 0 ? new Color(1, 0, 1, 1) : outline;
            if (name.StartsWith("STATUS_", StringComparison.Ordinal))
            {
                int radius = dx * dx + dy * dy;
                if (radius is > 42 and <= 55) return outline;
                if (radius <= 42)
                {
                    bool mark = name switch
                    {
                        "STATUS_POISON" => ((dx == -3 || dx == 3) && dy >= 1) || (dy == -3 && Math.Abs(dx) <= 3),
                        "STATUS_BURN" => (Math.Abs(dx) <= 2 && dy is >= -5 and <= 4) || (dx == -3 && dy is >= -2 and <= 1),
                        "STATUS_SLOW" => Math.Abs(dx) <= 1 || Math.Abs(dy) <= 1,
                        "STATUS_TAUNT" => (Math.Abs(dx) <= 1 && dy >= -4) || (dy == 4 && Math.Abs(dx) <= 1),
                        "STATUS_BARRIER" => Math.Abs(dx) + Math.Abs(dy) is >= 4 and <= 6,
                        "STATUS_BLESSING" => Math.Abs(dx) <= 1 || Math.Abs(dy) <= 1,
                        _ => false
                    };
                    return mark ? light : baseColor;
                }
                return clear;
            }

            bool monster = name.StartsWith("MONSTER_", StringComparison.Ordinal);
            bool head = dx * dx + (dy - 3) * (dy - 3) <= (monster ? 15 : 10);
            bool body = Math.Abs(dx) <= (monster ? 5 : 4) && dy is >= -5 and <= 2;
            bool legs = dy is >= -7 and <= -4 && (dx is >= -4 and <= -2 || dx is >= 2 and <= 4);
            bool outlineBody = Math.Abs(dx) <= (monster ? 6 : 5) && dy is >= -7 and <= 4;
            if (monster && dy >= 2 && (Math.Abs(dx) == 5 || Math.Abs(dx) == 6)) return outline;
            if (!head && !body && !legs) return outlineBody ? outline : clear;
            if (monster && dy == 4 && Math.Abs(dx) <= 2) return light;
            if (name == "JOB_GUARDIAN" && x <= 5 && y is >= 4 and <= 11) return light;
            if (name == "JOB_ARCHER" && x >= 11 && Math.Abs((y - 8) * (y - 8) + (x - 9) * (x - 9) - 16) <= 4) return light;
            if (name == "JOB_MAGE" && dy >= 3 && Math.Abs(dx) <= 5 - (dy - 3)) return light;
            if (name == "JOB_CLERIC" && (Math.Abs(dx) <= 1 || Math.Abs(dy) <= 1) && Math.Abs(dx) + Math.Abs(dy) <= 5) return light;
            if (name == "JOB_WARRIOR" && x >= 11 && y is >= 3 and <= 13 && Math.Abs(x - 12) <= 1) return light;
            return (x + y) % 4 == 0 ? light : ((x + y) % 3 == 0 ? dark : baseColor);
        }

        [MenuItem("Kingdom Tycoon/P06/Generate Region Combat Screen")]
        public static void GenerateScreen()
        {
            P06GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P06GeneratedAssetMarker>(MarkerPath);
            if (marker != null && marker.fingerprint == Fingerprint && AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) != null) return;
            AssetDatabase.DeleteAsset(ScreenPath);
            var root = new GameObject("P06RegionCombatRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RegionCombatPresenter));
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 25;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            RectTransform rootRect = root.GetComponent<RectTransform>(); rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f); rootRect.pivot = new Vector2(0.5f, 0.5f); rootRect.sizeDelta = new Vector2(1920, 1080);

            Image background = Image("RegionScreen", root.transform, new Color32(22, 31, 28, 255)); Stretch(background.rectTransform);
            var viewObject = new GameObject("P06RegionView", typeof(RectTransform), typeof(RegionCombatView)); viewObject.transform.SetParent(background.transform, false); Stretch(viewObject.GetComponent<RectTransform>());
            Image content = Image("ContentRoot", viewObject.transform, new Color32(28, 43, 37, 255)); SetRect(content.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 112), new Vector2(0, -112));
            Image top = Image("TopBar", content.transform, new Color32(34, 45, 51, 255)); SetRect(top.rectTransform, new Vector2(0, 1), Vector2.one, Vector2.zero, new Vector2(0, 96));
            Text("P06_UI_REGION_TITLE", top.transform, "왕국 외곽 초원", 38, TextAlignmentOptions.Left, new Vector2(0.02f, 0), new Vector2(0.45f, 1));
            Button pause = Button("P06_UI_PAUSE", top.transform, "일시정지", new Color32(77, 91, 102, 255)); SetButtonRect(pause.GetComponent<RectTransform>(), new Vector2(0.88f, 0.5f), new Vector2(230, 72));
            GameObject offline = Text("P06_UI_OFFLINE_BADGE", top.transform, "오프라인", 24, TextAlignmentOptions.Center, new Vector2(0.68f, 0.16f), new Vector2(0.8f, 0.84f)).gameObject; offline.SetActive(false);

            Image world = Image("WorldViewport", content.transform, new Color32(64, 89, 65, 255)); SetRect(world.rectTransform, new Vector2(0.24f, 0.14f), new Vector2(1, 0.9f), new Vector2(8, 8), new Vector2(-16, -8));
            TMP_Text encounter = Text("P06_UI_ENCOUNTER", world.transform, "왕국 외곽 초원 · 전투 준비", 28, TextAlignmentOptions.Center, new Vector2(0.2f, 0.86f), new Vector2(0.8f, 0.98f));
            Image grid = Image("P06_UI_GRID", world.transform, new Color32(80, 112, 74, 180)); SetRect(grid.rectTransform, new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.84f), Vector2.zero, Vector2.zero);

            Image party = Image("PartyPanel", content.transform, new Color32(31, 42, 49, 255)); SetRect(party.rectTransform, new Vector2(0, 0.14f), new Vector2(0.24f, 0.9f), new Vector2(16, 8), new Vector2(-8, -8));
            TMP_Text partyText = Text("P06_UI_MEMBER_CARD", party.transform, "파티 미편성", 24, TextAlignmentOptions.TopLeft, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f)); partyText.textWrappingMode = TextWrappingModes.Normal;
            Image bottom = Image("BottomBar", content.transform, new Color32(34, 45, 51, 255)); SetRect(bottom.rectTransform, Vector2.zero, new Vector2(1, 0.14f), Vector2.zero, Vector2.zero);
            TMP_Text autonomy = Text("P06_UI_AUTONOMY", bottom.transform, "사냥 준비", 26, TextAlignmentOptions.Left, new Vector2(0.02f, 0.15f), new Vector2(0.68f, 0.85f));
            Button recall = Button("P06_UI_RECALL", bottom.transform, "귀환", new Color32(165, 77, 58, 255)); SetButtonRect(recall.GetComponent<RectTransform>(), new Vector2(0.91f, 0.5f), new Vector2(268, 72));

            Image partyModal = Image("P06_UI_PARTY_MODAL", viewObject.transform, new Color32(26, 35, 43, 248)); SetRect(partyModal.rectTransform, new Vector2(0.29f, 0.2f), new Vector2(0.71f, 0.84f), Vector2.zero, Vector2.zero);
            Text("P06_UI_PARTY_SELECT", partyModal.transform, "사냥 파티", 38, TextAlignmentOptions.Center, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.96f));
            Text("P06_UI_PARTY_CARD", partyModal.transform, "활동 중이며 마을에 있는 용병 최대 4명이 편성됩니다.", 26, TextAlignmentOptions.Center, new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.78f));
            Button start = Button("P06_UI_START", partyModal.transform, "사냥 시작", new Color32(181, 113, 49, 255)); SetButtonRect(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0.165f), new Vector2(404, 96));

            Image recallModal = Image("P06_UI_RECALL_MODAL", viewObject.transform, new Color32(35, 42, 50, 252)); SetRect(recallModal.rectTransform, new Vector2(0.34f, 0.32f), new Vector2(0.66f, 0.7f), Vector2.zero, Vector2.zero); recallModal.gameObject.SetActive(false);
            Text("P06_UI_RECALL_CONFIRM", recallModal.transform, "사냥을 종료하고 귀환합니까?", 32, TextAlignmentOptions.Center, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.8f));
            Image result = Image("P06_UI_RESULT", viewObject.transform, new Color32(31, 42, 49, 252)); SetRect(result.rectTransform, new Vector2(0.25f, 0.12f), new Vector2(0.75f, 0.88f), Vector2.zero, Vector2.zero); result.gameObject.SetActive(false);
            Text("P06_UI_RESULT_TITLE", result.transform, "사냥 결과", 42, TextAlignmentOptions.Center, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.96f));

            Image overlay = Image("P06_UI_STATE_OVERLAY", viewObject.transform, new Color32(18, 25, 32, 250)); Stretch(overlay.rectTransform);
            TMP_Text state = Text("P06_UI_STATE_MESSAGE", overlay.transform, "지역 정보를 불러오는 중입니다.", 34, TextAlignmentOptions.Center, new Vector2(0.18f, 0.35f), new Vector2(0.82f, 0.65f));
            Button stateAction = Button("P06_UI_STATE_ACTION", overlay.transform, "다시 시도", new Color32(181, 113, 49, 255)); SetButtonRect(stateAction.GetComponent<RectTransform>(), new Vector2(0.5f, 0.26f), new Vector2(308, 86));
            viewObject.GetComponent<RegionCombatView>().Configure(start, recall, pause, stateAction, encounter, autonomy, partyText, state, content.gameObject, overlay.gameObject, offline);
            root.GetComponent<RegionCombatPresenter>().Configure(viewObject.GetComponent<RegionCombatView>());
            PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
        }

        [MenuItem("Kingdom Tycoon/P06/Attach Combat Screen To Region")]
        public static void AttachToRegionScene()
        {
            Scene scene = EditorSceneManager.OpenScene(RegionScenePath, OpenSceneMode.Single);
            bool changed = false;
            GameObject[] roots = scene.GetRootGameObjects().Where(value => value.name == "P06RegionCombatRoot").ToArray();
            foreach (GameObject duplicate in roots.Skip(1)) { Object.DestroyImmediate(duplicate); changed = true; }
            if (roots.Length == 0)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P06_REGION_PREFAB_MISSING");
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); instance.name = "P06RegionCombatRoot";
                changed = true;
            }
            // Bootstrap owns the single persistent Input System EventSystem.
            foreach (GameObject eventSystem in scene.GetRootGameObjects().Where(value => value.GetComponent<UnityEngine.EventSystems.EventSystem>() != null).ToArray())
            {
                Object.DestroyImmediate(eventSystem);
                changed = true;
            }
            if (changed) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene, RegionScenePath); }
        }

        private static Image Image(string name, Transform parent, Color color)
        { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); value.GetComponent<Image>().color = color; return value.GetComponent<Image>(); }
        private static TMP_Text Text(string name, Transform parent, string text, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max)
        { var value = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); value.transform.SetParent(parent, false); TMP_Text label = value.GetComponent<TMP_Text>(); label.text = text; label.fontSize = size; label.alignment = alignment; label.color = new Color32(239, 230, 207, 255); label.overflowMode = TextOverflowModes.Overflow; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string text, Color color)
        { Image image = Image(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; RectTransform rect = image.rectTransform; rect.sizeDelta = new Vector2(240, 72); Text("Label", image.transform, text, 25, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetButtonRect(RectTransform value, Vector2 anchor, Vector2 size) { value.anchorMin = anchor; value.anchorMax = anchor; value.anchoredPosition = Vector2.zero; value.sizeDelta = size; }
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offsetMin; value.offsetMax = offsetMax; }
        private static void EnsureFolder(string path)
        { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P06GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P06/Verify Generated Assets")]
        public static void VerifyAll()
        {
            P06CombatSetup.ValidateContent();
            P06GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P06GeneratedAssetMarker>(P06CombatSetup.MarkerPath);
            if (marker == null || marker.fingerprint != P06CombatSetup.Fingerprint) throw new BuildFailedException("P06_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P06CombatSetup.ScreenPath) ?? throw new BuildFailedException("P06_REGION_PREFAB_MISSING");
            if (prefab.GetComponent<RegionCombatPresenter>() == null || prefab.GetComponentInChildren<RegionCombatView>(true) == null) throw new BuildFailedException("P06_REGION_PREFAB_INVALID");
            string[] requiredIds = { "P06_UI_PARTY_MODAL", "P06_UI_MEMBER_CARD", "P06_UI_ENCOUNTER", "P06_UI_AUTONOMY", "P06_UI_RECALL", "P06_UI_RECALL_MODAL", "P06_UI_PAUSE", "P06_UI_RESULT", "P06_UI_STATE_OVERLAY" };
            Transform[] children = prefab.GetComponentsInChildren<Transform>(true);
            foreach (string id in requiredIds) if (children.Count(value => value.name == id) != 1) throw new BuildFailedException("P06_UI_ID_INVALID: " + id);
            foreach (Button button in prefab.GetComponentsInChildren<Button>(true)) if (button.GetComponent<RectTransform>().rect.height < 64f) throw new BuildFailedException("P06_TOUCH_TARGET_INVALID: " + button.name);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("P06_ADDRESSABLES_MISSING");
            string[] addresses = { "P06/Characters/JOB_WARRIOR", "P06/Characters/JOB_GUARDIAN", "P06/Characters/JOB_ARCHER", "P06/Characters/JOB_MAGE", "P06/Characters/JOB_CLERIC", "P06/Status/STATUS_POISON", "P06/Status/STATUS_BURN", "P06/Status/STATUS_SLOW", "P06/Status/STATUS_TAUNT", "P06/Status/STATUS_BARRIER", "P06/Status/STATUS_BLESSING" };
            foreach (string address in addresses) if (settings.groups.Where(group => group != null).SelectMany(group => group.entries).Count(entry => entry.address == address) != 1) throw new BuildFailedException("P06_ADDRESS_INVALID: " + address);
            Scene scene = EditorSceneManager.OpenScene(P06CombatSetup.RegionScenePath, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Count(value => value.name == "P06RegionCombatRoot") != 1) throw new BuildFailedException("P06_REGION_SCENE_INVALID");
        }
    }

    public static class P06AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P06/Build Android Development APK")]
        public static void Build()
        {
            P06GeneratedAssetVerifier.VerifyAll();
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P06-Development.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P06 Android output directory is invalid."));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            var options = new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P06 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"P06_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    public static class P06CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P06/Generate Acceptance Captures")]
        public static void Run()
        {
            P06GeneratedAssetVerifier.VerifyAll();
            Scene scene = EditorSceneManager.OpenScene(P06CombatSetup.RegionScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().Single(value => value.name == "P06RegionCombatRoot");
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            GameObject partyModal = Find(nodes, "P06_UI_PARTY_MODAL");
            GameObject recallModal = Find(nodes, "P06_UI_RECALL_MODAL");
            GameObject result = Find(nodes, "P06_UI_RESULT");
            GameObject overlay = Find(nodes, "P06_UI_STATE_OVERLAY");
            GameObject offline = Find(nodes, "P06_UI_OFFLINE_BADGE");
            Button start = Find(nodes, "P06_UI_START").GetComponent<Button>();
            Button recall = Find(nodes, "P06_UI_RECALL").GetComponent<Button>();
            TMP_Text encounter = Find(nodes, "P06_UI_ENCOUNTER").GetComponent<TMP_Text>();
            TMP_Text autonomy = Find(nodes, "P06_UI_AUTONOMY").GetComponent<TMP_Text>();
            TMP_Text members = Find(nodes, "P06_UI_MEMBER_CARD").GetComponent<TMP_Text>();
            TMP_Text state = Find(nodes, "P06_UI_STATE_MESSAGE").GetComponent<TMP_Text>();
            TMP_Text pause = Find(nodes, "P06_UI_PAUSE").GetComponentInChildren<TMP_Text>();

            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P06"));
            Directory.CreateDirectory(output);
            SetBase(partyModal, recallModal, result, overlay, offline, start, recall, encounter, autonomy, members, pause);
            Capture(root, output, "p06_01_region_start.png", 1920, 1080);
            partyModal.SetActive(false); start.gameObject.SetActive(false); recall.gameObject.SetActive(true);
            encounter.text = "왕국 외곽 초원 · 경로 탐색 · 진행 32"; autonomy.text = "적 탐색 · 적 발견";
            Capture(root, output, "p06_02_path.png", 1920, 1080);
            encounter.text = "왕국 외곽 초원 · 전투 1 · 적 4"; autonomy.text = "전투 중 · 적 발견 · 진행 87";
            members.text = "0001  생명력 182/220  기여 96\n0002  생명력 240/260  기여 42\n0003  생명력 138/160  기여 121\n0004  생명력 150/170  기여 28";
            Capture(root, output, "p06_03_combat.png", 1920, 1080);
            autonomy.text = "전투 · 도발 · 위협 대상 고정"; members.text = "가디언  생명력 211/260  방벽 · 도발\n전사  생명력 182/220  기여 128\n궁수  생명력 138/160  기여 154\n성직자  생명력 150/170  기여 36";
            Capture(root, output, "p06_04_taunt.png", 1920, 1080);
            autonomy.text = "전투 · 회복 · 유효 회복 48"; members.text = "성직자  생명력 150/170  회복 48\n가디언  생명력 259/260  방벽\n전사  생명력 204/220  기여 168\n궁수  생명력 142/160  기여 181";
            Capture(root, output, "p06_05_heal.png", 1920, 1080);
            recallModal.SetActive(true); autonomy.text = "RETURN_TOWN · PLAYER_RECALL";
            Capture(root, output, "p06_06_recall.png", 1920, 1080);
            recallModal.SetActive(false); overlay.SetActive(true); state.text = "전투 정보를 표시할 수 없습니다.\nP06_CONTENT_MISSING";
            Capture(root, output, "p06_07_error.png", 1920, 1080);
            overlay.SetActive(false); offline.SetActive(true); autonomy.text = "오프라인 · 기기 저장 상태 표시";
            Capture(root, output, "p06_08_20x9.png", 2400, 1080);
            Debug.Log($"P06_CAPTURES={output}; count=8");
        }

        private static void SetBase(GameObject partyModal, GameObject recallModal, GameObject result, GameObject overlay, GameObject offline, Button start, Button recall, TMP_Text encounter, TMP_Text autonomy, TMP_Text members, TMP_Text pause)
        {
            partyModal.SetActive(true); recallModal.SetActive(false); result.SetActive(false); overlay.SetActive(false); offline.SetActive(false);
            start.gameObject.SetActive(true); recall.gameObject.SetActive(false); pause.text = "일시정지";
            encounter.text = "왕국 외곽 초원 · 전투 준비"; autonomy.text = "활동 용병 1~4명을 선택해 사냥을 시작하세요."; members.text = "파티 미편성";
        }

        private static GameObject Find(IEnumerable<Transform> nodes, string name) => nodes.Single(value => value.name == name).gameObject;

        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            Canvas canvas = root.GetComponent<Canvas>();
            var cameraObject = new GameObject("P06CaptureCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(11, 16, 18, 255); camera.orthographic = true; camera.nearClipPlane = 0.1f; camera.farClipPlane = 100f;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases(); camera.Render();
            RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false);
            File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            string path = Path.Combine(directory, filename);
            if (!File.Exists(path) || new FileInfo(path).Length <= 1024) throw new BuildFailedException("P06_CAPTURE_FAILED: " + filename);
        }
    }

    internal sealed class P06CombatBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 60;
        public void OnPreprocessBuild(BuildReport report) => P06GeneratedAssetVerifier.VerifyAll();
    }
}
