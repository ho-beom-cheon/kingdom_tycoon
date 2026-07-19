using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.Inventory;
using KingdomTycoon.Presentation.Navigation;
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
    public static class P07InventorySetup
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P07Inventory";
        public const string ScreenPath = GeneratedRoot + "/InventoryScreen.prefab";
        public const string MarkerPath = GeneratedRoot + "/P07Inventory.marker.asset";
        public const string InventoryScenePath = "Assets/KingdomTycoon/Scenes/Inventory.unity";
        public const string Fingerprint = "p07-inventory-v1.1-grid-policy-potion-safearea";
        private const string GroupName = "Content-P07-Inventory-v1";

        private static readonly (string Name, string Address, Color Color)[] Assets = BuildAssets();

        [MenuItem("Kingdom Tycoon/P07/Run Complete Setup")]
        public static void Run()
        {
            ValidateContent(); GenerateAssets(); GenerateScreen(); GenerateScene();
            P07GeneratedAssetVerifier.VerifyAll();
            Debug.Log("P07 inventory setup completed.");
        }

        [MenuItem("Kingdom Tycoon/P07/Validate Content Package .5")]
        public static void ValidateContent()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.5");
            string manifest = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifest)) throw new BuildFailedException("P07_CONTENT_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifest), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 68)
                throw new BuildFailedException("P07_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        public static void GenerateAssets()
        {
            EnsureFolder(GeneratedRoot); EnsureFolder(GeneratedRoot + "/Sprites");
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("P07_ADDRESSABLES_MISSING");
            AddressableAssetGroup group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            foreach ((string name, string address, Color color) in Assets)
            {
                string path = GeneratedRoot + "/Sprites/" + name + ".asset";
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    texture.SetPixels(Enumerable.Repeat(color, 1024).ToArray()); texture.Apply(false, true); AssetDatabase.CreateAsset(texture, path);
                }
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                entry.address = address; entry.SetLabel("P07-Inventory", true, true);
            }
            P07GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P07GeneratedAssetMarker>(MarkerPath);
            if (marker == null) { marker = ScriptableObject.CreateInstance<P07GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void GenerateScreen()
        {
            AssetDatabase.DeleteAsset(ScreenPath);
            var root = new GameObject("P07InventoryRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(InventoryScreenPresenter));
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080));
            Image background = Image("InventoryScreen", root.transform, new Color32(18, 24, 31, 255)); Stretch(background.rectTransform);
            var viewObject = new GameObject("P07InventoryView", typeof(RectTransform), typeof(InventoryScreenView)); viewObject.transform.SetParent(background.transform, false); Stretch(viewObject.GetComponent<RectTransform>());
            Image safe = Image("SafeArea", viewObject.transform, new Color32(18, 24, 31, 255)); SetRect(safe.rectTransform, Vector2.zero, Vector2.one, new Vector2(48, 32), new Vector2(-48, -32));
            Image top = Image("TopBar", safe.transform, new Color32(31, 41, 52, 255)); SetRect(top.rectTransform, new Vector2(0, .9f), Vector2.one, Vector2.zero, Vector2.zero);
            Text("Title", top.transform, "왕국 창고 · 인벤토리", 40, TextAlignmentOptions.Left, new Vector2(.025f, .15f), new Vector2(.45f, .88f));
            TMP_Text capacity = Text("Capacity", top.transform, "재료 4/24   장비 3/16   포션 2/8", 24, TextAlignmentOptions.Right, new Vector2(.5f, .2f), new Vector2(.97f, .82f));
            Button kingdom = Button("P07_RETURN_KINGDOM", top.transform, "왕국으로", new Color32(72, 88, 102, 255)); SetAt(kingdom.GetComponent<RectTransform>(), new Vector2(.91f, .5f), new Vector2(210, 68));
            kingdom.gameObject.AddComponent<SceneNavigationButton>().Configure(kingdom, "Kingdom", false);
            SetRect(capacity.rectTransform, new Vector2(.47f, .2f), new Vector2(.83f, .82f), Vector2.zero, Vector2.zero);

            Image content = Image("ContentRoot", safe.transform, new Color32(22, 30, 38, 255)); SetRect(content.rectTransform, Vector2.zero, new Vector2(1, .9f), Vector2.zero, Vector2.zero);
            Image toolbar = Image("Toolbar", content.transform, new Color32(27, 37, 47, 255)); SetRect(toolbar.rectTransform, new Vector2(0, .9f), Vector2.one, Vector2.zero, Vector2.zero);
            Button category = Button("P07_UI_CATEGORY", toolbar.transform, "전체 · 재료", new Color32(62, 81, 96, 255)); SetAt(category.GetComponent<RectTransform>(), new Vector2(.1f, .5f), new Vector2(300, 72));
            Button sort = Button("P07_UI_SORT", toolbar.transform, "품질 높은 순", new Color32(62, 81, 96, 255)); SetAt(sort.GetComponent<RectTransform>(), new Vector2(.28f, .5f), new Vector2(260, 72));
            Button policy = Button("P07_UI_POLICY_BUTTON", toolbar.transform, "자동 판매 정책", new Color32(159, 105, 48, 255)); SetAt(policy.GetComponent<RectTransform>(), new Vector2(.9f, .5f), new Vector2(300, 72));

            Image gridPanel = Image("P07_UI_GRID", content.transform, new Color32(26, 35, 43, 255)); SetRect(gridPanel.rectTransform, new Vector2(0, 0), new Vector2(.64f, .9f), new Vector2(0, 0), new Vector2(-10, 0));
            for (int index = 0; index < 12; index++)
            {
                int column = index % 3; int row = index / 3;
                Image card = Image("ItemCard_" + index.ToString("00"), gridPanel.transform, index == 0 ? new Color32(129, 86, 46, 255) : new Color32(43, 57, 68, 255));
                SetRect(card.rectTransform, new Vector2(.03f + column * .32f, .73f - row * .23f), new Vector2(.31f + column * .32f, .94f - row * .23f), Vector2.zero, Vector2.zero);
                Text("Icon", card.transform, index % 4 == 0 ? "무" : index % 4 == 1 ? "재" : index % 4 == 2 ? "특" : "물", 34, TextAlignmentOptions.Center, new Vector2(.04f, .35f), new Vector2(.3f, .94f));
                Text("Name", card.transform, index == 0 ? "정교한 사냥 활\n희귀 · 1단계 · 잠금" : $"왕국 재료 {index + 1}\n{index + 2}개", 21, TextAlignmentOptions.Left, new Vector2(.32f, .12f), new Vector2(.96f, .9f));
            }
            TMP_Text gridText = Text("GridData", gridPanel.transform, string.Empty, 1, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(.01f, .01f));

            Image drawer = Image("P07_UI_ITEM_DETAIL", content.transform, new Color32(31, 41, 52, 255)); SetRect(drawer.rectTransform, new Vector2(.64f, 0), new Vector2(1, .9f), new Vector2(10, 0), Vector2.zero);
            Text("DetailTitle", drawer.transform, "정교한 사냥 활", 34, TextAlignmentOptions.Left, new Vector2(.06f, .86f), new Vector2(.92f, .97f));
            Text("Quality", drawer.transform, "희귀 · 무기 · 활 · 1단계 · 잠금", 23, TextAlignmentOptions.Left, new Vector2(.06f, .79f), new Vector2(.94f, .87f));
            Image compare = Image("P07_UI_COMPARE", drawer.transform, new Color32(23, 31, 39, 255)); SetRect(compare.rectTransform, new Vector2(.05f, .31f), new Vector2(.95f, .77f), Vector2.zero, Vector2.zero);
            TMP_Text detail = Text("CompareData", compare.transform, "장비 점수  18,420  (+2,640)\n\n공격력       123  →  218   +95\n치명타       2,561 → 3,061  +500\n방어력       38   →  38       0\n이동 속도    7,959 → 7,959    0", 24, TextAlignmentOptions.TopLeft, new Vector2(.06f, .08f), new Vector2(.94f, .93f));
            Button equip = Button("P07_UI_EQUIP", drawer.transform, "장착", new Color32(57, 127, 88, 255)); SetAt(equip.GetComponent<RectTransform>(), new Vector2(.18f, .12f), new Vector2(180, 72));
            Button unequip = Button("P07_UI_UNEQUIP", drawer.transform, "해제", new Color32(72, 88, 102, 255)); SetAt(unequip.GetComponent<RectTransform>(), new Vector2(.5f, .12f), new Vector2(180, 72));
            Button sell = Button("P07_UI_SELL", drawer.transform, "판매", new Color32(157, 74, 61, 255)); SetAt(sell.GetComponent<RectTransform>(), new Vector2(.82f, .12f), new Vector2(180, 72));

            Image potion = Image("P07_UI_POTION_PANEL", drawer.transform, new Color32(29, 42, 50, 255)); SetRect(potion.rectTransform, new Vector2(.04f, .18f), new Vector2(.96f, .78f), Vector2.zero, Vector2.zero); potion.gameObject.SetActive(false);
            Text("PotionText", potion.transform, "소형 회복 포션  창고 3\n배정 대상: 전사 르온  2/4", 24, TextAlignmentOptions.TopLeft, new Vector2(.08f, .52f), new Vector2(.92f, .92f));
            Button transfer = Button("P07_UI_TRANSFER", potion.transform, "용병에게 배정", new Color32(57, 127, 88, 255)); SetAt(transfer.GetComponent<RectTransform>(), new Vector2(.28f, .2f), new Vector2(260, 72));
            Button back = Button("P07_UI_RETURN", potion.transform, "창고로 회수", new Color32(72, 88, 102, 255)); SetAt(back.GetComponent<RectTransform>(), new Vector2(.72f, .2f), new Vector2(260, 72));

            Image saleModal = Modal("P07_UI_SELL_MODAL", viewObject.transform, new Vector2(.34f, .3f), new Vector2(.66f, .7f), "판매 확인", "왕국 재료 2개를 판매합니다.\n개인 골드 +12"); saleModal.gameObject.SetActive(false);
            Image policyModal = Modal("P07_UI_POLICY_MODAL", viewObject.transform, new Vector2(.29f, .16f), new Vector2(.71f, .84f), "자동 판매 · 보호 정책", "자동 장착   켜짐\n자동 판매   켜짐 · 최대 1단계\n교체 기준   +5%\n\n보호 품질   희귀 / 유산 / 유물\n보스 장비 보호   켜짐\n첫 발견 보호      켜짐"); policyModal.gameObject.SetActive(false);
            Image lootModal = Modal("P07_UI_OVERFLOW", viewObject.transform, new Vector2(.25f, .14f), new Vector2(.75f, .86f), "사냥 전리품", "보관  재료 2 · 장비 1\n자동 장착  성직자 무기\n자동 판매  +28 개인 골드\n\n용량 초과 폐기  0"); lootModal.gameObject.SetActive(false);
            Image state = Modal("P07_UI_STATE", viewObject.transform, new Vector2(.18f, .18f), new Vector2(.82f, .82f), "인벤토리", "인벤토리를 불러오는 중입니다."); state.gameObject.SetActive(false);
            TMP_Text stateText = state.transform.Find("Body").GetComponent<TMP_Text>();

            InventoryScreenView view = viewObject.GetComponent<InventoryScreenView>();
            view.Configure(capacity, gridText, detail, stateText, content.gameObject, state.gameObject, policyModal.gameObject, saleModal.gameObject, lootModal.gameObject, policy, sell);
            root.GetComponent<InventoryScreenPresenter>().Configure(view);
            PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
        }

        public static void GenerateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P07_UI_PREFAB_MISSING");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); instance.name = "P07InventoryRoot";
            EditorSceneManager.SaveScene(scene, InventoryScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(value => value.path != InventoryScenePath)) scenes.Add(new EditorBuildSettingsScene(InventoryScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static (string, string, Color)[] BuildAssets()
        {
            var result = new List<(string, string, Color)>();
            string[] items = { "ALCHEMY", "BOSS", "CRAFT", "ENHANCE", "MAGIC", "MONSTER", "ORE", "PROMOTION", "REFINE", "RELIC" };
            for (int index = 0; index < items.Length; index++) result.Add(("ITEM_" + items[index], "P07/Items/" + items[index], Color.HSVToRGB(index / 10f, .55f, .82f)));
            string[] slots = { "WEAPON", "ARMOR", "HELMET", "ACCESSORY" };
            foreach (string value in slots) result.Add(("SLOT_" + value, "P07/Slots/" + value, new Color32(105, 132, 155, 255)));
            for (int tier = 1; tier <= 5; tier++) result.Add(("TIER_" + tier, "P07/Tier/" + tier, new Color32((byte)(90 + tier * 22), (byte)(80 + tier * 18), 62, 255)));
            string[] qualities = { "QUALITY_COMMON", "QUALITY_FINE", "QUALITY_RARE", "QUALITY_LEGACY", "QUALITY_RELIC" };
            Color[] qualityColors = { new Color32(140, 145, 150, 255), new Color32(85, 170, 105, 255), new Color32(70, 125, 220, 255), new Color32(165, 80, 210, 255), new Color32(235, 165, 55, 255) };
            for (int index = 0; index < qualities.Length; index++) result.Add((qualities[index], "P07/Quality/" + qualities[index], qualityColors[index]));
            result.Add(("STATE_EMPTY", "P07/State/EMPTY", new Color32(76, 87, 98, 255)));
            result.Add(("STATE_ERROR", "P07/State/ERROR", new Color32(180, 70, 62, 255)));
            result.Add(("STATE_LOCKED", "P07/State/LOCKED", new Color32(154, 112, 58, 255)));
            return result.ToArray();
        }

        private static Image Modal(string name, Transform parent, Vector2 min, Vector2 max, string title, string body)
        {
            Image panel = Image(name, parent, new Color32(31, 41, 52, 252)); SetRect(panel.rectTransform, min, max, Vector2.zero, Vector2.zero);
            Text("Title", panel.transform, title, 38, TextAlignmentOptions.Center, new Vector2(.08f, .82f), new Vector2(.92f, .96f));
            Text("Body", panel.transform, body, 27, TextAlignmentOptions.TopLeft, new Vector2(.09f, .2f), new Vector2(.91f, .78f));
            Button close = Button("Close", panel.transform, "닫기", new Color32(72, 88, 102, 255)); SetAt(close.GetComponent<RectTransform>(), new Vector2(.5f, .1f), new Vector2(220, 72));
            close.onClick.AddListener(() => panel.gameObject.SetActive(false)); return panel;
        }

        private static Image Image(string name, Transform parent, Color color)
        { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); value.GetComponent<Image>().color = color; return value.GetComponent<Image>(); }
        private static TMP_Text Text(string name, Transform parent, string text, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max)
        { var value = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); value.transform.SetParent(parent, false); TMP_Text label = value.GetComponent<TMP_Text>(); label.text = text; label.fontSize = size; label.alignment = alignment; label.color = new Color32(238, 231, 214, 255); label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string text, Color color)
        { Image image = Image(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; Text("Label", image.transform, text, 24, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void SetAt(RectTransform value, Vector2 anchor, Vector2 size) { value.anchorMin = anchor; value.anchorMax = anchor; value.anchoredPosition = Vector2.zero; value.sizeDelta = size; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offset, Vector2 size)
        { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offset; value.offsetMax = offset; if (min == max) { value.anchoredPosition = offset; value.sizeDelta = size; } }
        private static void EnsureFolder(string path)
        { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P07GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P07/Verify Generated Assets")]
        public static void VerifyAll()
        {
            P07InventorySetup.ValidateContent();
            P07GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P07GeneratedAssetMarker>(P07InventorySetup.MarkerPath);
            if (marker == null || marker.fingerprint != P07InventorySetup.Fingerprint) throw new BuildFailedException("P07_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P07InventorySetup.ScreenPath) ?? throw new BuildFailedException("P07_UI_PREFAB_MISSING");
            string[] required = { "P07_UI_GRID", "P07_UI_CATEGORY", "P07_UI_SORT", "P07_UI_ITEM_DETAIL", "P07_UI_COMPARE", "P07_UI_EQUIP", "P07_UI_UNEQUIP", "P07_UI_SELL", "P07_UI_SELL_MODAL", "P07_UI_POLICY_MODAL", "P07_UI_POTION_PANEL", "P07_UI_TRANSFER", "P07_UI_RETURN", "P07_UI_OVERFLOW", "P07_UI_STATE" };
            Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true);
            foreach (string id in required) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P07_UI_ID_INVALID: " + id);
            foreach (Button button in prefab.GetComponentsInChildren<Button>(true)) if (button.GetComponent<RectTransform>().rect.height < 64f) throw new BuildFailedException("P07_TOUCH_TARGET_INVALID: " + button.name);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("P07_ADDRESSABLES_MISSING");
            foreach (string address in settings.groups.Where(value => value != null).SelectMany(value => value.entries).Where(value => value.address.StartsWith("P07/", StringComparison.Ordinal)).Select(value => value.address))
                if (settings.groups.Where(value => value != null).SelectMany(value => value.entries).Count(value => value.address == address) != 1) throw new BuildFailedException("P07_ADDRESS_DUPLICATE: " + address);
            int count = settings.groups.Where(value => value != null).SelectMany(value => value.entries).Count(value => value.address.StartsWith("P07/", StringComparison.Ordinal));
            if (count != 27) throw new BuildFailedException("P07_ADDRESS_COUNT_INVALID: " + count);
            Scene scene = EditorSceneManager.OpenScene(P07InventorySetup.InventoryScenePath, OpenSceneMode.Single);
            if (scene.GetRootGameObjects().Count(value => value.name == "P07InventoryRoot") != 1) throw new BuildFailedException("P07_SCENE_INVALID");
        }
    }

    public static class P07AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P07/Build Android Development APK")]
        public static void Build()
        {
            P07GeneratedAssetVerifier.VerifyAll();
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P07-Development.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P07 Android output invalid."));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            var options = new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P07 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"P07_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    public static class P07CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P07/Generate Acceptance Captures")]
        public static void Run()
        {
            P07GeneratedAssetVerifier.VerifyAll();
            Scene scene = EditorSceneManager.OpenScene(P07InventorySetup.InventoryScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().Single(value => value.name == "P07InventoryRoot");
            PrepareRoot(root);
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            GameObject potion = Find(nodes, "P07_UI_POTION_PANEL"), policy = Find(nodes, "P07_UI_POLICY_MODAL"), sale = Find(nodes, "P07_UI_SELL_MODAL"), loot = Find(nodes, "P07_UI_OVERFLOW"), state = Find(nodes, "P07_UI_STATE");
            TMP_Text detail = Find(nodes, "CompareData").GetComponent<TMP_Text>(); TMP_Text stateBody = state.transform.Find("Body").GetComponent<TMP_Text>();
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P07")); Directory.CreateDirectory(output);
            Hide(potion, policy, sale, loot, state); Capture(root, output, "p07_01_items.png", 1920, 1080);
            detail.text = "희귀 · 무기 · 활 · 1단계 · 잠금\n장착 슬롯  무기\n품질 배율  1.18\n강화  +0"; Capture(root, output, "p07_02_equipment.png", 1920, 1080);
            detail.text = "장비 점수  18,420  (+2,640)\n\n공격력  123 → 218   +95\n치명타  2,561 → 3,061   +500\n최대 체력  909 → 909   0"; Capture(root, output, "p07_03_compare.png", 1920, 1080);
            detail.text = "자동 장착 완료\n성직자 · WEAPON\n기존  없음\n신규  수습 성직자 철퇴\n교체 점수 +6,100"; Capture(root, output, "p07_04_auto_equip.png", 1920, 1080);
            policy.SetActive(true); Capture(root, output, "p07_05_policy.png", 1920, 1080); policy.SetActive(false);
            sale.SetActive(true); Capture(root, output, "p07_06_sale_confirm.png", 1920, 1080); sale.SetActive(false);
            loot.SetActive(true); Capture(root, output, "p07_07_loot.png", 1920, 1080); loot.SetActive(false);
            detail.text = "보관 중인 아이템이 없습니다.\n사냥을 완료하거나 포션을 창고로 옮겨보세요."; Capture(root, output, "p07_08_empty.png", 1920, 1080);
            state.SetActive(true); stateBody.text = "인벤토리를 표시할 수 없습니다.\nP07_CONTENT_MISSING\n\n다시 시도"; Capture(root, output, "p07_09_error.png", 1920, 1080); state.SetActive(false);
            detail.text = "SAFE AREA · 20:9\n긴 화면에서도 상세 패널과 버튼이 안전 영역 안에 유지됩니다."; Capture(root, output, "p07_10_20x9.png", 2400, 1080);
            Debug.Log($"P07_CAPTURES={output}; count=10");
        }

        private static void Hide(params GameObject[] values) { foreach (GameObject value in values) value.SetActive(false); }
        private static GameObject Find(IEnumerable<Transform> nodes, string name) => nodes.Single(value => value.name == name).gameObject;
        private static void PrepareRoot(GameObject root)
        {
            root.SetActive(true);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1920, 1080);
        }

        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            Canvas canvas = root.GetComponent<Canvas>(); var cameraObject = new GameObject("P07CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(10, 14, 18, 255); camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false); File.WriteAllBytes(Path.Combine(directory, filename), texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            if (new FileInfo(Path.Combine(directory, filename)).Length <= 1024) throw new BuildFailedException("P07_CAPTURE_FAILED: " + filename);
        }
    }

    internal sealed class P07InventoryBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 70;
        public void OnPreprocessBuild(BuildReport report) => P07GeneratedAssetVerifier.VerifyAll();
    }
}
