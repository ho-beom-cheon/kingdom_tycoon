using System;
using System.IO;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Presentation.Mercenaries;
using KingdomTycoon.Presentation.Mercenaries.Views;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace KingdomTycoon.Editor
{
    public static class P05MercenaryRosterSetup
    {
        [MenuItem("Kingdom Tycoon/P05/Run Complete Setup")]
        public static void Run()
        {
            P05ContentPackageGenerator.GenerateContent3();
            P05MercenaryUiGenerator.Generate();
            P05MercenaryUiGenerator.AttachToBootstrap();
            P05GeneratedAssetVerifier.VerifyAll();
            Debug.Log("P05 mercenary roster setup completed.");
        }
    }

    public static class P05ContentPackageGenerator
    {
        [MenuItem("Kingdom Tycoon/P05/Import Content Package .3")]
        public static void GenerateContent3()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.3");
            string manifestPath = Path.Combine(root, "content_manifest.json");
            if (!File.Exists(manifestPath)) throw new BuildFailedException("CONTENT_P05_MANIFEST_MISSING");
            ContentImportResult result = new CsvContentImporter().Import(File.ReadAllText(manifestPath), file => File.ReadAllText(Path.Combine(root, file)));
            if (!result.IsValid || result.Catalog.Tables.Count != 62)
                throw new BuildFailedException("CONTENT_P05_PACKAGE_GOLDEN_MISMATCH: " + string.Join("; ", result.Report.Issues));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("P05 content .3 validated: 62 canonical tables.");
        }
    }

    public static class P05MercenaryUiGenerator
    {
        public const string GeneratedRoot = "Assets/KingdomTycoon/ContentGenerated/P05Mercenary";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "p05-roster-v1.0.5-rect-clipping-drawer-origin";
        private const string MarkerPath = GeneratedRoot + "/P05MercenaryUi.marker.asset";
        private const string CardPath = GeneratedRoot + "/Card.prefab";
        private const string RosterPath = GeneratedRoot + "/RosterScreen.prefab";
        private const string DrawerPath = GeneratedRoot + "/DetailDrawer.prefab";
        private const string FilterPath = GeneratedRoot + "/FilterModal.prefab";
        private const string SortPath = GeneratedRoot + "/SortModal.prefab";
        private const string FontSourcePath = GeneratedRoot + "/Fonts/NotoSansKR-VF.ttf";
        private const string FontAssetPath = GeneratedRoot + "/Fonts/P05NotoSansKR.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        public static void ImportTmpResources()
        {
            EnsureTmpResources();
            Debug.Log("P05 TMP Korean font and settings are ready.");
        }

        [MenuItem("Kingdom Tycoon/P05/Generate Mercenary UI")]
        public static void Generate()
        {
            EnsureFolder(GeneratedRoot);
            EnsureFolder(GeneratedRoot + "/Fonts");
            EnsureTmpResources();
            P05GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P05GeneratedAssetMarker>(MarkerPath);
            bool current = marker != null && marker.fingerprint == Fingerprint
                && new[] { CardPath, RosterPath, DrawerPath, FilterPath, SortPath }.All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null);
            if (current)
            {
                Debug.Log("P05 UI assets already match generator fingerprint.");
                return;
            }

            CreateCardPrefab();
            CreateRosterPrefab();
            CreateDrawerPrefab();
            CreateFilterModal();
            CreateOptionModal(SortPath, "정렬", new[] { "기본", "이름 오름차순", "이름 내림차순", "레벨 높은순", "레벨 낮은순", "등급 높은순", "등급 낮은순", "랭크 높은순", "랭크 낮은순" },
                new[] { "DEFAULT", "NAME_ASC", "NAME_DESC", "LEVEL_DESC", "LEVEL_ASC", "GRADE_DESC", "GRADE_ASC", "RANK_DESC", "RANK_ASC" });
            if (marker == null)
            {
                AssetDatabase.DeleteAsset(MarkerPath);
                marker = ScriptableObject.CreateInstance<P05GeneratedAssetMarker>();
                AssetDatabase.CreateAsset(marker, MarkerPath);
            }
            marker.fingerprint = Fingerprint;
            EditorUtility.SetDirty(marker);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void AttachToBootstrap()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            Transform common = scene.GetRootGameObjects().Select(root => root.transform.Find("CommonUiRoot")).FirstOrDefault(value => value != null)
                ?? throw new BuildFailedException("P05_COMMON_UI_ROOT_MISSING");
            Transform screenLayer = common.Find("ScreenCanvas/SafeArea");
            Transform drawerLayer = common.Find("DrawerCanvas/SafeArea");
            Transform modalLayer = common.Find("ModalCanvas/SafeArea");
            RemoveManagedComponents<MercenaryRosterPresenter>(common);
            RemoveManagedComponents<MercenaryRosterView>(screenLayer);
            RemoveManagedComponents<MercenaryDetailDrawerView>(drawerLayer);
            RemoveManagedComponents<MercenaryFilterModalView>(modalLayer);
            RemoveManagedComponents<MercenaryOptionModalView>(modalLayer);
            RemoveManaged(common, "P05MercenaryRosterPresenter");
            RemoveManaged(common.Find("HudCanvas/SafeArea"), "NAV_MERCENARIES");
            RemoveManaged(screenLayer, "MercenaryRosterScreen");
            RemoveManaged(drawerLayer, "MercenaryDetailDrawer");
            RemoveManaged(modalLayer, "MercenaryFilterModal");
            RemoveManaged(modalLayer, "MercenarySortModal");
            RemoveManaged(common.Find("ToastCanvas/SafeArea"), "P05_RECOVERY_TOAST");
            RemoveManaged(common.Find("DebugCanvas/SafeArea"), "P05_DEBUG_LABEL");

            Button nav = CreateButton("NAV_MERCENARIES", common.Find("HudCanvas/SafeArea"), "용병", new Color32(159, 99, 53, 255));
            RectTransform navRect = nav.GetComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0.42f, 0f); navRect.anchorMax = new Vector2(0.58f, 0f); navRect.pivot = new Vector2(0.5f, 0f);
            navRect.offsetMin = new Vector2(0f, 16f); navRect.offsetMax = new Vector2(0f, 96f);

            GameObject rosterObject = InstantiatePrefab(RosterPath, common.Find("ScreenCanvas/SafeArea"), "MercenaryRosterScreen");
            GameObject drawerObject = InstantiatePrefab(DrawerPath, common.Find("DrawerCanvas/SafeArea"), "MercenaryDetailDrawer");
            GameObject filterObject = InstantiatePrefab(FilterPath, common.Find("ModalCanvas/SafeArea"), "MercenaryFilterModal");
            GameObject sortObject = InstantiatePrefab(SortPath, common.Find("ModalCanvas/SafeArea"), "MercenarySortModal");
            rosterObject.SetActive(false);
            drawerObject.SetActive(false);
            filterObject.SetActive(false);
            sortObject.SetActive(false);

            TMP_Text recoveryToast = CreateText("P05_RECOVERY_TOAST", common.Find("ToastCanvas/SafeArea"), "저장 파일을 복구했습니다.", 28f, TextAlignmentOptions.Center);
            SetRect(recoveryToast.rectTransform, new Vector2(0.3f, 0.91f), new Vector2(0.7f, 0.97f), Vector2.zero, Vector2.zero);
            recoveryToast.color = new Color32(255, 221, 150, 255);
            recoveryToast.gameObject.SetActive(false);
            TMP_Text debugLabel = CreateText("P05_DEBUG_LABEL", common.Find("DebugCanvas/SafeArea"), string.Empty, 18f, TextAlignmentOptions.TopLeft);
            SetRect(debugLabel.rectTransform, new Vector2(0f, 0.9f), new Vector2(0.45f, 1f), new Vector2(12f, 0f), Vector2.zero);
            debugLabel.gameObject.SetActive(false);

            var presenterObject = new GameObject("P05MercenaryRosterPresenter", typeof(MercenaryRosterPresenter));
            presenterObject.transform.SetParent(common, false);
            presenterObject.GetComponent<MercenaryRosterPresenter>().Configure(nav, rosterObject.GetComponent<MercenaryRosterView>(),
                drawerObject.GetComponent<MercenaryDetailDrawerView>(), filterObject.GetComponent<MercenaryFilterModalView>(),
                sortObject.GetComponent<MercenaryOptionModalView>(), recoveryToast, debugLabel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
        }

        private static void CreateCardPrefab()
        {
            AssetDatabase.DeleteAsset(CardPath);
            var root = new GameObject("MercenaryCard", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MercenaryCardView));
            RectTransform rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(420f, 220f);
            Image background = root.GetComponent<Image>(); background.color = new Color32(37, 48, 60, 248);
            Button select = root.GetComponent<Button>(); select.targetGraphic = background; select.navigation = new Navigation { mode = Navigation.Mode.None };
            Image portrait = CreateImage("Portrait", root.transform, new Color32(63, 72, 82, 255));
            SetRect(portrait.rectTransform, new Vector2(0f, 0f), new Vector2(0.38f, 1f), new Vector2(16f, 16f), new Vector2(-8f, -16f));
            TMP_Text name = CreateText("Name", root.transform, "용병", 30f, TextAlignmentOptions.Left);
            SetRect(name.rectTransform, new Vector2(0.4f, 0.68f), new Vector2(0.96f, 0.94f), Vector2.zero, Vector2.zero);
            TMP_Text identity = CreateText("Identity", root.transform, string.Empty, 20f, TextAlignmentOptions.Left);
            SetRect(identity.rectTransform, new Vector2(0.4f, 0.37f), new Vector2(0.96f, 0.67f), Vector2.zero, Vector2.zero);
            TMP_Text state = CreateText("State", root.transform, string.Empty, 19f, TextAlignmentOptions.Left);
            state.color = new Color32(235, 190, 105, 255);
            SetRect(state.rectTransform, new Vector2(0.4f, 0.08f), new Vector2(0.96f, 0.36f), Vector2.zero, Vector2.zero);
            root.GetComponent<MercenaryCardView>().Configure(select, portrait, name, identity, state);
            PrefabUtility.SaveAsPrefabAsset(root, CardPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateRosterPrefab()
        {
            AssetDatabase.DeleteAsset(RosterPath);
            var root = new GameObject("MercenaryRosterScreen", typeof(RectTransform), typeof(Image), typeof(MercenaryRosterView));
            SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(0f, 112f), new Vector2(0f, -112f));
            root.GetComponent<Image>().color = new Color32(18, 25, 32, 252);
            TMP_Text title = CreateText("Title", root.transform, "용병 로스터", 40f, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0.3f, 1f), new Vector2(0.7f, 1f), new Vector2(0f, -88f), new Vector2(0f, -16f));
            TMP_Text owned = CreateText("COUNT_OWNED", root.transform, "보유 0/0", 27f, TextAlignmentOptions.Left);
            SetRect(owned.rectTransform, new Vector2(0f, 1f), new Vector2(0.25f, 1f), new Vector2(24f, -88f), new Vector2(0f, -16f));
            TMP_Text active = CreateText("COUNT_ACTIVE", root.transform, "활동 0/0", 27f, TextAlignmentOptions.Left);
            SetRect(active.rectTransform, new Vector2(0.18f, 1f), new Vector2(0.42f, 1f), new Vector2(24f, -88f), new Vector2(0f, -16f));
            Button close = CreateButton("Close", root.transform, "닫기", new Color32(83, 91, 101, 255));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, 1f), Vector2.one, new Vector2(-104f, -88f), new Vector2(-24f, -16f));

            TMP_InputField search = CreateInput("SEARCH", root.transform, "용병 이름 검색");
            SetRect(search.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0.35f, 1f), new Vector2(24f, -176f), new Vector2(-12f, -104f));
            Button filter = CreateButton("FILTER_BUTTON", root.transform, "필터", new Color32(60, 79, 94, 255));
            SetRect(filter.GetComponent<RectTransform>(), new Vector2(0.35f, 1f), new Vector2(0.5f, 1f), new Vector2(12f, -176f), new Vector2(-12f, -104f));
            Button sort = CreateButton("SORT_BUTTON", root.transform, "정렬", new Color32(60, 79, 94, 255));
            SetRect(sort.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.65f, 1f), new Vector2(12f, -176f), new Vector2(-12f, -104f));
            TMP_Text filterSummary = CreateText("FILTER_CHIPS", root.transform, "필터 없음", 21f, TextAlignmentOptions.Left);
            SetRect(filterSummary.rectTransform, new Vector2(0.65f, 1f), new Vector2(0.92f, 1f), new Vector2(12f, -176f), new Vector2(-12f, -104f));
            TMP_Text offline = CreateText("OFFLINE_BADGE", root.transform, "오프라인 · 로컬 저장 사용 중", 20f, TextAlignmentOptions.Center);
            offline.color = new Color32(255, 198, 102, 255);
            SetRect(offline.rectTransform, new Vector2(0.65f, 1f), new Vector2(0.92f, 1f), new Vector2(12f, -104f), new Vector2(-12f, -88f));
            offline.gameObject.SetActive(false);

            var contentPanel = new GameObject("ContentPanel", typeof(RectTransform)); contentPanel.transform.SetParent(root.transform, false); Stretch(contentPanel.GetComponent<RectTransform>());
            var scrollObject = new GameObject("ROSTER_SCROLL", typeof(RectTransform), typeof(ScrollRect)); scrollObject.transform.SetParent(contentPanel.transform, false);
            SetRect(scrollObject.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -192f));
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewportObject.transform.SetParent(scrollObject.transform, false); Stretch(viewportObject.GetComponent<RectTransform>());
            var gridObject = new GameObject("Content", typeof(RectTransform), typeof(VirtualizedMercenaryGrid)); gridObject.transform.SetParent(viewportObject.transform, false);
            RectTransform content = gridObject.GetComponent<RectTransform>(); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0f, 1f); content.offsetMin = content.offsetMax = Vector2.zero;
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>(); scroll.viewport = viewportObject.GetComponent<RectTransform>(); scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            MercenaryCardView card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath).GetComponent<MercenaryCardView>();
            gridObject.GetComponent<VirtualizedMercenaryGrid>().Configure(scroll, viewportObject.GetComponent<RectTransform>(), content, card);
            TMP_Text noResults = CreateText("NO_RESULTS", viewportObject.transform, "조건에 맞는 용병이 없습니다.", 30f, TextAlignmentOptions.Center); Stretch(noResults.rectTransform); noResults.gameObject.SetActive(false);

            Image statePanel = CreateImage("StatePanel", root.transform, new Color32(18, 25, 32, 252)); Stretch(statePanel.rectTransform);
            TMP_Text stateMessage = CreateText("StateMessage", statePanel.transform, "용병 정보를 불러오는 중…", 32f, TextAlignmentOptions.Center);
            SetRect(stateMessage.rectTransform, new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.65f), Vector2.zero, Vector2.zero);
            var skeletons = new GameObject("LoadingSkeletons", typeof(RectTransform));
            skeletons.transform.SetParent(statePanel.transform, false); Stretch(skeletons.GetComponent<RectTransform>());
            for (int i = 0; i < 4; i++)
            {
                Image skeleton = CreateImage("Skeleton_" + i, skeletons.transform, new Color32(48, 61, 74, 180));
                SetRect(skeleton.rectTransform, new Vector2(0.05f + i * 0.235f, 0.16f), new Vector2(0.245f + i * 0.235f, 0.34f), Vector2.zero, Vector2.zero);
            }
            Button retry = CreateButton("RETRY_LOAD", statePanel.transform, "다시 불러오기", new Color32(181, 113, 49, 255));
            SetRect(retry.GetComponent<RectTransform>(), new Vector2(0.38f, 0.22f), new Vector2(0.62f, 0.31f), Vector2.zero, Vector2.zero);
            retry.gameObject.SetActive(false);
            root.GetComponent<MercenaryRosterView>().Configure(owned, active, search, filter, sort, close, stateMessage,
                statePanel.gameObject, contentPanel, gridObject.GetComponent<VirtualizedMercenaryGrid>(), noResults,
                filterSummary, offline, retry);
            PrefabUtility.SaveAsPrefabAsset(root, RosterPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateDrawerPrefab()
        {
            AssetDatabase.DeleteAsset(DrawerPath);
            var root = new GameObject("MercenaryDetailDrawer", typeof(RectTransform), typeof(CanvasGroup), typeof(MercenaryDetailDrawerView));
            Stretch(root.GetComponent<RectTransform>());
            Button scrim = CreateButton("Scrim", root.transform, string.Empty, new Color32(0, 0, 0, 150)); Stretch(scrim.GetComponent<RectTransform>());
            Image panel = CreateImage("Panel", root.transform, new Color32(27, 36, 46, 253));
            SetRect(panel.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-720f, 0f), Vector2.zero);
            Image portrait = CreateImage("DETAIL_PORTRAIT", panel.transform, new Color32(61, 72, 84, 255));
            SetRect(portrait.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -240f), new Vector2(216f, -48f));
            TMP_Text identity = CreateText("DETAIL_IDENTITY", panel.transform, "용병", 29f, TextAlignmentOptions.TopLeft);
            SetRect(identity.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(232f, -240f), new Vector2(-96f, -48f));
            Button close = CreateButton("DETAIL_CLOSE", panel.transform, "닫기", new Color32(82, 91, 101, 255));
            SetRect(close.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-88f, -88f), new Vector2(-16f, -16f));
            string[] tabNames = { "개요", "장비", "성장", "기록" };
            var tabs = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                tabs[i] = CreateButton("TAB_" + i, panel.transform, tabNames[i], new Color32(57, 72, 86, 255));
                SetRect(tabs[i].GetComponent<RectTransform>(), new Vector2(i * 0.25f, 1f), new Vector2((i + 1) * 0.25f, 1f), new Vector2(16f, -320f), new Vector2(-4f, -256f));
            }
            var bodyScrollObject = new GameObject("DETAIL_SCROLL", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            bodyScrollObject.transform.SetParent(panel.transform, false);
            Image bodyPanel = bodyScrollObject.GetComponent<Image>(); bodyPanel.color = new Color32(20, 29, 37, 220);
            SetRect(bodyPanel.rectTransform, Vector2.zero, Vector2.one, new Vector2(24f, 120f), new Vector2(-24f, -336f));
            var bodyViewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); bodyViewport.transform.SetParent(bodyPanel.transform, false); Stretch(bodyViewport.GetComponent<RectTransform>());
            var bodyContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); bodyContent.transform.SetParent(bodyViewport.transform, false);
            RectTransform bodyContentRect = bodyContent.GetComponent<RectTransform>(); bodyContentRect.anchorMin = new Vector2(0f, 1f); bodyContentRect.anchorMax = Vector2.one; bodyContentRect.pivot = new Vector2(0.5f, 1f); bodyContentRect.offsetMin = bodyContentRect.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = bodyContent.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(20, 20, 18, 18); layout.childControlHeight = true; layout.childControlWidth = true;
            ContentSizeFitter fitter = bodyContent.GetComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text body = CreateText("Body", bodyContent.transform, string.Empty, 24f, TextAlignmentOptions.TopLeft); body.overflowMode = TextOverflowModes.Overflow;
            bodyScrollObject.GetComponent<ScrollRect>().viewport = bodyViewport.GetComponent<RectTransform>(); bodyScrollObject.GetComponent<ScrollRect>().content = bodyContentRect; bodyScrollObject.GetComponent<ScrollRect>().horizontal = false; bodyScrollObject.GetComponent<ScrollRect>().vertical = true;
            TMP_Text error = CreateText("Error", panel.transform, string.Empty, 20f, TextAlignmentOptions.Center); error.color = new Color32(255, 140, 118, 255);
            SetRect(error.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 104f), new Vector2(-24f, 136f));
            Button toggle = CreateButton("ACTIVE_TOGGLE", panel.transform, "활동 배치", new Color32(181, 113, 49, 255));
            SetRect(toggle.GetComponent<RectTransform>(), Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 24f), new Vector2(-24f, 104f));
            TMP_Text toggleLabel = toggle.transform.Find("Label").GetComponent<TMP_Text>();
            root.GetComponent<MercenaryDetailDrawerView>().Configure(portrait, identity, body, error, close, toggle, toggleLabel,
                tabs, panel.rectTransform, scrim, root.GetComponent<CanvasGroup>());
            PrefabUtility.SaveAsPrefabAsset(root, DrawerPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateOptionModal(string path, string titleText, string[] labels, string[] values)
        {
            AssetDatabase.DeleteAsset(path);
            var root = new GameObject(Path.GetFileNameWithoutExtension(path), typeof(RectTransform), typeof(MercenaryOptionModalView)); Stretch(root.GetComponent<RectTransform>());
            Button scrim = CreateButton("Scrim", root.transform, string.Empty, new Color32(0, 0, 0, 170)); Stretch(scrim.GetComponent<RectTransform>());
            Image panel = CreateImage("Panel", root.transform, new Color32(36, 47, 59, 255));
            SetRect(panel.rectTransform, new Vector2(0.3f, 0.18f), new Vector2(0.7f, 0.82f), Vector2.zero, Vector2.zero);
            TMP_Text title = CreateText("Title", panel.transform, titleText, 36f, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0f, 0.82f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, 0f));
            Button close = CreateButton("Close", panel.transform, "닫기", new Color32(83, 91, 101, 255));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.7f, 0.02f), new Vector2(0.96f, 0.14f), Vector2.zero, Vector2.zero);
            var buttons = new Button[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / 2;
                int column = i % 2;
                float top = 0.76f - row * 0.13f;
                buttons[i] = CreateButton("Option_" + values[i], panel.transform, labels[i], new Color32(60, 79, 94, 255));
                SetRect(buttons[i].GetComponent<RectTransform>(), new Vector2(0.05f + column * 0.46f, top - 0.1f), new Vector2(0.49f + column * 0.46f, top), Vector2.zero, Vector2.zero);
            }
            root.GetComponent<MercenaryOptionModalView>().Configure(scrim, close, buttons, values);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateFilterModal()
        {
            AssetDatabase.DeleteAsset(FilterPath);
            var root = new GameObject("FilterModal", typeof(RectTransform), typeof(MercenaryFilterModalView)); Stretch(root.GetComponent<RectTransform>());
            Button scrim = CreateButton("Scrim", root.transform, string.Empty, new Color32(0, 0, 0, 170)); Stretch(scrim.GetComponent<RectTransform>());
            Image panel = CreateImage("Panel", root.transform, new Color32(36, 47, 59, 255));
            SetRect(panel.rectTransform, new Vector2(0.18f, 0.06f), new Vector2(0.82f, 0.94f), Vector2.zero, Vector2.zero);
            TMP_Text title = CreateText("Title", panel.transform, "용병 필터", 36f, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0f, 0.9f), Vector2.one, new Vector2(24f, 0f), new Vector2(-24f, 0f));

            var scrollObject = new GameObject("FilterScroll", typeof(RectTransform), typeof(ScrollRect)); scrollObject.transform.SetParent(panel.transform, false);
            SetRect(scrollObject.GetComponent<RectTransform>(), new Vector2(0.04f, 0.16f), new Vector2(0.96f, 0.89f), Vector2.zero, Vector2.zero);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)); viewport.transform.SetParent(scrollObject.transform, false); Stretch(viewport.GetComponent<RectTransform>());
            var contentObject = new GameObject("Content", typeof(RectTransform)); contentObject.transform.SetParent(viewport.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>(); content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 1f); content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;

            var labels = new[]
            {
                "[직업] 전사", "[직업] 수호자", "[직업] 궁수", "[직업] 마법사", "[직업] 성직자",
                "[등급] 일반", "[등급] 고급", "[등급] 희귀", "[등급] 영웅", "[등급] 전설",
                "[랭크] 견습", "[랭크] 정규", "[랭크] 숙련", "[랭크] 정예", "[랭크] 영웅", "[랭크] 전설",
                "[상태] 마을 대기", "[상태] 준비", "[상태] 지역 이동", "[상태] 대상 탐색", "[상태] 전투", "[상태] 전리품 수집",
                "[상태] 계속 판단", "[상태] 귀환", "[상태] 판매", "[상태] 치료", "[상태] 소모품 구매", "[상태] 장비 평가",
                "[상태] 장비 구매", "[상태] 승급 가능", "[상태] 승급 심사", "[상태] 부상", "[상태] 레이드 준비",
                "[활동] 전체", "[활동] 활동", "[활동] 대기", "승급 가능만", "부상만"
            };
            var dimensions = Enumerable.Repeat("JOB", 5).Concat(Enumerable.Repeat("GRADE", 5)).Concat(Enumerable.Repeat("RANK", 6))
                .Concat(Enumerable.Repeat("STATE", 17)).Concat(Enumerable.Repeat("ACTIVE", 3)).Concat(new[] { "PROMOTION", "INJURY" }).ToArray();
            var values = new[]
            {
                "JOB_WARRIOR", "JOB_GUARDIAN", "JOB_ARCHER", "JOB_MAGE", "JOB_CLERIC",
                "GRADE_C", "GRADE_B", "GRADE_A", "GRADE_S", "GRADE_SS",
                "RANK_APPRENTICE", "RANK_REGULAR", "RANK_SKILLED", "RANK_ELITE", "RANK_HERO", "RANK_LEGEND",
                "IDLE_TOWN", "PREPARE", "TRAVEL_TO_REGION", "FIND_TARGET", "COMBAT", "LOOT", "CONTINUE_DECISION", "RETURN_TOWN", "SELL_LOOT", "HEAL", "BUY_CONSUMABLES", "EVALUATE_EQUIPMENT", "BUY_EQUIPMENT", "PROMOTION_READY", "PROMOTION_PROCESS", "INJURED", "RAID_READY",
                "ALL", "ACTIVE", "INACTIVE", "READY_ONLY", "INJURED_ONLY"
            };
            var buttons = new Button[labels.Length];
            const float rowHeight = 72f;
            int rows = Mathf.CeilToInt(labels.Length / 2f);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rows * rowHeight);
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / 2;
                int column = i % 2;
                buttons[i] = CreateButton("Filter_" + dimensions[i] + "_" + values[i], content, labels[i], new Color32(60, 79, 94, 255));
                RectTransform rect = buttons[i].GetComponent<RectTransform>();
                SetRect(rect, new Vector2(column * 0.5f, 1f), new Vector2((column + 1) * 0.5f, 1f),
                    new Vector2(column == 0 ? 0f : 12f, -row * rowHeight - 64f),
                    new Vector2(column == 0 ? -12f : 0f, -row * rowHeight));
            }
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>(); scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;

            Button reset = CreateButton("Reset", panel.transform, "초기화", new Color32(83, 91, 101, 255));
            SetRect(reset.GetComponent<RectTransform>(), new Vector2(0.04f, 0.03f), new Vector2(0.28f, 0.13f), Vector2.zero, Vector2.zero);
            Button apply = CreateButton("Apply", panel.transform, "적용", new Color32(181, 113, 49, 255));
            SetRect(apply.GetComponent<RectTransform>(), new Vector2(0.38f, 0.03f), new Vector2(0.64f, 0.13f), Vector2.zero, Vector2.zero);
            Button close = CreateButton("Close", panel.transform, "닫기", new Color32(83, 91, 101, 255));
            SetRect(close.GetComponent<RectTransform>(), new Vector2(0.72f, 0.03f), new Vector2(0.96f, 0.13f), Vector2.zero, Vector2.zero);
            root.GetComponent<MercenaryFilterModalView>().Configure(scrim, close, apply, reset, buttons, dimensions, values);
            PrefabUtility.SaveAsPrefabAsset(root, FilterPath);
            Object.DestroyImmediate(root);
        }

        private static TMP_InputField CreateInput(string name, Transform parent, string placeholderValue)
        {
            Image background = CreateImage(name, parent, new Color32(48, 61, 74, 255));
            var area = new GameObject("Text Area", typeof(RectTransform)); area.transform.SetParent(background.transform, false); Stretch(area.GetComponent<RectTransform>()); area.GetComponent<RectTransform>().offsetMin = new Vector2(20f, 8f); area.GetComponent<RectTransform>().offsetMax = new Vector2(-20f, -8f);
            TMP_Text placeholder = CreateText("Placeholder", area.transform, placeholderValue, 23f, TextAlignmentOptions.MidlineLeft); Stretch(placeholder.rectTransform); placeholder.color = new Color32(170, 178, 184, 255);
            TMP_Text text = CreateText("Text", area.transform, string.Empty, 23f, TextAlignmentOptions.MidlineLeft); Stretch(text.rectTransform);
            TMP_InputField input = background.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = background; input.textViewport = area.GetComponent<RectTransform>(); input.textComponent = text; input.placeholder = placeholder;
            return input;
        }

        private static Button CreateButton(string name, Transform parent, string labelValue, Color32 color)
        {
            Image image = CreateImage(name, parent, color);
            Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            TMP_Text label = CreateText("Label", image.transform, labelValue, 25f, TextAlignmentOptions.Center); Stretch(label.rectTransform);
            return button;
        }

        private static Image CreateImage(string name, Transform parent, Color32 color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image)); gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>(); image.color = color; image.raycastTarget = true; return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); gameObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>(); text.text = value; text.fontSize = size; text.alignment = alignment; text.color = new Color32(244, 239, 222, 255); text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font != null) text.font = font;
            return text;
        }

        private static void EnsureTmpResources()
        {
            EnsureFolder(GeneratedRoot);
            EnsureFolder(GeneratedRoot + "/Fonts");
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                settings.name = "TMP Settings";
                AssetDatabase.CreateAsset(settings, TmpSettingsPath);
                var initialSettings = new SerializedObject(settings);
                SerializedProperty initialVersion = initialSettings.FindProperty("assetVersion");
                if (initialVersion != null) initialVersion.stringValue = "2";
                initialSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                TMP_Settings.LoadDefaultSettings();
            }
            AssetDatabase.ImportAsset(FontSourcePath, ImportAssetOptions.ForceSynchronousImport);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                Font source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath) ?? throw new BuildFailedException("P05_KOREAN_FONT_SOURCE_MISSING");
                fontAsset = TMP_FontAsset.CreateFontAsset(source) ?? throw new BuildFailedException("P05_KOREAN_FONT_CREATE_FAILED");
                fontAsset.name = "P05NotoSansKR";
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                if (fontAsset.material != null && !EditorUtility.IsPersistent(fontAsset.material)) AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                foreach (Texture2D atlas in fontAsset.atlasTextures) if (atlas != null && !EditorUtility.IsPersistent(atlas)) AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }
            var serialized = new SerializedObject(settings);
            SerializedProperty version = serialized.FindProperty("assetVersion");
            if (version != null) version.stringValue = "2";
            SerializedProperty defaultFont = serialized.FindProperty("m_defaultFontAsset");
            if (defaultFont != null) defaultFont.objectReferenceValue = fontAsset;
            SerializedProperty fontPath = serialized.FindProperty("m_defaultFontAssetPath");
            if (fontPath != null) fontPath.stringValue = "";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fontAsset);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static GameObject InstantiatePrefab(string path, Transform parent, string name)
        {
            if (parent == null) throw new BuildFailedException("P05_UI_LAYER_MISSING: " + name);
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new BuildFailedException("P05_PREFAB_MISSING: " + path);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent); instance.name = name; return instance;
        }

        private static void RemoveManaged(Transform parent, string name)
        {
            if (parent == null) return;
            Transform existing;
            while ((existing = parent.Find(name)) != null) Object.DestroyImmediate(existing.gameObject);
        }

        private static void RemoveManagedComponents<T>(Transform parent) where T : Component
        {
            if (parent == null) return;
            T[] values = parent.GetComponentsInChildren<T>(true);
            foreach (T value in values) if (value != null && value.transform.parent == parent) Object.DestroyImmediate(value.gameObject);
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; }
        private static void Stretch(RectTransform rect) => SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; }
        }
    }

    public static class P05GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P05/Verify Generated Assets")]
        public static void VerifyAll()
        {
            P03ContentAssetGenerator.Verify();
            P04KingdomAssetGenerator.Verify();
            P05ContentPackageGenerator.GenerateContent3();
            string root = P05MercenaryUiGenerator.GeneratedRoot;
            string[] prefabs = { "RosterScreen", "Card", "DetailDrawer", "FilterModal", "SortModal" };
            foreach (string name in prefabs) if (AssetDatabase.LoadAssetAtPath<GameObject>(root + "/" + name + ".prefab") == null) throw new BuildFailedException("P05_PREFAB_MISSING: " + name);
            GameObject roster = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/RosterScreen.prefab");
            if (roster.GetComponent<MercenaryRosterView>() == null || roster.GetComponentInChildren<VirtualizedMercenaryGrid>(true) == null) throw new BuildFailedException("P05_ROSTER_PREFAB_INVALID");
            GameObject drawerRoot = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/DetailDrawer.prefab");
            RectTransform drawer = drawerRoot.transform.Find("Panel")?.GetComponent<RectTransform>();
            if (drawer == null || Mathf.Abs(drawer.rect.width - 720f) > 0.01f || drawerRoot.GetComponent<CanvasGroup>() == null) throw new BuildFailedException("P05_DRAWER_WIDTH_INVALID");
            GameObject filter = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/FilterModal.prefab");
            if (filter.GetComponent<MercenaryFilterModalView>() == null || filter.GetComponentsInChildren<Button>(true).Length < 42) throw new BuildFailedException("P05_FILTER_MODAL_INVALID");
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root + "/Fonts/P05NotoSansKR.asset") == null) throw new BuildFailedException("P05_KOREAN_FONT_MISSING");
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new BuildFailedException("P05_ADDRESSABLES_MISSING");
            string[] portraitAddresses = { "ASSET_MERC_PLACEHOLDER_WARRIOR_V1", "ASSET_MERC_PLACEHOLDER_GUARDIAN_V1", "ASSET_MERC_PLACEHOLDER_ARCHER_V1", "ASSET_MERC_PLACEHOLDER_MAGE_V1", "ASSET_MERC_PLACEHOLDER_CLERIC_V1" };
            foreach (string address in portraitAddresses) if (settings.groups.Where(group => group != null).SelectMany(group => group.entries).Count(entry => entry.address == address) != 1) throw new BuildFailedException("P05_PORTRAIT_ADDRESS_INVALID: " + address);
            Scene scene = EditorSceneManager.OpenScene(P05MercenaryUiGenerator.BootstrapScenePath, OpenSceneMode.Single);
            MercenaryRosterPresenter presenter = Object.FindFirstObjectByType<MercenaryRosterPresenter>(FindObjectsInactive.Include);
            if (!scene.IsValid() || presenter == null) throw new BuildFailedException("P05_BOOTSTRAP_UI_INVALID");
            if (Object.FindObjectsByType<MercenaryRosterPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1
                || Object.FindObjectsByType<MercenaryRosterView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1
                || Object.FindObjectsByType<MercenaryDetailDrawerView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1
                || Object.FindObjectsByType<MercenaryFilterModalView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1
                || Object.FindObjectsByType<MercenaryOptionModalView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 1)
                throw new BuildFailedException("P05_BOOTSTRAP_UI_DUPLICATE");
            Button nav = GameObject.Find("NAV_MERCENARIES")?.GetComponent<Button>();
            if (nav == null || nav.GetComponent<RectTransform>().rect.height < 64f) throw new BuildFailedException("P05_NAV_TOUCH_TARGET_INVALID");
            foreach (Button button in presenter.GetComponentsInChildren<Button>(true))
            {
                Rect rect = button.GetComponent<RectTransform>().rect;
                if (button.gameObject.name is "DETAIL_CLOSE" or "ACTIVE_TOGGLE" or "FILTER_BUTTON" or "SORT_BUTTON" or "Close" or "Apply" or "Reset")
                    if (rect.width < 64f || rect.height < 64f) throw new BuildFailedException("P05_TOUCH_TARGET_INVALID: " + button.gameObject.name);
            }
            Debug.Log("P05 generated content, portraits, prefabs, touch targets, and Bootstrap wiring verified.");
        }
    }

    public static class P05AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P05/Build Android Development APK")]
        public static void Build()
        {
            P05GeneratedAssetVerifier.VerifyAll();
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P05-Development.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P05 Android output directory is invalid."));
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(),
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException($"P05 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"P05 Android build succeeded: {output}, bytes={report.summary.totalSize}");
        }
    }

    internal sealed class P05MercenaryBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -800;
        public void OnPreprocessBuild(BuildReport report) => P05GeneratedAssetVerifier.VerifyAll();
    }
}
