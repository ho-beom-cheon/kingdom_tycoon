using System;
using System.Collections.Generic;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomTycoon.Editor
{
    public static class P02ProjectSetup
    {
        private const string ProjectRoot = "Assets/KingdomTycoon";
        private const string ConfigRoot = ProjectRoot + "/Config";
        private const string LocalizationRoot = ConfigRoot + "/Localization";
        private const string ScenesRoot = ProjectRoot + "/Scenes";
        private const string InputActionsPath = ConfigRoot + "/KingdomTycoon.inputactions";

        public static void Configure()
        {
            ConfigureProjectSettings();
            EnsureProjectFolders();
            InputActionAsset inputActions = MoveTemplateInputActions();
            ConfigureAddressables();
            ConfigureLocalization();
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            CreateScenes(inputActions);
            RemoveTemplateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("P02 Unity foundation configured successfully.");
        }

        public static void VerifyAndroidSupport()
        {
            bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            if (!supported)
            {
                throw new InvalidOperationException("Unity Android Build Support is not available.");
            }

            Debug.Log("Unity Android Build Support is available.");
        }

        private static void ConfigureProjectSettings()
        {
            VersionControlSettings.mode = "Visible Meta Files";
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "Kingdom Tycoon";
            PlayerSettings.productName = "Kingdom Tycoon";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
        }

        private static void EnsureProjectFolders()
        {
            EnsureFolder(ProjectRoot);
            EnsureFolder(ConfigRoot);
            EnsureFolder(LocalizationRoot);
            EnsureFolder(ScenesRoot);
        }

        private static InputActionAsset MoveTemplateInputActions()
        {
            const string templatePath = "Assets/InputSystem_Actions.inputactions";
            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath) == null)
            {
                string error = AssetDatabase.MoveAsset(templatePath, InputActionsPath);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException($"Failed to move Input Actions asset: {error}");
                }
            }

            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputActions == null)
            {
                throw new InvalidOperationException("Input Actions asset is missing after project setup.");
            }

            return inputActions;
        }

        private static void ConfigureAddressables()
        {
            if (AddressableAssetSettingsDefaultObject.GetSettings(true) == null)
            {
                throw new InvalidOperationException("Addressables settings could not be created.");
            }
        }

        private static void ConfigureLocalization()
        {
            const string settingsPath = LocalizationRoot + "/LocalizationSettings.asset";
            LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "Kingdom Tycoon Localization Settings";
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = settings;

            Locale korean = CreateLocale(SystemLanguage.Korean, "Korean.asset");
            Locale english = CreateLocale(SystemLanguage.English, "English.asset");
            settings.SetSelectedLocale(korean);

            if (LocalizationEditorSettings.GetStringTableCollection("UI") == null)
            {
                var locales = new List<Locale> { korean, english };
                var collection = LocalizationEditorSettings.CreateStringTableCollection("UI", LocalizationRoot, locales);
                var koreanTable = (StringTable)collection.GetTable(korean.Identifier);
                var englishTable = (StringTable)collection.GetTable(english.Identifier);
                koreanTable.AddEntry("screen.kingdom.title", "왕국 타이쿤");
                englishTable.AddEntry("screen.kingdom.title", "Kingdom Tycoon");
                foreach (var table in collection.StringTables)
                {
                    EditorUtility.SetDirty(table);
                }
            }

            EditorUtility.SetDirty(settings);
        }

        private static Locale CreateLocale(SystemLanguage language, string fileName)
        {
            string path = $"{LocalizationRoot}/{fileName}";
            Locale locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(language);
                AssetDatabase.CreateAsset(locale, path);
            }

            if (LocalizationEditorSettings.GetLocale(locale.Identifier) == null)
            {
                LocalizationEditorSettings.AddLocale(locale);
            }

            return locale;
        }

        private static void CreateScenes(InputActionAsset inputActions)
        {
            CreateBootstrapScene(inputActions);
            CreateContentScene("Kingdom", true);
            CreateContentScene("Region", false);
            CreateContentScene("Raid", false);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{ScenesRoot}/Bootstrap.unity", true),
                new EditorBuildSettingsScene($"{ScenesRoot}/Kingdom.unity", true),
                new EditorBuildSettingsScene($"{ScenesRoot}/Region.unity", true),
                new EditorBuildSettingsScene($"{ScenesRoot}/Raid.unity", true),
            };
        }

        private static void CreateBootstrapScene(InputActionAsset inputActions)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var appRootObject = new GameObject("AppRoot");
            var appRoot = appRootObject.AddComponent<AppRoot>();
            var appRootSerialized = new SerializedObject(appRoot);
            appRootSerialized.FindProperty("inputActions").objectReferenceValue = inputActions;
            appRootSerialized.FindProperty("firstScene").stringValue = "Kingdom";
            appRootSerialized.FindProperty("loadFirstSceneOnStart").boolValue = true;
            appRootSerialized.ApplyModifiedPropertiesWithoutUndo();

            var commonUiObject = new GameObject("CommonUiRoot", typeof(RectTransform), typeof(CommonUiRoot));
            commonUiObject.transform.SetParent(appRootObject.transform, false);

            for (int index = 0; index < CommonUiRoot.RequiredLayers.Length; index++)
            {
                CreateCanvasLayer(CommonUiRoot.RequiredLayers[index], commonUiObject.transform, index * 100);
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(appRootObject.transform, false);
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

            EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/Bootstrap.unity");
        }

        private static void CreateContentScene(string sceneName, bool createSampleScreen)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sceneObject = new GameObject($"{sceneName}Scene", typeof(SceneIdentity));
            sceneObject.GetComponent<SceneIdentity>().SetSceneId(sceneName.ToLowerInvariant());

            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(PixelPerfectCamera));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.08f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            PixelPerfectCamera pixelPerfectCamera = cameraObject.GetComponent<PixelPerfectCamera>();
            pixelPerfectCamera.assetsPPU = 32;
            pixelPerfectCamera.refResolutionX = 1920;
            pixelPerfectCamera.refResolutionY = 1080;

            if (createSampleScreen)
            {
                CreateKingdomSampleScreen(sceneObject.transform);
            }

            EditorSceneManager.SaveScene(scene, $"{ScenesRoot}/{sceneName}.unity");
        }

        private static void CreateKingdomSampleScreen(Transform parent)
        {
            var screenObject = new GameObject("SampleKingdomScreen", typeof(SampleKingdomScreen));
            screenObject.transform.SetParent(parent, false);

            GameObject canvasObject = CreateCanvasLayer("SampleScreenCanvas", screenObject.transform, 50);
            Transform safeArea = canvasObject.transform.Find("SafeArea");

            var panelObject = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(safeArea, false);
            Stretch(panelObject.GetComponent<RectTransform>());
            panelObject.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.13f, 0.96f);

            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(panelObject.transform, false);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.35f);
            titleRect.anchorMax = new Vector2(0.9f, 0.65f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            TextMeshProUGUI title = titleObject.GetComponent<TextMeshProUGUI>();
            title.text = "왕국 타이쿤";
            title.fontSize = 64f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.95f, 0.82f, 0.42f, 1f);
        }

        private static GameObject CreateCanvasLayer(string name, Transform parent, int sortingOrder)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            Stretch(safeAreaObject.GetComponent<RectTransform>());
            return canvasObject;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static void RemoveTemplateScene()
        {
            const string sampleScenePath = "Assets/Scenes/SampleScene.unity";
            if (AssetDatabase.LoadAssetAtPath<Object>(sampleScenePath) != null)
            {
                AssetDatabase.DeleteAsset(sampleScenePath);
            }

            if (AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.DeleteAsset("Assets/Scenes");
            }

            if (AssetDatabase.IsValidFolder("Assets/Editor"))
            {
                AssetDatabase.DeleteAsset("Assets/Editor");
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path[..path.LastIndexOf('/')];
            string name = path[(path.LastIndexOf('/') + 1)..];
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
