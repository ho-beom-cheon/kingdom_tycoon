using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KingdomTycoon.Presentation.Recruitment;
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
    public static class P13RecruitmentSetup
    {
        public const string Root = "Assets/KingdomTycoon/ContentGenerated/P13Recruitment";
        public const string PrefabRoot = Root + "/Prefabs";
        public const string ScreenPath = PrefabRoot + "/P13_RECRUITMENT_SCREEN.prefab";
        public const string MarkerPath = Root + "/P13Recruitment.marker.asset";
        public const string BootstrapScenePath = "Assets/KingdomTycoon/Scenes/Bootstrap.unity";
        public const string Fingerprint = "P13-RECRUITMENT-UI-v1.0.0";
        public static readonly string[] RequiredIds =
        {
            "P13_RECRUITMENT_SCREEN", "P13_HEADER", "P13_CLOSE", "P13_WALLET", "P13_CAPACITY", "P13_AUTHORITY",
            "P13_TAB_TAVERN", "P13_TAB_SPECIAL", "P13_TAB_HISTORY", "P13_TAVERN_ROOT", "P13_TAVERN_REFRESH",
            "P13_CANDIDATE_1", "P13_CANDIDATE_5", "P13_DETAIL_PANEL", "P13_CANDIDATE_LOCK", "P13_HIRE",
            "P13_SPECIAL_ROOT", "P13_PITY_S", "P13_PITY_S_FILL", "P13_PITY_SS", "P13_PITY_SS_FILL", "P13_SPECIAL_RECRUIT",
            "P13_HISTORY_ROOT", "P13_HISTORY_LIST", "P13_STATE_PANEL", "P13_RESULT_TOAST"
        };

        [MenuItem("Kingdom Tycoon/P13/Generate Recruitment Assets")]
        public static void Run()
        {
            ValidateContent(); GenerateScreen(); IntegrateBootstrapScene(); P13GeneratedAssetVerifier.Verify(); Debug.Log("P13_SETUP_COMPLETED");
        }

        public static void ValidateContent()
        {
            string manifestPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.11", "content_manifest.json"));
            if (!File.Exists(manifestPath)) throw new BuildFailedException("P13_CONTENT_MANIFEST_MISSING");
            string text = File.ReadAllText(manifestPath); if (!text.Contains("\"contentVersion\":\"1.0.0-content.11\"", StringComparison.Ordinal) || !text.Contains("\"csvSchemaSetVersion\":9", StringComparison.Ordinal)) throw new BuildFailedException("P13_CONTENT_MANIFEST_INVALID");
            string root = Path.GetDirectoryName(manifestPath) ?? throw new BuildFailedException("P13_CONTENT_PATH_INVALID");
            foreach (string file in new[] { "recruitment_tavern_rules.csv", "recruitment_grade_cost_rules.csv", "recruitment_history_rules.csv", "recruitment_pity_rules.csv" }) if (!File.Exists(Path.Combine(root, file))) throw new BuildFailedException("P13_CONTENT_TABLE_MISSING: " + file);
        }

        private static void GenerateScreen()
        {
            EnsureFolder(Root); EnsureFolder(PrefabRoot);
            var root = new GameObject("P13_RECRUITMENT_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RecruitmentScreenPresenter));
            RectTransform rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(1920, 1080); Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 95;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = .5f;
            CanvasGroup group = root.GetComponent<CanvasGroup>(); Image background = Panel("Background", root.transform, new Color32(10, 17, 19, 255)); Stretch(background.rectTransform);
            Image vignette = Panel("BronzeTop", background.transform, new Color32(52, 37, 25, 255)); SetRect(vignette.rectTransform, new Vector2(0, .91f), Vector2.one, Vector2.zero, Vector2.zero);
            Image safe = Panel("SafeArea", root.transform, new Color32(19, 27, 29, 250)); SetRect(safe.rectTransform, new Vector2(.018f, .028f), new Vector2(.982f, .972f), Vector2.zero, Vector2.zero);

            Text("Eyebrow", safe.transform, "왕실 모집소  ·  콘텐츠 11", 17, TextAlignmentOptions.Left, new Vector2(.022f, .935f), new Vector2(.45f, .985f), new Color32(123, 215, 192, 255));
            Text("P13_HEADER", safe.transform, "왕실 모집소", 40, TextAlignmentOptions.Left, new Vector2(.022f, .865f), new Vector2(.28f, .94f), new Color32(246, 211, 124, 255));
            TMP_Text wallet = Text("P13_WALLET", safe.transform, "왕국 골드  5,000    모집권  3    무료 프리미엄  600", 23, TextAlignmentOptions.Left, new Vector2(.30f, .89f), new Vector2(.78f, .95f));
            TMP_Text capacity = Text("P13_CAPACITY", safe.transform, "숙소  4 / 8", 22, TextAlignmentOptions.Right, new Vector2(.76f, .89f), new Vector2(.90f, .95f), new Color32(211, 188, 126, 255));
            TMP_Text authority = Text("P13_AUTHORITY", safe.transform, "DEVELOPMENT MOCK", 15, TextAlignmentOptions.Center, new Vector2(.79f, .952f), new Vector2(.93f, .985f), new Color32(113, 218, 193, 255));
            Button close = Button("P13_CLOSE", safe.transform, "×", new Color32(78, 55, 44, 255), 32); SetRect(close.GetComponent<RectTransform>(), new Vector2(.94f, .90f), new Vector2(.985f, .975f), Vector2.zero, Vector2.zero);

            Image rail = Panel("NavigationRail", safe.transform, new Color32(25, 38, 39, 255)); SetRect(rail.rectTransform, new Vector2(.018f, .055f), new Vector2(.15f, .85f), Vector2.zero, Vector2.zero);
            Text("RailTitle", rail.transform, "모집 업무", 19, TextAlignmentOptions.Left, new Vector2(.10f, .89f), new Vector2(.90f, .97f), new Color32(161, 183, 176, 255));
            Button tavernTab = Button("P13_TAB_TAVERN", rail.transform, "주점 후보", new Color32(132, 82, 39, 255), 23); SetRect(tavernTab.GetComponent<RectTransform>(), new Vector2(.08f, .69f), new Vector2(.92f, .84f), Vector2.zero, Vector2.zero);
            Button specialTab = Button("P13_TAB_SPECIAL", rail.transform, "특별 모집", new Color32(35, 48, 49, 255), 23); SetRect(specialTab.GetComponent<RectTransform>(), new Vector2(.08f, .51f), new Vector2(.92f, .66f), Vector2.zero, Vector2.zero);
            Button historyTab = Button("P13_TAB_HISTORY", rail.transform, "모집 기록", new Color32(35, 48, 49, 255), 23); SetRect(historyTab.GetComponent<RectTransform>(), new Vector2(.08f, .33f), new Vector2(.92f, .48f), Vector2.zero, Vector2.zero);
            Text("RailFoot", rail.transform, "후보는 갱신 전\n잠글 수 있습니다.\n\n특별 모집 결과는\n서버 영수증 경계를\n따릅니다.", 17, TextAlignmentOptions.TopLeft, new Vector2(.10f, .07f), new Vector2(.90f, .28f), new Color32(154, 167, 162, 255));

            Image center = Panel("MainStage", safe.transform, new Color32(29, 33, 33, 255)); SetRect(center.rectTransform, new Vector2(.16f, .055f), new Vector2(.74f, .85f), Vector2.zero, Vector2.zero);
            Image detail = Panel("P13_DETAIL_PANEL", safe.transform, new Color32(34, 43, 43, 255)); SetRect(detail.rectTransform, new Vector2(.75f, .055f), new Vector2(.985f, .85f), Vector2.zero, Vector2.zero);
            TMP_Text detailTitle = Text("DetailTitle", detail.transform, "후보를 선택하세요", 34, TextAlignmentOptions.Left, new Vector2(.07f, .84f), new Vector2(.93f, .95f), new Color32(246, 211, 124, 255));
            TMP_Text detailBody = Text("DetailBody", detail.transform, "후보의 직업과 등급, 고용비를 비교할 수 있습니다.", 20, TextAlignmentOptions.TopLeft, new Vector2(.07f, .37f), new Vector2(.93f, .82f));
            Button lockButton = Button("P13_CANDIDATE_LOCK", detail.transform, "후보 잠금", new Color32(43, 75, 72, 255), 22); SetRect(lockButton.GetComponent<RectTransform>(), new Vector2(.07f, .23f), new Vector2(.93f, .34f), Vector2.zero, Vector2.zero); TMP_Text lockLabel = lockButton.GetComponentInChildren<TMP_Text>();
            Button hireButton = Button("P13_HIRE", detail.transform, "500 G로 고용", new Color32(181, 111, 44, 255), 24); SetRect(hireButton.GetComponent<RectTransform>(), new Vector2(.07f, .075f), new Vector2(.93f, .20f), Vector2.zero, Vector2.zero); TMP_Text hireLabel = hireButton.GetComponentInChildren<TMP_Text>();

            GameObject tavernRoot = Panel("P13_TAVERN_ROOT", center.transform, Color.clear).gameObject; Stretch(tavernRoot.GetComponent<RectTransform>());
            Text("TavernHeading", tavernRoot.transform, "오늘의 주점 후보", 29, TextAlignmentOptions.Left, new Vector2(.035f, .88f), new Vector2(.45f, .96f), new Color32(235, 194, 109, 255));
            Text("TavernSub", tavernRoot.transform, "주점 1레벨  ·  일반~희귀 등급  ·  잠금 1명", 18, TextAlignmentOptions.Right, new Vector2(.45f, .89f), new Vector2(.965f, .95f), new Color32(159, 184, 175, 255));
            var candidates = new Button[5]; var candidateLabels = new TMP_Text[5];
            for (int index = 0; index < 5; index++)
            {
                candidates[index] = Button($"P13_CANDIDATE_{index + 1}", tavernRoot.transform, $"<size=17>고급</size>\n<b>후보 {index + 1}</b>\n<size=18>전사</size>\n<size=16>750 골드</size>", new Color32(57, 66, 60, 255), 21);
                SetRect(candidates[index].GetComponent<RectTransform>(), new Vector2(.025f + index * .195f, .31f), new Vector2(.205f + index * .195f, .82f), Vector2.zero, Vector2.zero); candidateLabels[index] = candidates[index].GetComponentInChildren<TMP_Text>();
                Image sigil = Panel("JobSigil", candidates[index].transform, index % 2 == 0 ? new Color32(79, 108, 96, 140) : new Color32(116, 78, 43, 140)); SetRect(sigil.rectTransform, new Vector2(.15f, .63f), new Vector2(.85f, .91f), Vector2.zero, Vector2.zero);
                Text("SigilGlyph", sigil.transform, new[] { "⚔", "◆", "▲", "✦", "✚" }[index], 38, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Color32(239, 211, 137, 255));
            }
            Button refresh = Button("P13_TAVERN_REFRESH", tavernRoot.transform, "무료 후보 갱신", new Color32(45, 91, 80, 255), 23); SetRect(refresh.GetComponent<RectTransform>(), new Vector2(.025f, .09f), new Vector2(.42f, .24f), Vector2.zero, Vector2.zero); TMP_Text refreshLabel = refresh.GetComponentInChildren<TMP_Text>();
            Text("RefreshHint", tavernRoot.transform, "◆ 잠긴 후보는 갱신 후에도 남습니다.   다음 무료 갱신  03:59:59", 18, TextAlignmentOptions.Left, new Vector2(.45f, .10f), new Vector2(.965f, .23f), new Color32(166, 178, 172, 255));

            GameObject specialRoot = Panel("P13_SPECIAL_ROOT", center.transform, Color.clear).gameObject; Stretch(specialRoot.GetComponent<RectTransform>()); specialRoot.SetActive(false);
            Image banner = Panel("SpecialBanner", specialRoot.transform, new Color32(31, 57, 62, 255)); SetRect(banner.rectTransform, new Vector2(.035f, .42f), new Vector2(.965f, .94f), Vector2.zero, Vector2.zero);
            Text("BannerEyebrow", banner.transform, "기간 한정 비전 계약  ·  표준", 16, TextAlignmentOptions.Left, new Vector2(.05f, .84f), new Vector2(.70f, .95f), new Color32(111, 222, 202, 255));
            Text("BannerTitle", banner.transform, "별빛 서약 특별 모집", 36, TextAlignmentOptions.Left, new Vector2(.05f, .65f), new Vector2(.70f, .84f), new Color32(248, 215, 122, 255));
            Text("BannerBody", banner.transform, "희귀 82%   영웅 16%   전설 2%\n매 모집마다 새로운 이름·외형·성장 성향을 가진 고유 용병이 합류합니다.", 21, TextAlignmentOptions.TopLeft, new Vector2(.05f, .32f), new Vector2(.72f, .63f));
            Image seal = Panel("ArcaneSeal", banner.transform, new Color32(76, 51, 111, 255)); SetRect(seal.rectTransform, new Vector2(.76f, .20f), new Vector2(.94f, .82f), Vector2.zero, Vector2.zero); Text("Seal", seal.transform, "✦\n<size=17>전설</size>", 45, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Color32(246, 213, 112, 255));
            TMP_Text pityS = Text("P13_PITY_S", specialRoot.transform, "영웅 이상 보장  0 / 10", 20, TextAlignmentOptions.Left, new Vector2(.045f, .31f), new Vector2(.47f, .38f)); Image sTrack = Panel("PitySTrack", specialRoot.transform, new Color32(17, 27, 29, 255)); SetRect(sTrack.rectTransform, new Vector2(.045f, .27f), new Vector2(.47f, .305f), Vector2.zero, Vector2.zero); Image sFill = Panel("P13_PITY_S_FILL", sTrack.transform, new Color32(99, 201, 175, 255)); SetRect(sFill.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            TMP_Text pitySs = Text("P13_PITY_SS", specialRoot.transform, "전설 확정 천장  0 / 80", 20, TextAlignmentOptions.Left, new Vector2(.52f, .31f), new Vector2(.955f, .38f)); Image ssTrack = Panel("PitySSTrack", specialRoot.transform, new Color32(17, 27, 29, 255)); SetRect(ssTrack.rectTransform, new Vector2(.52f, .27f), new Vector2(.955f, .305f), Vector2.zero, Vector2.zero); Image ssFill = Panel("P13_PITY_SS_FILL", ssTrack.transform, new Color32(211, 164, 75, 255)); SetRect(ssFill.rectTransform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            Button specialRecruit = Button("P13_SPECIAL_RECRUIT", specialRoot.transform, "모집권 1장으로 특별 모집", new Color32(115, 74, 156, 255), 24); SetRect(specialRecruit.GetComponent<RectTransform>(), new Vector2(.28f, .075f), new Vector2(.72f, .21f), Vector2.zero, Vector2.zero); TMP_Text specialRecruitLabel = specialRecruit.GetComponentInChildren<TMP_Text>();

            GameObject historyRoot = Panel("P13_HISTORY_ROOT", center.transform, Color.clear).gameObject; Stretch(historyRoot.GetComponent<RectTransform>()); historyRoot.SetActive(false);
            Text("HistoryHeading", historyRoot.transform, "모집 기록", 31, TextAlignmentOptions.Left, new Vector2(.05f, .86f), new Vector2(.50f, .96f), new Color32(235, 194, 109, 255));
            Text("HistorySub", historyRoot.transform, "최근 100건  ·  영수증과 결과 용병을 함께 보존", 18, TextAlignmentOptions.Right, new Vector2(.45f, .88f), new Vector2(.95f, .95f), new Color32(154, 180, 171, 255));
            TMP_Text historyText = Text("P13_HISTORY_LIST", historyRoot.transform, "아직 모집 기록이 없습니다.", 21, TextAlignmentOptions.TopLeft, new Vector2(.05f, .08f), new Vector2(.95f, .82f));

            Image state = Panel("P13_STATE_PANEL", safe.transform, new Color32(25, 34, 36, 253)); SetRect(state.rectTransform, new Vector2(.28f, .31f), new Vector2(.74f, .70f), Vector2.zero, Vector2.zero); TMP_Text stateTitle = Text("StateTitle", state.transform, "모집 명단을 정리하는 중", 34, TextAlignmentOptions.Center, new Vector2(.08f, .61f), new Vector2(.92f, .85f), new Color32(245, 207, 111, 255)); TMP_Text stateBody = Text("StateBody", state.transform, "후보와 천장 기록을 불러오고 있습니다.", 22, TextAlignmentOptions.Center, new Vector2(.08f, .25f), new Vector2(.92f, .60f)); state.gameObject.SetActive(false);
            Image toast = Panel("P13_RESULT_TOAST", safe.transform, new Color32(38, 105, 81, 252)); SetRect(toast.rectTransform, new Vector2(.34f, .01f), new Vector2(.68f, .075f), Vector2.zero, Vector2.zero); TMP_Text toastText = Text("ToastText", toast.transform, "후보 명단을 갱신했습니다.", 21, TextAlignmentOptions.Center, new Vector2(.04f, .08f), new Vector2(.96f, .92f)); toast.gameObject.SetActive(false);

            RecruitmentScreenView view = safe.gameObject.AddComponent<RecruitmentScreenView>();
            view.Configure(group, wallet, capacity, authority, close, tavernTab, specialTab, historyTab, tavernRoot, specialRoot, historyRoot, candidates, candidateLabels, detailTitle, detailBody, refresh, refreshLabel, lockButton, lockLabel, hireButton, hireLabel, specialRecruit, specialRecruitLabel, pityS, pitySs, sFill, ssFill, historyText, state.gameObject, stateTitle, stateBody, toast.gameObject, toastText);
            root.GetComponent<RecruitmentScreenPresenter>().Configure(view); PrefabUtility.SaveAsPrefabAsset(root, ScreenPath); Object.DestroyImmediate(root);
            P13GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P13GeneratedAssetMarker>(MarkerPath); if (marker == null) { AssetDatabase.DeleteAsset(MarkerPath); marker = ScriptableObject.CreateInstance<P13GeneratedAssetMarker>(); AssetDatabase.CreateAsset(marker, MarkerPath); }
            marker.fingerprint = Fingerprint; EditorUtility.SetDirty(marker); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void IntegrateBootstrapScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single); CommonUiRoot common = Object.FindFirstObjectByType<CommonUiRoot>(FindObjectsInactive.Include) ?? throw new BuildFailedException("P13_COMMON_UI_ROOT_MISSING");
            Transform screenCanvas = common.transform.Find("ScreenCanvas") ?? throw new BuildFailedException("P13_SCREEN_CANVAS_MISSING"); foreach (Transform old in screenCanvas.Cast<Transform>().Where(value => value.name == "P13_RECRUITMENT_SCREEN").ToArray()) Object.DestroyImmediate(old.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath) ?? throw new BuildFailedException("P13_UI_PREFAB_MISSING"); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, screenCanvas); root.name = "P13_RECRUITMENT_SCREEN"; PrepareRoot(root); RecruitmentScreenPresenter presenter = root.GetComponent<RecruitmentScreenPresenter>(); root.SetActive(false);
            Transform hud = common.transform.Find("HudCanvas/SafeArea") ?? throw new BuildFailedException("P13_HUD_SAFE_AREA_MISSING"); Transform existing = hud.Find("P13_RECRUITMENT_NAV_BUTTON"); if (existing != null) Object.DestroyImmediate(existing.gameObject);
            Button button = Button("P13_RECRUITMENT_NAV_BUTTON", hud, "모집", new Color32(119, 75, 139, 255), 22); RectTransform buttonRect = button.GetComponent<RectTransform>(); buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(.90f, 0); buttonRect.offsetMin = new Vector2(0, 110); buttonRect.offsetMax = new Vector2(182, 190);
            RecruitmentEntryButton entry = button.gameObject.AddComponent<RecruitmentEntryButton>(); entry.Configure(presenter, button); UnityEventTools.AddPersistentListener(button.onClick, entry.OpenRecruitment); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        internal static void PrepareRoot(GameObject root) { RectTransform rect = root.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1920, 1080); CanvasGroup group = root.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1; }
        private static Image Panel(string name, Transform parent, Color color) { var value = new GameObject(name, typeof(RectTransform), typeof(Image)); value.transform.SetParent(parent, false); Image image = value.GetComponent<Image>(); image.color = color; return image; }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color? color = null) { var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text label = go.GetComponent<TMP_Text>(); label.text = value; label.fontSize = size; label.alignment = alignment; label.color = color ?? new Color32(238, 231, 214, 255); label.textWrappingMode = TextWrappingModes.Normal; label.overflowMode = TextOverflowModes.Overflow; label.raycastTarget = false; SetRect(label.rectTransform, min, max, Vector2.zero, Vector2.zero); return label; }
        private static Button Button(string name, Transform parent, string label, Color color, float size) { Image image = Panel(name, parent, color); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; ColorBlock colors = button.colors; colors.highlightedColor = Color.Lerp(color, Color.white, .12f); colors.pressedColor = Color.Lerp(color, Color.black, .16f); colors.disabledColor = new Color(color.r * .55f, color.g * .55f, color.b * .55f, .72f); button.colors = colors; Text("Label", image.transform, label, size, TextAlignmentOptions.Center, Vector2.zero, Vector2.one); return button; }
        private static void Stretch(RectTransform value) => SetRect(value, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static void SetRect(RectTransform value, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax) { value.anchorMin = min; value.anchorMax = max; value.offsetMin = offsetMin; value.offsetMax = offsetMax; }
        private static void EnsureFolder(string path) { string current = "Assets"; foreach (string segment in path.Split('/').Skip(1)) { string next = current + "/" + segment; if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment); current = next; } }
    }

    public static class P13GeneratedAssetVerifier
    {
        [MenuItem("Kingdom Tycoon/P13/Verify Generated Assets")]
        public static void Verify()
        {
            P13RecruitmentSetup.ValidateContent(); P13GeneratedAssetMarker marker = AssetDatabase.LoadAssetAtPath<P13GeneratedAssetMarker>(P13RecruitmentSetup.MarkerPath); if (marker == null || marker.fingerprint != P13RecruitmentSetup.Fingerprint) throw new BuildFailedException("P13_GENERATED_FINGERPRINT_INVALID");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P13RecruitmentSetup.ScreenPath) ?? throw new BuildFailedException("P13_UI_PREFAB_MISSING"); Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true); foreach (string id in P13RecruitmentSetup.RequiredIds) if (nodes.Count(value => value.name == id) != 1) throw new BuildFailedException("P13_UI_ID_INVALID: " + id);
            prefab.SetActive(true); P13RecruitmentSetup.PrepareRoot(prefab); Canvas.ForceUpdateCanvases(); string[] invalid = prefab.GetComponentsInChildren<Button>(true).Where(value => value.GetComponent<RectTransform>().rect.width < 64 || value.GetComponent<RectTransform>().rect.height < 64).Select(value => value.name).ToArray(); prefab.SetActive(false); if (invalid.Length > 0) throw new BuildFailedException("P13_TOUCH_TARGET_INVALID: " + string.Join(",", invalid));
            Scene scene = EditorSceneManager.OpenScene(P13RecruitmentSetup.BootstrapScenePath, OpenSceneMode.Single); RecruitmentScreenPresenter[] screens = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<RecruitmentScreenPresenter>(true)).ToArray(); RecruitmentEntryButton[] entries = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<RecruitmentEntryButton>(true)).ToArray(); if (screens.Length != 1 || entries.Length != 1) throw new BuildFailedException($"P13_SCENE_ENTRY_INVALID: screens={screens.Length}, entries={entries.Length}");
            RecruitmentEntryButton entry = entries[0]; Button button = entry.GetComponent<Button>();
            bool hasEntryBinding = button != null && Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index => button.onClick.GetPersistentTarget(index) == entry && button.onClick.GetPersistentMethodName(index) == nameof(RecruitmentEntryButton.OpenRecruitment));
            if (!hasEntryBinding) throw new BuildFailedException("P13_SCENE_ENTRY_BINDING_INVALID");
        }
    }

    public static class P13CaptureGenerator
    {
        [MenuItem("Kingdom Tycoon/P13/Generate Acceptance Captures")]
        public static void Run()
        {
            P13GeneratedAssetVerifier.Verify(); Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P13RecruitmentSetup.ScreenPath); GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); root.SetActive(true); P13RecruitmentSetup.PrepareRoot(root); Transform[] nodes = root.GetComponentsInChildren<Transform>(true); nodes.Single(value => value.name == "P13_STATE_PANEL").gameObject.SetActive(false);
            string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "reports", "captures", "P13")); Directory.CreateDirectory(output);
            Capture(root, output, "p13_01_tavern_candidates.png", 1920, 1080);
            nodes.Single(value => value.name == "P13_TAVERN_ROOT").gameObject.SetActive(false); nodes.Single(value => value.name == "P13_SPECIAL_ROOT").gameObject.SetActive(true); Capture(root, output, "p13_02_special_pity.png", 1920, 1080);
            nodes.Single(value => value.name == "BannerTitle").GetComponent<TMP_Text>().text = "전사 직업 확률 상승"; nodes.Single(value => value.name == "P13_PITY_S").GetComponent<TMP_Text>().text = "S 이상 보장  9 / 10"; nodes.Single(value => value.name == "P13_PITY_S_FILL").GetComponent<RectTransform>().anchorMax = new Vector2(.9f, 1); Capture(root, output, "p13_03_rate_up_near_pity.png", 1920, 1080);
            nodes.Single(value => value.name == "P13_SPECIAL_ROOT").gameObject.SetActive(false); nodes.Single(value => value.name == "P13_HISTORY_ROOT").gameObject.SetActive(true); nodes.Single(value => value.name == "P13_HISTORY_LIST").GetComponent<TMP_Text>().text = "<color=#D9B96D>특별 모집</color>    S 용병    비용 1    2026-07-19 12:00 UTC\n\n<color=#D9B96D>주점 고용</color>    B 용병    비용 750    2026-07-19 11:54 UTC"; Capture(root, output, "p13_04_history.png", 1920, 1080);
            nodes.Single(value => value.name == "P13_HISTORY_ROOT").gameObject.SetActive(false); nodes.Single(value => value.name == "P13_TAVERN_ROOT").gameObject.SetActive(true); Capture(root, output, "p13_05_20x9.png", 2400, 1080); Debug.Log($"P13_CAPTURES={output}; count=5");
        }
        private static void Capture(GameObject root, string directory, string filename, int width, int height)
        {
            root.SetActive(true); P13RecruitmentSetup.PrepareRoot(root); RectTransform rootRect = root.GetComponent<RectTransform>(); rootRect.sizeDelta = new Vector2(width, height); rootRect.position = Vector3.zero; Canvas canvas = root.GetComponent<Canvas>(); canvas.enabled = true;
            var cameraObject = new GameObject("P13CaptureCamera", typeof(Camera)); Camera camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(8, 13, 16, 255); camera.orthographic = true; camera.orthographicSize = height / 2f; camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.transform.position = new Vector3(0, 0, -10);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target; canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(false, false); string path = Path.Combine(directory, filename); File.WriteAllBytes(path, texture.EncodeToPNG()); RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(cameraObject); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; if (new FileInfo(path).Length <= 1024) throw new BuildFailedException("P13_CAPTURE_FAILED: " + filename);
        }
    }

    public static class P13AndroidBuilder
    {
        [MenuItem("Kingdom Tycoon/P13/Build Android Development APK")]
        public static void Build()
        {
            P13GeneratedAssetVerifier.Verify(); string output = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Builds", "Android", "KingdomTycoon-P13-Development.apk")); Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("P13 Android output invalid.")); UnityEditor.PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP); UnityEditor.PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = EditorBuildSettings.scenes.Where(value => value.enabled).Select(value => value.path).ToArray(), locationPathName = output, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.Development }); if (report.summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException($"P13 Android build failed: {report.summary.result}, errors={report.summary.totalErrors}"); Debug.Log($"P13_ANDROID_APK={output}; bytes={new FileInfo(output).Length}");
        }
    }

    internal sealed class P13RecruitmentBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 130;
        public void OnPreprocessBuild(BuildReport report) => P13GeneratedAssetVerifier.Verify();
    }
}
