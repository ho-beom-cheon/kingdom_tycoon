using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Platform.Android;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomTycoon.Editor
{
    public static class P17ReleaseReadinessSetup
    {
        public const string Version = "1.0.0-rc.1";
        public const int VersionCode = 1000001;
        public const string AndroidApplicationIdentifier = "com.kingdomtycoon.game";
        public const long MaxApkBytes = 80L * 1024L * 1024L;
        public const string ApkName = "KingdomTycoon-1.0.0-rc.1.apk";
        private const string KoreanFontPath = "Assets/KingdomTycoon/ContentGenerated/P05Mercenary/Fonts/P05NotoSansKR.asset";
        private static readonly string[] ForbiddenPlayerTokens = { "KINGDOM OPERATIONS", "RELEASE 1.0", "CONTENT.", "SAVE.", "OFFLINE 8H", "REGION_R", " tick", "PLACEHOLDER" };

        [MenuItem("Kingdom Tycoon/P17/Run Release Readiness Setup")]
        public static void Run()
        {
            P04KingdomAssetGenerator.GenerateAndVerify();
            P06CombatSetup.Run();
            // P04 rebuilds the base Kingdom scene. Reintegrate the already
            // generated later-phase screens without regenerating their prefabs,
            // which would churn Unity file IDs on every release setup run.
            P08StoreSetup.IntegrateKingdomScene();
            P08StoreSetup.IntegrateBootstrapScene();
            P09ProductionSetup.IntegrateBootstrapScene();
            P10EquipmentGrowthSetup.IntegrateBootstrapScene();
            P11ProgressionSetup.IntegrateBootstrapScene();
            P12RegionMapSetup.IntegrateBootstrapScene();
            P13RecruitmentSetup.IntegrateBootstrapScene();
            P14RaidSetup.IntegrateBootstrapScene();
            P15OfflineTutorialSetup.Run();
            ConfigureKoreanAndroidAppInfo();
            Verify();
            Debug.Log("P17_RELEASE_READINESS_SETUP_COMPLETED");
        }

        [MenuItem("Kingdom Tycoon/P17/Verify Release Readiness")]
        public static void Verify()
        {
            P04KingdomAssetGenerator.Verify();
            P06GeneratedAssetVerifier.VerifyAll();
            P15GeneratedAssetVerifier.Verify();
            VerifyFonts();
            VerifyKoreanPlayerText();
            VerifyReleaseArt();
            VerifyAssetRegister();
            VerifyAndroidConfiguration(false);
            Debug.Log("P17_RELEASE_READINESS_VERIFIED");
        }

        public static void ConfigureKoreanAndroidAppInfo()
        {
            LocalizationSettings settings = LocalizationSettings.Instance;
            if (settings == null) throw new BuildFailedException("P17_LOCALIZATION_SETTINGS_MISSING");
            AppInfo appInfo = LocalizationSettings.Metadata.GetMetadata<AppInfo>();
            if (appInfo == null)
            {
                appInfo = new AppInfo();
                LocalizationSettings.Metadata.AddMetadata(appInfo);
            }
            appInfo.DisplayName = new LocalizedString("UI", "screen.kingdom.title");
            PlayerSettings.productName = "킹덤 타이쿤";
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureAndroidRc()
        {
            ConfigureKoreanAndroidAppInfo();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidApplicationIdentifier);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High);
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
        }

        public static void VerifyAndroidConfiguration(bool requireRcVersion)
        {
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                throw new BuildFailedException("P17_ANDROID_BACKEND_INVALID");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                throw new BuildFailedException("P17_ANDROID_ARCHITECTURE_INVALID");
            if (requireRcVersion && PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != AndroidApplicationIdentifier)
                throw new BuildFailedException("P17_ANDROID_APPLICATION_IDENTIFIER_INVALID");
            if (requireRcVersion && (PlayerSettings.bundleVersion != Version || PlayerSettings.Android.bundleVersionCode != VersionCode))
                throw new BuildFailedException("P17_ANDROID_VERSION_INVALID");
            if (requireRcVersion && (EditorUserBuildSettings.development || EditorUserBuildSettings.allowDebugging))
                throw new BuildFailedException("P17_ANDROID_DEVELOPMENT_FLAG_INVALID");
        }

        private static void VerifyFonts()
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (font == null) throw new BuildFailedException("P17_KOREAN_FONT_MISSING");
            var missing = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/KingdomTycoon/ContentGenerated" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                missing.AddRange(prefab.GetComponentsInChildren<TMP_Text>(true).Where(value => value.font == null).Select(value => path + ":" + value.name));
            }
            if (missing.Count > 0) throw new BuildFailedException("P17_FONT_ASSET_MISSING: " + string.Join(",", missing));
        }

        private static void VerifyKoreanPlayerText()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P15OfflineTutorialSetup.ScreenPath) ?? throw new BuildFailedException("P17_REPORT_PREFAB_MISSING");
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                string plain = Regex.Replace(text.text ?? string.Empty, "<[^>]+>", string.Empty);
                string forbidden = ForbiddenPlayerTokens.FirstOrDefault(token => plain.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (forbidden != null) throw new BuildFailedException($"P17_PLAYER_TEXT_NOT_KOREAN: {text.name}:{forbidden}");
            }
        }

        private static void VerifyReleaseArt()
        {
            string[] facilityAssets = AssetDatabase.FindAssets("ASSET_FAC_ t:Texture2D", new[] { "Assets/KingdomTycoon/ContentGenerated/Kingdom/P04/Sprites" });
            if (facilityAssets.Length < 8) throw new BuildFailedException("P17_FACILITY_ART_MISSING");
            string[] facilityHashes = facilityAssets.Select(guid => AssetDatabase.GetAssetDependencyHash(AssetDatabase.GUIDToAssetPath(guid)).ToString()).Distinct(StringComparer.Ordinal).ToArray();
            if (facilityHashes.Length < 8) throw new BuildFailedException("P17_FACILITY_ART_NOT_DISTINCT");
            var combatHashes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in new[] { "JOB_WARRIOR", "JOB_GUARDIAN", "JOB_ARCHER", "JOB_MAGE", "JOB_CLERIC", "MONSTER_MELEE", "MONSTER_ELITE" })
            {
                string path = $"Assets/KingdomTycoon/ContentGenerated/P06Combat/Sprites/{name}.asset";
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null || new FileInfo(Path.GetFullPath(path)).Length < 2500) throw new BuildFailedException("P17_COMBAT_ART_MISSING: " + name);
                combatHashes.Add(AssetDatabase.GetAssetDependencyHash(path).ToString());
            }
            if (combatHashes.Count < 7) throw new BuildFailedException("P17_COMBAT_ART_NOT_DISTINCT");
        }

        private static void VerifyAssetRegister()
        {
            string path = Path.Combine(UnityEngine.Application.streamingAssetsPath, "Content", "1.0.0-content.13", "asset_register.csv");
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2) throw new BuildFailedException("P17_ASSET_REGISTER_EMPTY");
            string[] headers = lines[0].Split(',');
            int creator = Array.IndexOf(headers, "creator");
            int price = Array.IndexOf(headers, "price_krw");
            int license = Array.IndexOf(headers, "license");
            int commercial = Array.IndexOf(headers, "commercial_use");
            int modification = Array.IndexOf(headers, "modification_allowed");
            int status = Array.IndexOf(headers, "status");
            int enabled = Array.IndexOf(headers, "enabled");
            long total = 0;
            foreach (string line in lines.Skip(1))
            {
                string[] cells = line.Split(',');
                if (cells[enabled] != "TRUE") continue;
                if (string.IsNullOrWhiteSpace(cells[creator]) || string.IsNullOrWhiteSpace(cells[license]) || cells[commercial] != "TRUE" || cells[modification] != "TRUE" || cells[status] != "CONFIRMED" && cells[status] != "TUNABLE")
                    throw new BuildFailedException("P17_ASSET_LICENSE_INVALID: " + cells[0]);
                if (!long.TryParse(cells[price], out long value)) throw new BuildFailedException("P17_ASSET_PRICE_INVALID: " + cells[0]);
                total += value;
            }
            if (total != 0) throw new BuildFailedException("P17_ASSET_SPEND_NOT_ZERO: " + total);
        }
    }

    public static class P17CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P17/Generate Release Captures")]
        public static void Run()
        {
            P17ReleaseReadinessSetup.Verify();
            P15CaptureGenerator.Run();
            string source = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P15"));
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P17"));
            Directory.CreateDirectory(output);
            Copy(source, output, "p15_01_new_install.png", "p17_01_report_16x9.png");
            Copy(source, output, "p15_04_tutorial_active.png", "p17_02_process_target_16x9.png");
            Copy(source, output, "p15_05_tutorial_complete.png", "p17_03_process_complete_16x9.png");
            Copy(source, output, "p15_07_20x9.png", "p17_04_report_20x9.png");

            Scene scene = EditorSceneManager.OpenScene(P04KingdomAssetGenerator.ScenePath, OpenSceneMode.Single);
            Canvas canvas = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<Canvas>(true)).Single(value => value.name == "WorldCanvas");
            PrepareKingdomReleaseCapture(canvas);
            CaptureCanvas(canvas, Path.Combine(output, "p17_05_kingdom_release_art_16x9.png"), 1920, 1080);
            CaptureCanvas(canvas, Path.Combine(output, "p17_06_kingdom_release_art_20x9.png"), 2400, 1080);
            Debug.Log($"P17_CAPTURES={output}; count=6");
        }

        private static void PrepareKingdomReleaseCapture(Canvas canvas)
        {
            Transform[] nodes = canvas.GetComponentsInChildren<Transform>(true);
            Transform drawer = nodes.FirstOrDefault(value => value.name == "FacilityDrawer");
            if (drawer != null) drawer.gameObject.SetActive(false);
            string[] names = { "선술집", "용병 숙소", "길드 회관", "상점", "대장간", "연금 공방", "창고", "치료소" };
            string[] ids = { "FAC_TAVERN", "FAC_LODGE", "FAC_GUILD", "FAC_STORE", "FAC_BLACKSMITH", "FAC_ALCHEMY", "FAC_WAREHOUSE", "FAC_INFIRMARY" };
            for (int index = 0; index < ids.Length; index++)
            {
                Transform facility = nodes.First(value => value.name == ids[index]);
                Transform overlay = facility.Find("StateOverlay"); if (overlay != null) overlay.gameObject.SetActive(false);
                Transform stopped = facility.Find("StoppedIcon"); if (stopped != null) stopped.gameObject.SetActive(false);
                TMP_Text label = facility.Find("Label")?.GetComponent<TMP_Text>(); if (label != null) label.text = names[index];
            }
            TMP_Text stage = nodes.First(value => value.name == "StageLabel").GetComponent<TMP_Text>(); stage.text = "왕국 1단계";
            TMP_Text status = nodes.First(value => value.name == "StatusLabel").GetComponent<TMP_Text>(); status.text = "왕국 운영 준비 완료";
            TMP_Text gold = nodes.First(value => value.name == "GoldLabel").GetComponent<TMP_Text>(); gold.text = "왕국 골드 12,450";
        }

        private static void Copy(string source, string output, string from, string to)
        {
            string path = Path.Combine(source, from);
            if (!File.Exists(path)) throw new BuildFailedException("P17_CAPTURE_SOURCE_MISSING: " + from);
            File.Copy(path, Path.Combine(output, to), true);
        }

        private static void CaptureCanvas(Canvas canvas, string path, int width, int height)
        {
            RectTransform rect = canvas.GetComponent<RectTransform>();
            Vector2 previousSize = rect.sizeDelta;
            Vector3 previousScale = rect.localScale;
            Vector3 previousPosition = rect.localPosition;
            Quaternion previousRotation = rect.localRotation;
            RenderMode previousMode = canvas.renderMode;
            Camera previousCamera = canvas.worldCamera;
            rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one; rect.localPosition = Vector3.zero; rect.localRotation = Quaternion.identity;
            var cameraObject = new GameObject("P17CaptureCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(8, 16, 20, 255); camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100f; camera.aspect = width / (float)height; camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target; GL.Clear(true, true, camera.backgroundColor); camera.Render();
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false);
            Color32[] capturePixels = texture.GetPixels32(); int stride = Math.Max(1, capturePixels.Length / 4096); int distinct = capturePixels.Where((_, index) => index % stride == 0).Select(value => (value.r, value.g, value.b, value.a)).Distinct().Take(16).Count(); File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = previousMode; canvas.worldCamera = previousCamera; rect.sizeDelta = previousSize; rect.localScale = previousScale; rect.localPosition = previousPosition; rect.localRotation = previousRotation;
            if (new FileInfo(path).Length <= 20000 || distinct < 8) throw new BuildFailedException("P17_CAPTURE_FAILED: " + Path.GetFileName(path));
        }
    }

    public static class P17AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P17/Build Android RC APK")]
        public static void Build()
        {
            P17ReleaseReadinessSetup.Verify();
            P17ReleaseReadinessSetup.ConfigureAndroidRc();
            P17ReleaseReadinessSetup.VerifyAndroidConfiguration(true);
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", P17ReleaseReadinessSetup.ApkName));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new BuildFailedException("P17_ANDROID_OUTPUT_INVALID"));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.CleanBuildCache
            });
            var file = new FileInfo(output);
            if (report.summary.result != BuildResult.Succeeded || !file.Exists) throw new BuildFailedException($"P17 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            if (file.Length > P17ReleaseReadinessSetup.MaxApkBytes) throw new BuildFailedException($"P17_ANDROID_APK_TOO_LARGE: {file.Length}");
            Debug.Log($"P17_ANDROID_RC={output}; bytes={file.Length}; development={EditorUserBuildSettings.development}; version={PlayerSettings.bundleVersion}; code={PlayerSettings.Android.bundleVersionCode}");
        }
    }
}
