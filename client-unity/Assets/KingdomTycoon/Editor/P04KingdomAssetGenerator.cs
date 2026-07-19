using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.Kingdom;
using KingdomTycoon.Presentation.Kingdom.Views;
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
    public static class P04KingdomAssetGenerator
    {
        public const string GroupName = "Content-P04-Kingdom-v1";
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/Kingdom/P04";
        public const string ScenePath = "Assets/KingdomTycoon/Scenes/Kingdom.unity";
        private const string SpriteRoot = GeneratedRoot + "/Sprites";
        private const string PrefabRoot = GeneratedRoot + "/Prefabs";
        private static readonly FacilityVisualSpec[] Facilities =
        {
            new("FAC_TAVERN", "ASSET_FAC_TAVERN_PLACEHOLDER_V1", "P04/Kingdom/FAC_TAVERN", new Color32(164, 91, 57, 255), 0.18f, 0.72f, 20),
            new("FAC_LODGE", "ASSET_FAC_LODGE_PLACEHOLDER_V1", "P04/Kingdom/FAC_LODGE", new Color32(82, 126, 86, 255), 0.39f, 0.76f, 21),
            new("FAC_GUILD", "ASSET_FAC_GUILD_PLACEHOLDER_V1", "P04/Kingdom/FAC_GUILD", new Color32(99, 102, 151, 255), 0.62f, 0.72f, 22),
            new("FAC_STORE", "ASSET_FAC_STORE_PLACEHOLDER_V1", "P04/Kingdom/FAC_STORE", new Color32(194, 143, 65, 255), 0.82f, 0.70f, 23),
            new("FAC_BLACKSMITH", "ASSET_FAC_BLACKSMITH_PLACEHOLDER_V1", "P04/Kingdom/FAC_BLACKSMITH", new Color32(109, 96, 90, 255), 0.20f, 0.35f, 24),
            new("FAC_ALCHEMY", "ASSET_FAC_ALCHEMY_PLACEHOLDER_V1", "P04/Kingdom/FAC_ALCHEMY", new Color32(112, 76, 145, 255), 0.42f, 0.30f, 25),
            new("FAC_WAREHOUSE", "ASSET_FAC_WAREHOUSE_PLACEHOLDER_V1", "P04/Kingdom/FAC_WAREHOUSE", new Color32(116, 91, 62, 255), 0.64f, 0.35f, 26),
            new("FAC_INFIRMARY", "ASSET_FAC_INFIRMARY_PLACEHOLDER_V1", "P04/Kingdom/FAC_INFIRMARY", new Color32(157, 80, 89, 255), 0.82f, 0.30f, 27)
        };

        [MenuItem("Kingdom Tycoon/P04/Generate Kingdom Facilities")]
        public static void GenerateAndVerify()
        {
            EnsureFolders(SpriteRoot);
            EnsureFolders(PrefabRoot);
            EnsureSortingLayer("KingdomWorld");
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true) ?? throw new InvalidOperationException("Addressables settings are missing.");
            AddressableAssetGroup group = EnsureGroup(settings);
            RemoveManagedEntries(settings);

            Sprite background = GenerateSprite("ASSET_KINGDOM_BACKGROUND_V1", 1920, 1080, new Vector2(0.5f, 0.5f), BackgroundPixel);
            Sprite plot = GenerateSprite("ASSET_FACILITY_PLOT_V1", 256, 192, new Vector2(0.5f, 0.5f), (x, y) => EllipsePixel(x, y, 256, 192, new Color32(62, 71, 57, 210)));
            Sprite locked = GenerateSprite("ASSET_FACILITY_LOCKED_V1", 256, 256, new Vector2(0.5f, 0.5f), LockedPixel);
            Sprite construction = GenerateSprite("ASSET_FACILITY_CONSTRUCTION_V1", 256, 256, new Vector2(0.5f, 0.5f), ConstructionPixel);
            Sprite stopped = GenerateSprite("ASSET_FACILITY_STOPPED_V1", 64, 64, new Vector2(0.5f, 0.5f), StoppedPixel);
            Register(settings, group, SpritePath("ASSET_KINGDOM_BACKGROUND_V1"), "P04/Kingdom/Background");
            Register(settings, group, SpritePath("ASSET_FACILITY_PLOT_V1"), "P04/Kingdom/Plot");
            Register(settings, group, SpritePath("ASSET_FACILITY_LOCKED_V1"), "P04/Kingdom/Locked");
            Register(settings, group, SpritePath("ASSET_FACILITY_CONSTRUCTION_V1"), "P04/Kingdom/Construction");
            Register(settings, group, SpritePath("ASSET_FACILITY_STOPPED_V1"), "P04/Kingdom/Stopped");

            foreach (FacilityVisualSpec spec in Facilities)
            {
                Sprite baseSprite = GenerateSprite(spec.AssetId, 256, 256, new Vector2(0.5f, 0.1f), (x, y) => FacilityPixel(x, y, spec.Color, spec.FacilityId));
                CreateFacilityPrefab(spec, baseSprite, locked, construction, stopped);
                Register(settings, group, PrefabPath(spec), spec.Address);
            }

            CreateKingdomScene(background, plot);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Verify();
            Debug.Log("P04 Kingdom facilities generated and verified.");
        }

        [MenuItem("Kingdom Tycoon/P04/Verify Kingdom Facilities")]
        public static void Verify()
        {
            VerifyContentPackage();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("CONTENT_P04_ADDRESSABLES_MISSING");
            Dictionary<string, AddressableAssetEntry[]> byAddress = settings.groups.Where(value => value != null).SelectMany(value => value.entries)
                .Where(value => value.address.StartsWith("P04/Kingdom/", StringComparison.Ordinal))
                .GroupBy(value => value.address, StringComparer.Ordinal).ToDictionary(value => value.Key, value => value.ToArray(), StringComparer.Ordinal);
            foreach (FacilityVisualSpec spec in Facilities)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(spec));
                if (prefab == null || prefab.GetComponent<FacilityWorldView>() == null) throw new BuildFailedException("CONTENT_P04_PREFAB_MISSING: " + spec.FacilityId);
                string[] children = { "BaseSprite", "StateOverlay", "StoppedIcon", "Label", "HitTarget" };
                if (children.Any(name => prefab.transform.Find(name) == null)) throw new BuildFailedException("CONTENT_P04_PREFAB_HIERARCHY_INVALID: " + spec.FacilityId);
                if (!byAddress.TryGetValue(spec.Address, out AddressableAssetEntry[] entries) || entries.Length != 1 || entries[0].AssetPath != PrefabPath(spec))
                    throw new BuildFailedException("CONTENT_P04_ADDRESSABLE_MISMATCH: " + spec.Address);
            }
            foreach (string address in new[] { "P04/Kingdom/Background", "P04/Kingdom/Plot", "P04/Kingdom/Locked", "P04/Kingdom/Construction", "P04/Kingdom/Stopped" })
                if (!byAddress.TryGetValue(address, out AddressableAssetEntry[] entries) || entries.Length != 1) throw new BuildFailedException("CONTENT_P04_ADDRESSABLE_MISMATCH: " + address);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            KingdomScreenPresenter presenter = Object.FindFirstObjectByType<KingdomScreenPresenter>();
            FacilityWorldView[] views = Object.FindObjectsByType<FacilityWorldView>(FindObjectsSortMode.None);
            if (!scene.IsValid() || presenter == null || views.Length != 8 || views.Select(value => value.FacilityId).Distinct(StringComparer.Ordinal).Count() != 8)
                throw new BuildFailedException("CONTENT_P04_KINGDOM_SCENE_INVALID");
        }

        private static void CreateFacilityPrefab(FacilityVisualSpec spec, Sprite sprite, Sprite locked, Sprite construction, Sprite stopped)
        {
            string path = PrefabPath(spec);
            AssetDatabase.DeleteAsset(path);
            var root = new GameObject("FacilityWorldView", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(FacilityWorldView));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(346f, 281f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingLayerName = "KingdomWorld";
            canvas.sortingOrder = spec.SortingOrder;
            root.GetComponent<GraphicRaycaster>().blockingObjects = GraphicRaycaster.BlockingObjects.None;
            Image baseImage = CreateImage("BaseSprite", root.transform, sprite, false);
            Image overlay = CreateImage("StateOverlay", root.transform, locked, false);
            Image stoppedImage = CreateImage("StoppedIcon", root.transform, stopped, false);
            RectTransform stoppedRect = stoppedImage.rectTransform;
            stoppedRect.anchorMin = stoppedRect.anchorMax = Vector2.one;
            stoppedRect.pivot = Vector2.one;
            stoppedRect.sizeDelta = new Vector2(64f, 64f);
            stoppedRect.anchoredPosition = new Vector2(-8f, -8f);
            TMP_Text label = CreateText("Label", root.transform, string.Empty, 28f, TextAlignmentOptions.Center);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f); labelRect.anchorMax = new Vector2(1f, 0f); labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.offsetMin = new Vector2(0f, -58f); labelRect.offsetMax = new Vector2(0f, -6f);
            GameObject hit = new("HitTarget", typeof(RectTransform), typeof(Image), typeof(Button));
            hit.transform.SetParent(root.transform, false);
            Stretch(hit.GetComponent<RectTransform>());
            Image hitImage = hit.GetComponent<Image>(); hitImage.color = Color.clear; hitImage.raycastTarget = true;
            Button button = hit.GetComponent<Button>(); button.transition = Selectable.Transition.ColorTint; button.navigation = new Navigation { mode = Navigation.Mode.None };
            root.GetComponent<FacilityWorldView>().Configure(spec.FacilityId, baseImage, overlay, stoppedImage, label, button, locked, construction, stopped);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateKingdomScene(Sprite background, Sprite plot)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sceneRoot = new GameObject("KingdomScene", typeof(SceneIdentity));
            sceneRoot.GetComponent<SceneIdentity>().SetSceneId("kingdom");
            var screen = new GameObject("KingdomScreen", typeof(RectTransform), typeof(SampleKingdomScreen), typeof(KingdomScreenPresenter));
            screen.transform.SetParent(sceneRoot.transform, false);
            var canvasObject = new GameObject("WorldCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(screen.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform), typeof(KingdomTycoon.Presentation.Kingdom.SafeAreaFitter));
            safeAreaObject.transform.SetParent(canvasObject.transform, false); Stretch(safeAreaObject.GetComponent<RectTransform>());
            Transform safeArea = safeAreaObject.transform;
            Image backgroundImage = CreateImage("KingdomBackground", safeArea, background, false); backgroundImage.color = Color.white;
            Transform plotLayer = new GameObject("FacilityPlotLayer", typeof(RectTransform)).transform; plotLayer.SetParent(safeArea, false); Stretch((RectTransform)plotLayer);
            var views = new List<FacilityWorldView>();
            foreach (FacilityVisualSpec spec in Facilities)
            {
                Image plotImage = CreateImage("Plot_" + spec.FacilityId, plotLayer, plot, false);
                Place(plotImage.rectTransform, spec.AnchorX, spec.AnchorY, 346f, 207f);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(spec));
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, safeArea);
                instance.name = spec.FacilityId;
                Place(instance.GetComponent<RectTransform>(), spec.AnchorX, spec.AnchorY, 346f, 281f);
                views.Add(instance.GetComponent<FacilityWorldView>());
            }

            Image topBar = CreatePanel("TopHud", safeArea, new Color32(20, 28, 35, 230));
            RectTransform topRect = topBar.rectTransform; topRect.anchorMin = new Vector2(0f, 1f); topRect.anchorMax = Vector2.one; topRect.pivot = new Vector2(0.5f, 1f); topRect.sizeDelta = new Vector2(0f, 88f);
            TMP_Text stage = CreateText("StageLabel", topBar.transform, "왕국", 30f, TextAlignmentOptions.Left); SetAnchors(stage.rectTransform, new Vector2(0.03f, 0f), new Vector2(0.28f, 1f));
            TMP_Text status = CreateText("StatusLabel", topBar.transform, "불러오는 중", 23f, TextAlignmentOptions.Center); SetAnchors(status.rectTransform, new Vector2(0.28f, 0f), new Vector2(0.72f, 1f));
            TMP_Text gold = CreateText("GoldLabel", topBar.transform, "골드", 30f, TextAlignmentOptions.Right); SetAnchors(gold.rectTransform, new Vector2(0.72f, 0f), new Vector2(0.97f, 1f));

            FacilityDrawerView drawer = CreateDrawer(safeArea);
            screen.GetComponent<KingdomScreenPresenter>().Configure(stage, gold, status, views.ToArray(), drawer);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static FacilityDrawerView CreateDrawer(Transform parent)
        {
            Image panel = CreatePanel("FacilityDrawer", parent, new Color32(30, 38, 47, 248));
            RectTransform rect = panel.rectTransform; rect.anchorMin = new Vector2(0.64f, 0.08f); rect.anchorMax = new Vector2(0.98f, 0.92f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            FacilityDrawerView drawer = panel.gameObject.AddComponent<FacilityDrawerView>();
            TMP_Text title = CreateText("Title", panel.transform, "시설", 40f, TextAlignmentOptions.Left); SetAnchors(title.rectTransform, new Vector2(0.06f, 0.84f), new Vector2(0.82f, 0.97f));
            Button close = CreateButton("Close", panel.transform, "닫기", new Vector2(0.82f, 0.86f), new Vector2(0.96f, 0.96f), out _);
            TMP_Text state = CreateText("State", panel.transform, string.Empty, 25f, TextAlignmentOptions.Left); SetAnchors(state.rectTransform, new Vector2(0.06f, 0.73f), new Vector2(0.94f, 0.83f));
            TMP_Text effect = CreateText("Effect", panel.transform, string.Empty, 24f, TextAlignmentOptions.TopLeft); SetAnchors(effect.rectTransform, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.71f));
            TMP_Text cost = CreateText("Cost", panel.transform, string.Empty, 23f, TextAlignmentOptions.TopLeft); SetAnchors(cost.rectTransform, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.46f));
            TMP_Text error = CreateText("Error", panel.transform, string.Empty, 20f, TextAlignmentOptions.Center); error.color = new Color32(255, 144, 120, 255); SetAnchors(error.rectTransform, new Vector2(0.06f, 0.16f), new Vector2(0.94f, 0.24f));
            Button primary = CreateButton("PrimaryAction", panel.transform, "액션", new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.15f), out TMP_Text action);
            drawer.Configure(title, state, effect, cost, error, action, primary, close);
            return drawer;
        }

        private static Button CreateButton(string name, Transform parent, string text, Vector2 min, Vector2 max, out TMP_Text label)
        {
            Image image = CreatePanel(name, parent, new Color32(190, 126, 54, 255));
            SetAnchors(image.rectTransform, min, max);
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            label = CreateText("Label", image.transform, text, 25f, TextAlignmentOptions.Center); Stretch(label.rectTransform); label.raycastTarget = false;
            return button;
        }

        private static Sprite GenerateSprite(string assetId, int width, int height, Vector2 pivot, Func<int, int, Color32> pixel)
        {
            string path = SpritePath(assetId); AssetDatabase.DeleteAsset(path);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = assetId + "_Texture", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
            texture.SetPixels32(pixels); texture.Apply(false, true); AssetDatabase.CreateAsset(texture, path);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 100f); sprite.name = assetId;
            AssetDatabase.AddObjectToAsset(sprite, texture); EditorUtility.SetDirty(texture); return sprite;
        }

        private static Color32 BackgroundPixel(int x, int y)
        {
            float t = y / 1079f; byte r = (byte)Mathf.Lerp(30, 84, t); byte g = (byte)Mathf.Lerp(55, 122, t); byte b = (byte)Mathf.Lerp(51, 111, t);
            if (y < 270) { r = (byte)(r * 0.72f); g = (byte)(g * 0.80f); b = (byte)(b * 0.70f); }
            return new Color32(r, g, b, 255);
        }
        private static Color32 FacilityPixel(int x, int y, Color32 color, string facilityId)
        {
            Color32 outline = new(37, 31, 29, 255);
            Color32 shadow = new(43, 39, 35, 190);
            Color32 wallDark = Shade(color, -28);
            Color32 wallLight = Shade(color, 22);
            Color32 roof = Shade(color, -42);
            Color32 roofLight = Shade(color, 8);
            Color32 gold = new(222, 176, 76, 255);

            float dx = (x - 128f) / 112f;
            float dy = (y - 25f) / 23f;
            if (dx * dx + dy * dy <= 1f) return shadow;

            bool bodyOutline = x >= 42 && x <= 214 && y >= 24 && y <= 150;
            bool body = x >= 47 && x <= 209 && y >= 29 && y <= 147;
            bool roofOutline = y >= 132 && y <= 224 && Math.Abs(x - 128) <= (224 - y) * 3 / 2 + 28;
            bool roofInner = y >= 137 && y <= 218 && Math.Abs(x - 128) <= (218 - y) * 3 / 2 + 24;
            if (roofOutline && !roofInner) return outline;
            if (roofInner) return ((x + y) / 12) % 2 == 0 ? roofLight : roof;
            if (bodyOutline && !body) return outline;
            if (body)
            {
                if (x is >= 105 and <= 151 && y <= 96) return x is 109 or 110 or 146 or 147 ? outline : new Color32(67, 48, 35, 255);
                bool window = (x is >= 62 and <= 92 || x is >= 164 and <= 194) && y is >= 82 and <= 119;
                if (window)
                {
                    if (x % 30 is 2 or 3 || y is 84 or 85 or 116 or 117) return outline;
                    return new Color32(241, 196, 92, 255);
                }
                if (y % 18 is 0 or 1) return wallDark;
                if (x < 70 && y > 115) return wallLight;
                if (IsFacilitySymbol(facilityId, x, y)) return gold;
                return color;
            }
            return new Color32(0, 0, 0, 0);
        }

        private static bool IsFacilitySymbol(string id, int x, int y)
        {
            int sx = x - 128;
            int sy = y - 171;
            return id switch
            {
                "FAC_TAVERN" => (sx is >= -17 and <= 8 && sy is >= -14 and <= 10) || (sx is >= 8 and <= 18 && sy is >= -9 and <= 5 && Math.Abs(sx - 8) + Math.Abs(sy + 2) >= 7),
                "FAC_LODGE" => sy is >= -10 and <= 9 && ((sx is >= -20 and <= 20) || sx is >= -24 and <= -18),
                "FAC_GUILD" => Math.Abs(sx) <= 18 && sy <= 15 && sy >= -18 && Math.Abs(sx) <= 22 - Math.Abs(sy + 4) / 2,
                "FAC_STORE" => sy is >= -14 and <= 13 && ((sy >= 5 && Math.Abs(sx) <= 22) || (sy < 5 && ((sx + 24) / 9) % 2 == 0)),
                "FAC_BLACKSMITH" => (sy is >= -5 and <= 5 && sx is >= -23 and <= 18) || (sx is >= -10 and <= 9 && sy is >= -18 and <= 13),
                "FAC_ALCHEMY" => (Math.Abs(sx) <= 7 && sy is >= 5 and <= 18) || (sx * sx + (sy + 6) * (sy + 6) <= 18 * 18 && sy <= 7),
                "FAC_WAREHOUSE" => Math.Abs(sx) <= 22 && Math.Abs(sy) <= 17 && (Math.Abs(sx) >= 17 || Math.Abs(sy) >= 12 || Math.Abs(sx - sy) <= 2 || Math.Abs(sx + sy) <= 2),
                "FAC_INFIRMARY" => (Math.Abs(sx) <= 7 && Math.Abs(sy) <= 22) || (Math.Abs(sy) <= 7 && Math.Abs(sx) <= 22),
                _ => false
            };
        }

        private static Color32 Shade(Color32 color, int amount) => new(
            (byte)Mathf.Clamp(color.r + amount, 0, 255),
            (byte)Mathf.Clamp(color.g + amount, 0, 255),
            (byte)Mathf.Clamp(color.b + amount, 0, 255),
            color.a);
        private static Color32 EllipsePixel(int x, int y, int width, int height, Color32 color)
        {
            float dx = (x - width / 2f) / (width / 2f); float dy = (y - height / 2f) / (height / 2f);
            return dx * dx + dy * dy <= 1f ? color : new Color32(0, 0, 0, 0);
        }
        private static Color32 LockedPixel(int x, int y)
        {
            bool shade = x >= 12 && x <= 244 && y >= 12 && y <= 244;
            bool lockBody = x >= 92 && x <= 164 && y >= 84 && y <= 148;
            bool lockArc = y >= 140 && y <= 196 && Math.Abs((x - 128) * (x - 128) + (y - 148) * (y - 148) - 38 * 38) < 420;
            if (lockBody || lockArc) return new Color32(230, 224, 197, 235);
            return shade ? new Color32(13, 18, 24, 142) : new Color32(0, 0, 0, 0);
        }
        private static Color32 ConstructionPixel(int x, int y)
        {
            if (x < 8 || x > 247 || y < 8 || y > 247) return new Color32(0, 0, 0, 0);
            return ((x + y) / 24) % 2 == 0 ? new Color32(238, 169, 54, 145) : new Color32(50, 43, 35, 120);
        }
        private static Color32 StoppedPixel(int x, int y)
        {
            float dx = x - 31.5f, dy = y - 31.5f; float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance > 30f) return new Color32(0, 0, 0, 0);
            if (distance > 23f || Math.Abs(dx + dy) < 6f) return new Color32(224, 80, 75, 255);
            return new Color32(54, 46, 48, 235);
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, bool raycast)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image)); gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = sprite != null; image.raycastTarget = raycast; Stretch(image.rectTransform); return image;
        }
        private static Image CreatePanel(string name, Transform parent, Color32 color) { Image image = CreateImage(name, parent, null, true); image.color = color; return image; }
        private static TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.alignment = alignment; text.color = new Color32(244, 239, 222, 255); text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/KingdomTycoon/ContentGenerated/P05Mercenary/Fonts/P05NotoSansKR.asset") ?? TMP_Settings.defaultFontAsset;
            return text;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height) { rect.anchorMin = rect.anchorMax = new Vector2(x, y); rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(width, height); }
        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static string SpritePath(string assetId) => SpriteRoot + "/" + assetId + ".asset";
        private static string PrefabPath(FacilityVisualSpec spec) => PrefabRoot + "/" + spec.FacilityId + ".prefab";
        private static void Register(AddressableAssetSettings settings, AddressableAssetGroup group, string path, string address)
        { AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group); entry.address = address; entry.SetLabel("P04-Kingdom", true, true); }
        private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings) => settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        private static void RemoveManagedEntries(AddressableAssetSettings settings)
        {
            foreach (AddressableAssetEntry entry in settings.groups.Where(value => value != null).SelectMany(value => value.entries).Where(value => value.address.StartsWith("P04/Kingdom/", StringComparison.Ordinal)).ToArray()) settings.RemoveAssetEntry(entry.guid);
        }
        private static void EnsureFolders(string path)
        {
            string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; }
        }
        private static void EnsureSortingLayer(string layerName)
        {
            Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").First(); var serialized = new SerializedObject(tagManager); SerializedProperty layers = serialized.FindProperty("m_SortingLayers");
            for (int index = 0; index < layers.arraySize; index++) if (layers.GetArrayElementAtIndex(index).FindPropertyRelative("name").stringValue == layerName) return;
            layers.InsertArrayElementAtIndex(layers.arraySize); SerializedProperty layer = layers.GetArrayElementAtIndex(layers.arraySize - 1); layer.FindPropertyRelative("name").stringValue = layerName; layer.FindPropertyRelative("uniqueID").longValue = 294_041; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void VerifyContentPackage()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.2"); string manifest = File.ReadAllText(Path.Combine(root, "content_manifest.json"));
            ContentImportResult result = new CsvContentImporter().Import(manifest, file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 62) throw new BuildFailedException("CONTENT_P04_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        private sealed class FacilityVisualSpec
        {
            public FacilityVisualSpec(string facilityId, string assetId, string address, Color32 color, float anchorX, float anchorY, int sortingOrder) { FacilityId = facilityId; AssetId = assetId; Address = address; Color = color; AnchorX = anchorX; AnchorY = anchorY; SortingOrder = sortingOrder; }
            public string FacilityId { get; } public string AssetId { get; } public string Address { get; } public Color32 Color { get; } public float AnchorX { get; } public float AnchorY { get; } public int SortingOrder { get; }
        }
    }

    internal sealed class P04KingdomBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -900;
        public void OnPreprocessBuild(BuildReport report) => P04KingdomAssetGenerator.Verify();
    }
}
