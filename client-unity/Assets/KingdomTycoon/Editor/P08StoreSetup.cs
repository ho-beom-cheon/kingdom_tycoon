using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.Navigation;
using KingdomTycoon.Presentation.Kingdom.Views;
using KingdomTycoon.Presentation.Store;
using KingdomTycoon.UI;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
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
    public static class P08StoreSetup
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P08Store";
        public const string ScreenPath = GeneratedRoot + "/Prefabs/P08_STORE_SCREEN.prefab";
        public const string MarkerPath = GeneratedRoot + "/P08Store.marker.asset";
        public const string ScenePath = "Assets/KingdomTycoon/Scenes/Kingdom.unity";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "p08-store-v1-75-tables-24-rows-six-states";
        private const string GroupName = "Content-P08-Store-v1";
        private const string SpriteRoot = GeneratedRoot + "/Sprites";

        private static readonly AssetSpec[] Assets =
        {
            new("ASSET_P08_STORE_PANEL", "P08/Store/Panel", "P08_STORE", new Color32(58, 39, 29, 255), 0),
            new("ASSET_P08_GOLD_PERSONAL", "P08/Currency/PersonalGold", "P08_CURRENCY", new Color32(242, 212, 122, 255), 1),
            new("ASSET_P08_GOLD_KINGDOM", "P08/Currency/KingdomGold", "P08_CURRENCY", new Color32(231, 167, 76, 255), 2),
            new("ASSET_P08_STOCK", "P08/State/Stock", "P08_STATE", new Color32(112, 141, 101, 255), 3),
            new("ASSET_P08_SOLD_OUT", "P08/State/SoldOut", "P08_STATE", new Color32(128, 122, 114, 255), 4),
            new("ASSET_P08_POLICY_LOW", "P08/Policy/LOW", "P08_POLICY", new Color32(104, 138, 102, 255), 5),
            new("ASSET_P08_POLICY_STANDARD", "P08/Policy/STANDARD", "P08_POLICY", new Color32(231, 167, 76, 255), 6),
            new("ASSET_P08_POLICY_HIGH", "P08/Policy/HIGH", "P08_POLICY", new Color32(196, 102, 74, 255), 7),
            new("ASSET_P08_TRANSACTION_IN", "P08/Transaction/In", "P08_TRANSACTION", new Color32(88, 156, 111, 255), 8),
            new("ASSET_P08_TRANSACTION_OUT", "P08/Transaction/Out", "P08_TRANSACTION", new Color32(192, 102, 82, 255), 9),
            new("ASSET_P08_MERCHANT", "P08/Merchant/Portrait", "P08_MERCHANT", new Color32(126, 92, 63, 255), 10),
            new("ASSET_P08_STATE_ERROR", "P08/State/Error", "P08_STATE", new Color32(216, 107, 98, 255), 11)
        };

        [MenuItem("Kingdom Tycoon/P08/Run Complete Setup")]
        public static void Run()
        {
            ValidateContent();
            GenerateAssets();
            GenerateScreen();
            IntegrateBootstrapScene();
            IntegrateKingdomScene();
            P08GeneratedAssetVerifier.Verify();
            Debug.Log("P08 store setup completed.");
        }

        [MenuItem("Kingdom Tycoon/P08/Validate Content Package .6")]
        public static void ValidateContent()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.6");
            string manifest = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifest)) throw new BuildFailedException("P08_CONTENT_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifest), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 75)
                throw new BuildFailedException("P08_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
        }

        public static void GenerateAssets()
        {
            EnsureFolder(SpriteRoot);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("P08_ADDRESSABLES_MISSING");
            AddressableAssetGroup group = settings.FindGroup(GroupName) ?? settings.CreateGroup(GroupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            RemoveManagedEntries(settings);
            foreach (AssetSpec spec in Assets)
            {
                string path = SpriteRoot + "/" + spec.AssetId + ".asset";
                AssetDatabase.DeleteAsset(path);
                Texture2D texture = CreateTexture(spec);
                AssetDatabase.CreateAsset(texture, path);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group);
                entry.address = spec.Address;
                entry.SetLabel("P08_STORE", true, true);
                entry.SetLabel(spec.Label, true, true);
            }
            P08GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P08GeneratedAssetMarker>(MarkerPath);
            if (marker == null)
            {
                marker = ScriptableObject.CreateInstance<P08GeneratedAssetMarker>();
                AssetDatabase.CreateAsset(marker, MarkerPath);
            }
            marker.fingerprint = Fingerprint;
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void GenerateScreen()
        {
            EnsureFolder(GeneratedRoot + "/Prefabs");
            AssetDatabase.DeleteAsset(GeneratedRoot + "/Prefabs/StoreScreen.prefab");
            AssetDatabase.DeleteAsset(ScreenPath);
            var root = new GameObject("P08_STORE_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(StoreScreenPresenter));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1920, 1080));
            CanvasGroup group = root.GetComponent<CanvasGroup>();

            Image dim = Panel("Backdrop", root.transform, new Color32(15, 13, 11, 242)); Stretch(dim.rectTransform);
            var viewObject = new GameObject("StoreView", typeof(RectTransform), typeof(StoreScreenView));
            viewObject.transform.SetParent(dim.transform, false); Stretch(viewObject.GetComponent<RectTransform>());
            Image safe = Panel("SafeArea", viewObject.transform, new Color32(35, 31, 27, 255)); SetRect(safe.rectTransform, Vector2.zero, Vector2.one, new Vector2(46, 30), new Vector2(-46, -30));

            Image header = Panel("P08_STORE_HEADER", safe.transform, new Color32(58, 39, 29, 255)); SetRect(header.rectTransform, new Vector2(0, .88f), Vector2.one, Vector2.zero, Vector2.zero);
            Text("Brand", header.transform, "왕국 상회", 43, TextAlignmentOptions.Left, new Vector2(.035f, .16f), new Vector2(.25f, .9f), new Color32(242, 212, 122, 255));
            Text("Merchant", header.transform, "상인 조합 직영 · 안전 거래", 23, TextAlignmentOptions.Left, new Vector2(.2f, .18f), new Vector2(.47f, .82f), new Color32(197, 185, 169, 255));
            TMP_Text kingdomGold = Text("P08_KINGDOM_GOLD", header.transform, "왕국 금고  0", 27, TextAlignmentOptions.Right, new Vector2(.49f, .16f), new Vector2(.69f, .84f));
            TMP_Text personalGold = Text("PersonalGold", header.transform, "개인 지갑  0", 27, TextAlignmentOptions.Right, new Vector2(.68f, .16f), new Vector2(.86f, .84f));
            Button back = Button("P08_STORE_BACK", header.transform, "닫기", new Color32(83, 68, 57, 255)); SetRect(back.GetComponent<RectTransform>(), new Vector2(.88f, .16f), new Vector2(.975f, .84f), Vector2.zero, Vector2.zero);

            Image selector = Panel("P08_MERC_SELECTOR", safe.transform, new Color32(44, 41, 37, 255)); SetRect(selector.rectTransform, new Vector2(0, .805f), new Vector2(1, .88f), Vector2.zero, Vector2.zero);
            TMP_Text mercenary = Text("Mercenary", selector.transform, "레온 · 선택됨", 25, TextAlignmentOptions.Left, new Vector2(.035f, .08f), new Vector2(.35f, .92f));
            TMP_Text merchant = Text("MerchantStatus", selector.transform, "● 영업 중  상점 2레벨", 23, TextAlignmentOptions.Right, new Vector2(.52f, .08f), new Vector2(.965f, .92f), new Color32(142, 207, 145, 255));

            Image tabs = Panel("P08_CATEGORY_TABS", safe.transform, new Color32(39, 36, 32, 255)); SetRect(tabs.rectTransform, new Vector2(0, .73f), new Vector2(1, .805f), Vector2.zero, Vector2.zero);
            Button buy = Button("P08_TAB_BUY", tabs.transform, "구매", new Color32(126, 83, 42, 255)); SetRect(buy.GetComponent<RectTransform>(), new Vector2(.02f, .05f), new Vector2(.13f, .95f), Vector2.zero, Vector2.zero);
            Button sell = Button("P08_TAB_SELL", tabs.transform, "판매", new Color32(67, 61, 54, 255)); SetRect(sell.GetComponent<RectTransform>(), new Vector2(.14f, .05f), new Vector2(.25f, .95f), Vector2.zero, Vector2.zero);
            Button history = Button("P08_TAB_HISTORY", tabs.transform, "거래 장부", new Color32(67, 61, 54, 255)); SetRect(history.GetComponent<RectTransform>(), new Vector2(.26f, .05f), new Vector2(.4f, .95f), Vector2.zero, Vector2.zero);
            Button filter = Button("P08_FILTER", tabs.transform, "분류: 전체", new Color32(63, 59, 54, 255)); SetRect(filter.GetComponent<RectTransform>(), new Vector2(.63f, .05f), new Vector2(.79f, .95f), Vector2.zero, Vector2.zero);
            Button sort = Button("P08_SORT", tabs.transform, "가격 낮은 순", new Color32(63, 59, 54, 255)); SetRect(sort.GetComponent<RectTransform>(), new Vector2(.8f, .05f), new Vector2(.98f, .95f), Vector2.zero, Vector2.zero);

            Image content = Panel("Content", safe.transform, new Color32(32, 30, 27, 255)); SetRect(content.rectTransform, Vector2.zero, new Vector2(1, .73f), Vector2.zero, Vector2.zero);
            Image listFrame = Panel("P08_PRODUCT_VIEWPORT", content.transform, new Color32(42, 39, 35, 255)); SetRect(listFrame.rectTransform, new Vector2(.02f, .12f), new Vector2(.58f, .98f), Vector2.zero, Vector2.zero);
            listFrame.gameObject.AddComponent<RectMask2D>();
            var scroll = listFrame.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var listObject = new GameObject("P08_PRODUCT_LIST", typeof(RectTransform)); listObject.transform.SetParent(listFrame.transform, false);
            RectTransform listRect = listObject.GetComponent<RectTransform>(); listRect.anchorMin = new Vector2(0, 1); listRect.anchorMax = new Vector2(1, 1); listRect.pivot = new Vector2(.5f, 1); listRect.anchoredPosition = Vector2.zero; listRect.sizeDelta = new Vector2(0, 24 * 122);
            scroll.viewport = listFrame.rectTransform; scroll.content = listRect;
            var pool = new GameObject("P08_PRODUCT_ROW_POOL", typeof(RectTransform), typeof(StoreProductVirtualList)); pool.transform.SetParent(listObject.transform, false); Stretch(pool.GetComponent<RectTransform>());
            var rows = new Button[24]; var rowLabels = new TMP_Text[24];
            for (int index = 0; index < 24; index++)
            {
                Button row = Button("ProductRow_" + index.ToString("00"), pool.transform, string.Empty, new Color32(48, 46, 42, 245));
                RectTransform rect = row.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(.018f, 1); rect.anchorMax = new Vector2(.982f, 1); rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -8 - index * 122); rect.sizeDelta = new Vector2(0, 112);
                TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>(); label.text = $"상품 {index + 1}\n<size=20><color=#B9B0A3>재고 {index + 2}</color></size>  <color=#F2D47A>{(index + 1) * 35:N0} 골드</color>"; label.alignment = TextAlignmentOptions.MidlineLeft; label.margin = new Vector4(28, 6, 20, 6);
                rows[index] = row; rowLabels[index] = label;
            }
            StoreProductVirtualList virtualList = pool.GetComponent<StoreProductVirtualList>(); virtualList.Configure(rows, rowLabels);

            Image detail = Panel("P08_DETAIL_PANEL", content.transform, new Color32(58, 54, 48, 255)); SetRect(detail.rectTransform, new Vector2(.595f, .12f), new Vector2(.98f, .98f), Vector2.zero, Vector2.zero);
            TMP_Text detailTitle = Text("DetailTitle", detail.transform, "소형 회복 물약", 34, TextAlignmentOptions.Left, new Vector2(.06f, .84f), new Vector2(.94f, .96f), new Color32(242, 212, 122, 255));
            Image compare = Panel("P08_COMPARE_PANEL", detail.transform, new Color32(45, 42, 38, 255)); SetRect(compare.rectTransform, new Vector2(.05f, .33f), new Vector2(.95f, .82f), Vector2.zero, Vector2.zero);
            TMP_Text detailBody = Text("DetailBody", compare.transform, "물약 · 왕국 보급\n\n재고  8\n단계  -\n품질  일반\n\n<size=36><color=#F2D47A>35 골드</color></size>\n\n개인 골드에서 결제하고\n왕국 금고로 이전합니다.", 24, TextAlignmentOptions.TopLeft, new Vector2(.07f, .06f), new Vector2(.93f, .94f));
            Button minus = Button("P08_QUANTITY_MINUS", detail.transform, "−", new Color32(74, 68, 60, 255)); SetRect(minus.GetComponent<RectTransform>(), new Vector2(.06f, .21f), new Vector2(.22f, .31f), Vector2.zero, Vector2.zero);
            Text("Quantity", detail.transform, "1", 28, TextAlignmentOptions.Center, new Vector2(.23f, .21f), new Vector2(.38f, .31f));
            Button plus = Button("P08_QUANTITY_PLUS", detail.transform, "+", new Color32(74, 68, 60, 255)); SetRect(plus.GetComponent<RectTransform>(), new Vector2(.39f, .21f), new Vector2(.55f, .31f), Vector2.zero, Vector2.zero);
            Button primary = Button("P08_PRIMARY_ACTION", detail.transform, "구매 견적 보기", new Color32(174, 111, 47, 255)); SetRect(primary.GetComponent<RectTransform>(), new Vector2(.58f, .2f), new Vector2(.95f, .32f), Vector2.zero, Vector2.zero);
            TMP_Text actionLabel = primary.transform.Find("Label").GetComponent<TMP_Text>();
            Button policy = Button("P08_POLICY_CONTROL", detail.transform, "가격 정책  표준", new Color32(78, 68, 54, 255)); SetRect(policy.GetComponent<RectTransform>(), new Vector2(.06f, .06f), new Vector2(.95f, .17f), Vector2.zero, Vector2.zero);
            TMP_Text policyLabel = policy.transform.Find("Label").GetComponent<TMP_Text>();

            Image activityPanel = Panel("P08_ACTIVITY_STRIP", content.transform, new Color32(51, 44, 35, 255)); SetRect(activityPanel.rectTransform, new Vector2(.02f, .015f), new Vector2(.98f, .1f), Vector2.zero, Vector2.zero);
            TMP_Text activity = Text("Activity", activityPanel.transform, "최근 거래 없음 · 장부 검증 완료", 21, TextAlignmentOptions.Left, new Vector2(.025f, .08f), new Vector2(.95f, .92f), new Color32(202, 192, 177, 255));

            Image state = Panel("P08_STATE_PANEL", viewObject.transform, new Color32(42, 37, 32, 252)); SetRect(state.rectTransform, new Vector2(.23f, .25f), new Vector2(.77f, .72f), Vector2.zero, Vector2.zero);
            TMP_Text stateTitle = Text("StateTitle", state.transform, "상점을 준비하고 있습니다", 38, TextAlignmentOptions.Center, new Vector2(.08f, .68f), new Vector2(.92f, .9f), new Color32(242, 212, 122, 255));
            TMP_Text stateBody = Text("StateBody", state.transform, "재고와 장부를 검증하는 중입니다…", 26, TextAlignmentOptions.Center, new Vector2(.1f, .2f), new Vector2(.9f, .65f));

            Image modal = Panel("P08_CONFIRM_MODAL", viewObject.transform, new Color32(55, 46, 38, 255)); SetRect(modal.rectTransform, new Vector2(.33f, .25f), new Vector2(.67f, .75f), Vector2.zero, Vector2.zero);
            Text("ModalTitle", modal.transform, "거래 확인", 38, TextAlignmentOptions.Center, new Vector2(.1f, .78f), new Vector2(.9f, .94f), new Color32(242, 212, 122, 255));
            TMP_Text modalBody = Text("ModalBody", modal.transform, "소형 회복 물약\n\n수량  1\n결제  35 개인 골드\n\n구매 후 왕국 금고로 이전합니다.", 27, TextAlignmentOptions.Center, new Vector2(.08f, .3f), new Vector2(.92f, .76f));
            Button confirm = Button("P08_CONFIRM", modal.transform, "거래 확정", new Color32(173, 111, 47, 255)); SetRect(confirm.GetComponent<RectTransform>(), new Vector2(.08f, .08f), new Vector2(.48f, .25f), Vector2.zero, Vector2.zero);
            Button cancel = Button("P08_CANCEL", modal.transform, "취소", new Color32(82, 73, 65, 255)); SetRect(cancel.GetComponent<RectTransform>(), new Vector2(.52f, .08f), new Vector2(.92f, .25f), Vector2.zero, Vector2.zero);
            StoreTransactionModal transactionModal = modal.gameObject.AddComponent<StoreTransactionModal>(); transactionModal.Configure(modalBody, confirm, cancel); modal.gameObject.SetActive(false);

            Image toast = Panel("P08_RESULT_TOAST", viewObject.transform, new Color32(46, 83, 57, 252)); SetRect(toast.rectTransform, new Vector2(.31f, .05f), new Vector2(.69f, .15f), Vector2.zero, Vector2.zero);
            TMP_Text toastText = Text("ToastText", toast.transform, "거래 완료 · 개인 −35 · 왕국 +35", 24, TextAlignmentOptions.Center, new Vector2(.04f, .08f), new Vector2(.96f, .92f)); toast.gameObject.SetActive(false);
            Image debug = Panel("P08_DEBUG_PANEL", viewObject.transform, new Color32(35, 33, 30, 245)); SetRect(debug.rectTransform, new Vector2(.71f, .46f), new Vector2(.965f, .6f), Vector2.zero, Vector2.zero);
            Text("DebugText", debug.transform, "P08 · content.6\nledger: valid · revision: 0", 18, TextAlignmentOptions.TopLeft, new Vector2(.06f, .12f), new Vector2(.94f, .88f), new Color32(177, 168, 154, 255)); debug.gameObject.SetActive(false);

            var walletObject = new GameObject("WalletRenderer", typeof(StoreWalletRenderer)); walletObject.transform.SetParent(viewObject.transform, false); StoreWalletRenderer wallet = walletObject.GetComponent<StoreWalletRenderer>(); wallet.Configure(kingdomGold, personalGold);
            StoreScreenView view = viewObject.GetComponent<StoreScreenView>();
            view.Configure(group, content.gameObject, state.gameObject, stateTitle, stateBody, merchant, mercenary, detailTitle, detailBody, activity, policyLabel, actionLabel, wallet, virtualList, transactionModal, back, buy, sell, history, primary, policy, toast.gameObject, toastText);
            root.GetComponent<StoreScreenPresenter>().Configure(view);
            PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
        }

        public static void IntegrateKingdomScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject old in scene.GetRootGameObjects().Where(value => value.name.StartsWith("P08_STORE_SCREEN", StringComparison.Ordinal)).ToArray()) Object.DestroyImmediate(old);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P08_UI_PREFAB_MISSING");
            GameObject storeRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); storeRoot.name = "P08_STORE_SCREEN";
            PrepareStoreRoot(storeRoot);
            StoreScreenPresenter presenter = storeRoot.GetComponent<StoreScreenPresenter>(); storeRoot.SetActive(false);
            FacilityDrawerView drawer = Object.FindFirstObjectByType<FacilityDrawerView>(FindObjectsInactive.Include);
            if (drawer == null) throw new BuildFailedException("P08_FACILITY_DRAWER_MISSING");
            Transform oldEntry = drawer.transform.Find("P08_STORE_OPEN_BUTTON"); if (oldEntry != null) Object.DestroyImmediate(oldEntry.gameObject);
            Button open = Button("P08_STORE_OPEN_BUTTON", drawer.transform, "상점 입장", new Color32(174, 111, 47, 255)); SetRect(open.GetComponent<RectTransform>(), new Vector2(.06f, .16f), new Vector2(.94f, .27f), Vector2.zero, Vector2.zero);
            open.gameObject.AddComponent<CanvasGroup>();
            StoreEntryButton entry = open.gameObject.AddComponent<StoreEntryButton>(); entry.Configure(drawer, presenter, open);
            UnityEventTools.AddPersistentListener(open.onClick, entry.OpenStore);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        public static void IntegrateBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            AppRoot appRoot = Object.FindFirstObjectByType<AppRoot>(FindObjectsInactive.Include)
                ?? throw new BuildFailedException("P08_APP_ROOT_MISSING");
            foreach (Transform duplicate in appRoot.transform.Cast<Transform>().Where(value => value.name == "P08_PRESENTATION_CAMERA").Skip(1).ToArray())
                Object.DestroyImmediate(duplicate.gameObject);
            Transform cameraTransform = appRoot.transform.Find("P08_PRESENTATION_CAMERA");
            Camera camera;
            if (cameraTransform == null)
            {
                var cameraObject = new GameObject("P08_PRESENTATION_CAMERA", typeof(Camera));
                cameraObject.transform.SetParent(appRoot.transform, false);
                camera = cameraObject.GetComponent<Camera>();
            }
            else camera = cameraTransform.GetComponent<Camera>() ?? cameraTransform.gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(12, 17, 20, 255);
            camera.cullingMask = 0;
            camera.depth = -100f;
            camera.orthographic = true;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 10f;

            CommonUiRoot common = Object.FindFirstObjectByType<CommonUiRoot>(FindObjectsInactive.Include)
                ?? throw new BuildFailedException("P08_COMMON_UI_ROOT_MISSING");
            Transform hud = common.transform.Find("HudCanvas/SafeArea") ?? throw new BuildFailedException("P08_HUD_SAFE_AREA_MISSING");
            foreach (string name in new[] { "NAV_KINGDOM", "NAV_HUNT" })
            {
                Transform old = hud.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            Button kingdom = Button("NAV_KINGDOM", hud, "왕국", new Color32(88, 104, 80, 255));
            SetRect(kingdom.GetComponent<RectTransform>(), new Vector2(.25f, 0f), new Vector2(.41f, 0f), new Vector2(0f, 16f), new Vector2(0f, 96f));
            kingdom.gameObject.AddComponent<SceneNavigationButton>().Configure(kingdom, "Kingdom", true);
            Button hunt = Button("NAV_HUNT", hud, "사냥터", new Color32(126, 83, 42, 255));
            SetRect(hunt.GetComponent<RectTransform>(), new Vector2(.59f, 0f), new Vector2(.75f, 0f), new Vector2(0f, 16f), new Vector2(0f, 96f));
            hunt.gameObject.AddComponent<SceneNavigationButton>().Configure(hunt, "Region", false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        internal static IReadOnlyList<string> ExpectedAddresses => Assets.Select(value => value.Address).ToArray();

        internal static void PrepareStoreRoot(GameObject root)
        {
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1920, 1080);
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }

        private static Texture2D CreateTexture(AssetSpec spec)
        {
            const int size = 64; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = spec.AssetId, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                bool border = x < 3 || y < 3 || x >= size - 3 || y >= size - 3;
                bool diagonal = ((x + y + spec.Pattern * 7) % 19) < 2;
                Color32 baseColor = border ? new Color32(31, 25, 21, 255) : spec.Color;
                if (diagonal && !border) baseColor = Lerp(baseColor, new Color32(242, 212, 122, 255), .18f);
                pixels[y * size + x] = baseColor;
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        private static Color32 Lerp(Color32 a, Color32 b, float t) => new((byte)Mathf.Lerp(a.r, b.r, t), (byte)Mathf.Lerp(a.g, b.g, t), (byte)Mathf.Lerp(a.b, b.b, t), 255);
        private static Image Panel(string name, Transform parent, Color color) { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); Image image = value.GetComponent<Image>(); image.color = color; return image; }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color? color = null)
        { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text label = go.GetComponent<TMP_Text>(); label.text = value; label.fontSize = size; label.alignment = alignment; label.color = color ?? new Color32(238, 231, 214, 255); label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string text, Color color)
        { Image image = Panel(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; Text("Label", image.transform, text, 24, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offsetMin; value.offsetMax = offsetMax; }
        private static void EnsureFolder(string path)
        { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
        private static void RemoveManagedEntries(AddressableAssetSettings settings)
        { foreach (AddressableAssetEntry entry in settings.groups.Where(value => value != null).SelectMany(value => value.entries).Where(value => value.address.StartsWith("P08/", StringComparison.Ordinal)).ToArray()) settings.RemoveAssetEntry(entry.guid); }

        private sealed class AssetSpec
        {
            public AssetSpec(string assetId, string address, string label, Color32 color, int pattern) { AssetId = assetId; Address = address; Label = label; Color = color; Pattern = pattern; }
            public string AssetId { get; } public string Address { get; } public string Label { get; } public Color32 Color { get; } public int Pattern { get; }
        }
    }

    public static class P08GeneratedAssetVerifier
    {
        private static readonly string[] RequiredIds =
        {
            "P08_STORE_SCREEN", "P08_STORE_HEADER", "P08_STORE_BACK", "P08_KINGDOM_GOLD", "P08_MERC_SELECTOR", "P08_CATEGORY_TABS",
            "P08_TAB_BUY", "P08_TAB_SELL", "P08_TAB_HISTORY", "P08_FILTER", "P08_SORT", "P08_PRODUCT_VIEWPORT", "P08_PRODUCT_LIST",
            "P08_PRODUCT_ROW_POOL", "P08_DETAIL_PANEL", "P08_COMPARE_PANEL", "P08_QUANTITY_MINUS", "P08_QUANTITY_PLUS", "P08_PRIMARY_ACTION",
            "P08_POLICY_CONTROL", "P08_ACTIVITY_STRIP", "P08_STATE_PANEL", "P08_CONFIRM_MODAL", "P08_CONFIRM", "P08_CANCEL", "P08_RESULT_TOAST", "P08_DEBUG_PANEL"
        };

        [MenuItem("Kingdom Tycoon/P08/Verify Generated Assets")]
        public static void Verify()
        {
            P08StoreSetup.ValidateContent();
            P08GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P08GeneratedAssetMarker>(P08StoreSetup.MarkerPath);
            if (marker == null || marker.fingerprint != P08StoreSetup.Fingerprint) throw new BuildFailedException("P08_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P08StoreSetup.ScreenPath) ?? throw new BuildFailedException("P08_UI_PREFAB_MISSING");
            Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true);
            foreach (string id in RequiredIds) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P08_UI_ID_INVALID: " + id);
            StoreProductVirtualList list = prefab.GetComponentInChildren<StoreProductVirtualList>(true);
            if (list == null || list.PoolSize != 24) throw new BuildFailedException("P08_ROW_POOL_INVALID");
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("P08_ADDRESSABLES_MISSING");
            AddressableAssetEntry[] entries = settings.groups.Where(value => value != null).SelectMany(value => value.entries).Where(value => value.address.StartsWith("P08/", StringComparison.Ordinal)).ToArray();
            if (entries.Length != 12) throw new BuildFailedException("P08_ADDRESS_COUNT_INVALID: " + entries.Length);
            foreach (string address in P08StoreSetup.ExpectedAddresses) if (entries.Count(value => value.address == address) != 1) throw new BuildFailedException("P08_ADDRESS_INVALID: " + address);
            Scene scene = EditorSceneManager.OpenScene(P08StoreSetup.ScenePath, OpenSceneMode.Single);
            GameObject[] storeRoots = scene.GetRootGameObjects().Where(value => value.name == "P08_STORE_SCREEN").ToArray();
            if (storeRoots.Length != 1) throw new BuildFailedException("P08_SCENE_STORE_INVALID");
            GameObject sceneStore = storeRoots[0]; sceneStore.SetActive(true); P08StoreSetup.PrepareStoreRoot(sceneStore); Canvas.ForceUpdateCanvases();
            string[] invalidButtons = sceneStore.GetComponentsInChildren<Button>(true)
                .Where(button => button.GetComponent<RectTransform>().rect.height < 64f)
                .Select(button => button.name + "=" + button.GetComponent<RectTransform>().rect.height.ToString("0.##"))
                .ToArray();
            sceneStore.SetActive(false);
            if (invalidButtons.Length > 0) throw new BuildFailedException("P08_TOUCH_TARGET_INVALID: " + string.Join(", ", invalidButtons));
            FacilityDrawerView drawer = Object.FindFirstObjectByType<FacilityDrawerView>(FindObjectsInactive.Include);
            if (drawer == null || drawer.GetComponentsInChildren<Transform>(true).Count(value => value.name == "P08_STORE_OPEN_BUTTON") != 1) throw new BuildFailedException("P08_SCENE_ENTRY_INVALID");
            StoreEntryButton entry = drawer.GetComponentInChildren<StoreEntryButton>(true);
            if (entry == null || !entry.gameObject.activeSelf || entry.GetComponent<CanvasGroup>() == null) throw new BuildFailedException("P08_SCENE_ENTRY_LIFECYCLE_INVALID");
            Button entryButton = entry.GetComponent<Button>();
            if (entryButton == null || entryButton.onClick.GetPersistentEventCount() != 1
                || entryButton.onClick.GetPersistentTarget(0) != entry
                || entryButton.onClick.GetPersistentMethodName(0) != nameof(StoreEntryButton.OpenStore))
                throw new BuildFailedException("P08_SCENE_ENTRY_BINDING_INVALID");

            Scene bootstrap = EditorSceneManager.OpenScene(P08StoreSetup.BootstrapScenePath, OpenSceneMode.Single);
            AppRoot appRoot = Object.FindFirstObjectByType<AppRoot>(FindObjectsInactive.Include);
            if (appRoot == null || appRoot.transform.Cast<Transform>().Count(value => value.name == "P08_PRESENTATION_CAMERA" && value.GetComponent<Camera>() != null) != 1)
                throw new BuildFailedException("P08_PRESENTATION_CAMERA_INVALID");
            SceneNavigationButton[] navigation = Object.FindObjectsByType<SceneNavigationButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (navigation.Count(value => value.TargetScene == "Kingdom") != 1 || navigation.Count(value => value.TargetScene == "Region") != 1)
                throw new BuildFailedException("P08_SCENE_NAVIGATION_INVALID");
        }
    }

    public static class P08CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P08/Generate Acceptance Captures")]
        public static void Run()
        {
            P08GeneratedAssetVerifier.Verify();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P08StoreSetup.ScreenPath) ?? throw new BuildFailedException("P08_UI_PREFAB_MISSING");
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); root.SetActive(true); P08StoreSetup.PrepareStoreRoot(root);
            Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
            GameObject content = Find(nodes, "Content"), state = Find(nodes, "P08_STATE_PANEL"), modal = Find(nodes, "P08_CONFIRM_MODAL"), toast = Find(nodes, "P08_RESULT_TOAST"), debug = Find(nodes, "P08_DEBUG_PANEL");
            TMP_Text stateTitle = Find(nodes, "StateTitle").GetComponent<TMP_Text>(); TMP_Text stateBody = Find(nodes, "StateBody").GetComponent<TMP_Text>(); TMP_Text detail = Find(nodes, "DetailBody").GetComponent<TMP_Text>(); TMP_Text activity = Find(nodes, "Activity").GetComponent<TMP_Text>();
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P08")); Directory.CreateDirectory(output);
            state.SetActive(false); modal.SetActive(false); toast.SetActive(false); debug.SetActive(false); content.SetActive(true);
            Capture(root, output, "p08_01_store_content.png", 1920, 1080);
            Find(nodes, "P08_TAB_SELL").GetComponent<Image>().color = new Color32(126, 83, 42, 255); Find(nodes, "P08_TAB_BUY").GetComponent<Image>().color = new Color32(67, 61, 54, 255); detail.text = "전리품 판매\n\n허브 묶음 x4\n기준가  12 G\n정책 배율  1.00\n\n<size=36><color=#F2D47A>48 G</color></size>\n\n판매 대금은 개인 골드로 지급됩니다."; Capture(root, output, "p08_02_sell_loot.png", 1920, 1080);
            detail.text = "장비 비교\n\n현재  낡은 활     공격  12\n신규  정찰대 활   공격  18\n\n<color=#8ECF91>공격 +6 · 전투력 +420</color>\n\n구매 즉시 레온에게 장착"; Capture(root, output, "p08_03_equipment_compare.png", 1920, 1080);
            modal.SetActive(true); modal.transform.Find("ModalBody").GetComponent<TMP_Text>().text = "소형 회복 물약\n\n수량  2\n결제  <color=#F2D47A>70 개인 골드</color>\n\n구매 후 레온 가방으로 이동합니다."; Capture(root, output, "p08_04_potion_buy.png", 1920, 1080); modal.SetActive(false);
            Find(nodes, "P08_POLICY_CONTROL").transform.Find("Label").GetComponent<TMP_Text>().text = "가격 정책  HIGH  ·  +12%"; detail.text = "정책 미리보기\n\nLOW        31 G\nSTANDARD   35 G\nHIGH       39 G\n\n상점 Lv.2부터 변경할 수 있습니다."; Capture(root, output, "p08_05_policy.png", 1920, 1080);
            toast.SetActive(true); toast.transform.Find("ToastText").GetComponent<TMP_Text>().text = "거래 완료 · 개인 −35 · 왕국 +35 · 장부 기록됨"; Capture(root, output, "p08_06_result.png", 1920, 1080); toast.SetActive(false);
            detail.text = "판매 가능한 상품이 없습니다.\n\n다음 보급 주기를 기다리거나\n사냥을 완료해 전리품을 획득하세요."; activity.text = "재고 소진 · 다음 보급 epoch 1"; Capture(root, output, "p08_07_empty.png", 1920, 1080);
            state.SetActive(true); content.SetActive(false); stateTitle.text = "상인이 자리를 비웠습니다"; stateBody.text = "상점 시설은 운영 중이지만 담당 상인이 없습니다.\n용병 배치를 확인하세요.\n\nP08_MERCHANT_MISSING"; Capture(root, output, "p08_08_merchant_missing.png", 1920, 1080);
            stateTitle.text = "왕국 상점이 잠겨 있습니다"; stateBody.text = "상점을 건설하고 상인을 배치하면 거래할 수 있습니다.\n\nP08_STORE_LOCKED"; Capture(root, output, "p08_09_locked.png", 1920, 1080);
            stateTitle.text = "상점을 표시할 수 없습니다"; stateBody.text = "저장 데이터는 변경하지 않았습니다.\n다시 시도해 주세요.\n\nP08_LEDGER_RECONCILIATION_FAILED"; Capture(root, output, "p08_10_error.png", 1920, 1080);
            state.SetActive(false); content.SetActive(true); detail.text = "20:9 안전 영역\n\n상품 목록, 상세 정보와 거래 버튼이\n노치 및 제스처 영역 안에 유지됩니다."; Capture(root, output, "p08_11_20x9.png", 2400, 1080);
            debug.SetActive(true); debug.transform.Find("DebugText").GetComponent<TMP_Text>().text = "AUTONOMY SETTLEMENT\n판매 +84 · 물약 −35 · 장비 −210\nledger: 3 valid · revision: 18"; detail.text = "사냥 정산 자동 거래\n\n1. 불필요 전리품 판매\n2. 회복 물약 보충\n3. 장비 개선 구매\n\n<color=#8ECF91>모든 거래가 장부와 일치합니다.</color>"; Capture(root, output, "p08_12_autonomy.png", 1920, 1080);
            Debug.Log($"P08_CAPTURES={output}; count=12");
        }

        private static GameObject Find(IEnumerable<Transform> nodes, string name) => nodes.Single(value => value.name == name).gameObject;
        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            P08StoreSetup.PrepareStoreRoot(root);
            Canvas canvas = root.GetComponent<Canvas>(); var cameraObject = new GameObject("P08CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(12, 10, 9, 255); camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false); string path = Path.Combine(directory, filename); File.WriteAllBytes(path, texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            if (new FileInfo(path).Length <= 1024) throw new BuildFailedException("P08_CAPTURE_FAILED: " + filename);
        }
    }

    public static class P08AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P08/Build Android Development APK")]
        public static void Build()
        {
            P08GeneratedAssetVerifier.Verify();
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P08-Development.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P08 Android output invalid."));
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            var options = new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P08 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"P08_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    internal sealed class P08StoreBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 80;
        public void OnPreprocessBuild(BuildReport report) => P08GeneratedAssetVerifier.Verify();
    }
}
