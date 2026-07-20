using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Presentation.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Combat
{
    /// <summary>Portrait-first continuous kingdom and hunting world. Mutations are delegated to application services.</summary>
    public sealed class ContinuousHuntScreenPresenter : MonoBehaviour
    {
        private readonly List<MemberButton> memberButtons = new();
        private readonly List<ActorView> actorViews = new();
        private readonly Dictionary<string, RegionView> regionViews = new(StringComparer.Ordinal);
        private readonly WorldHuntFeedbackTracker feedbackTracker = new();
        private readonly List<FeedbackRow> feedbackRows = new();
        private ContinuousHuntGameService service;
        private UnifiedNavigationMenu navigation;
        private WorldHuntFeedbackAudio feedbackAudio;
        private WorldHuntPixelArtLibrary visuals;
        private MobileWorldAssetLibrary externalVisuals;
        private ContinuousHuntOverviewDto overview;
        private TMP_Text summary;
        private TMP_Text selection;
        private TMP_Text statusLine;
        private RectTransform worldViewport;
        private RectTransform worldContent;
        private WorldMapDragSurface dragSurface;
        private GameObject assignmentSheet;
        private GameObject menuPanel;
        private GameObject feedbackRail;
        private Button soundButton;
        private Button motionButton;
        private string selectedRegionId = "REGION_R01";
        private float nextTick;

        public RectTransform WorldViewport => worldViewport;
        public RectTransform WorldContent => worldContent;
        public WorldMapDragSurface DragSurface => dragSurface;
        public string SelectedRegionId => selectedRegionId;
        public bool AssignmentSheetOpen => assignmentSheet != null && assignmentSheet.activeSelf;
        public bool MobileMenuOpen => menuPanel != null && menuPanel.activeSelf;
        public bool SoundEnabled => feedbackAudio != null && feedbackAudio.SoundEnabled;
        public bool ReducedMotion => WorldHuntFeedbackPreferences.ReducedMotion;
        public IReadOnlyList<TMP_Text> FeedbackTexts => feedbackRows.Select(value => value.Text).ToArray();
        public int PixelArtSpriteCount => visuals?.SpriteCount ?? 0;
        public int ExternalSpriteCount => externalVisuals?.LoadedSpriteCount ?? 0;

        public static ContinuousHuntScreenPresenter Install()
        {
            ContinuousHuntScreenPresenter current = FindFirstObjectByType<ContinuousHuntScreenPresenter>(FindObjectsInactive.Include);
            if (current != null) return current;
            var root = new GameObject("CONTINUOUS_HUNT_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ContinuousHuntScreenPresenter));
            if (AppRoot.Instance != null) root.transform.SetParent(AppRoot.Instance.transform, false); else DontDestroyOnLoad(root);
            return root.GetComponent<ContinuousHuntScreenPresenter>();
        }

        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 850;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = .5f;
            RectTransform root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            visuals = new WorldHuntPixelArtLibrary();
            externalVisuals = new MobileWorldAssetLibrary();
            feedbackAudio = gameObject.AddComponent<WorldHuntFeedbackAudio>();
            BuildUi();
            UpdatePreferenceLabels();
            gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            navigation ??= FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include);
            navigation?.SetNavigationVisible(false);
            feedbackTracker.Reset();
            ClearFeedbackRows();
            if (service == null)
            {
                if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized)
                {
                    ShowStatus("게임 정보를 준비하고 있습니다.");
                    return;
                }
                service = AppRoot.Instance.Services.Get<ContinuousHuntGameService>();
                service.Changed += OnChanged;
            }
            Refresh();
            assignmentSheet.SetActive(false);
            menuPanel.SetActive(false);
            Canvas.ForceUpdateCanvases();
            dragSurface.ResetView();
        }

        public void Close()
        {
            assignmentSheet?.SetActive(false);
            menuPanel?.SetActive(false);
            gameObject.SetActive(false);
        }

        private void Update()
        {
            AnimateWorld();
            if (service == null || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            try { service.AdvanceTo(DateTimeOffset.UtcNow); }
            catch (Exception exception)
            {
                Debug.LogWarning(exception);
                ShowStatus("사냥 기록을 갱신하지 못했습니다. 저장 상태를 유지하고 다시 시도합니다.");
            }
        }

        private void BuildUi()
        {
            Image background = Panel("월드배경", transform, new Color32(8, 18, 20, 255), Vector2.zero, Vector2.one);
            Image safe = Panel("안전영역", background.transform, Color.clear, Vector2.zero, Vector2.one);
            safe.gameObject.AddComponent<KingdomTycoon.UI.SafeAreaFitter>();

            Image viewportImage = Panel("월드뷰포트", safe.transform, new Color32(30, 58, 50, 255), Vector2.zero, Vector2.one);
            viewportImage.raycastTarget = true;
            viewportImage.gameObject.AddComponent<RectMask2D>();
            worldViewport = viewportImage.rectTransform;
            dragSurface = viewportImage.gameObject.AddComponent<WorldMapDragSurface>();
            var contentObject = new GameObject("드래그월드", typeof(RectTransform));
            contentObject.transform.SetParent(viewportImage.transform, false);
            worldContent = (RectTransform)contentObject.transform;
            worldContent.anchorMin = worldContent.anchorMax = worldContent.pivot = new Vector2(.5f, .5f);
            worldContent.sizeDelta = MobileLivingWorldLayout.WorldSize;
            worldContent.anchoredPosition = Vector2.zero;
            dragSurface.Configure(worldViewport, worldContent);
            BuildWorldBase();

            Image hud = Panel("왕국상태", safe.transform, new Color32(9, 22, 24, 225), new Vector2(.02f, .905f), new Vector2(.61f, .985f));
            Text("화면제목", hud.transform, "통합 사냥 월드 · 상시 자동 사냥", 25, TextAlignmentOptions.Left, new Vector2(.045f, .47f), new Vector2(.95f, .94f), new Color32(244, 211, 127, 255));
            summary = Text("전체요약", hud.transform, "배치 0명 · 누적 수익 0골드", 18, TextAlignmentOptions.Left, new Vector2(.045f, .07f), new Vector2(.95f, .48f), new Color32(199, 220, 209, 255));

            Button home = MakeButton("왕국복귀", safe.transform, "왕국", new Color32(61, 84, 70, 245), new Vector2(.64f, .905f), new Vector2(.745f, .985f));
            Button news = MakeButton("소식버튼", safe.transform, "소식", new Color32(58, 79, 88, 245), new Vector2(.755f, .905f), new Vector2(.86f, .985f));
            Button menu = MakeButton("통합메뉴버튼", safe.transform, "메뉴", new Color32(83, 61, 92, 245), new Vector2(.87f, .905f), new Vector2(.98f, .985f));
            home.GetComponentInChildren<TMP_Text>().fontSize = 19;
            home.onClick.AddListener(() => { dragSurface.ResetView(); ShowStatus("중앙 왕국으로 돌아왔습니다."); });
            news.onClick.AddListener(() => feedbackRail.SetActive(!feedbackRail.activeSelf));
            menu.onClick.AddListener(() => { assignmentSheet.SetActive(false); menuPanel.SetActive(!menuPanel.activeSelf); });

            BuildFeedbackRail(safe.transform);
            BuildMobileMenu(safe.transform);
            BuildAssignmentSheet(safe.transform);
        }

        private void BuildWorldBase()
        {
            Panel("월드지면", worldContent, new Color32(51, 82, 58, 255), Vector2.zero, Vector2.one).raycastTarget = false;
            BuildRoad("북쪽길", new Vector2(0f, 660f), new Vector2(150f, 820f), 0f);
            BuildRoad("남쪽길", new Vector2(0f, -690f), new Vector2(150f, 900f), 0f);
            BuildRoad("동쪽길", new Vector2(600f, 170f), new Vector2(850f, 140f), -10f);
            BuildRoad("서쪽길", new Vector2(-600f, 170f), new Vector2(850f, 140f), 10f);

            Image kingdom = WorldPanel("왕국거점", worldContent, new Color32(64, 92, 65, 255), MobileLivingWorldLayout.KingdomCenter, new Vector2(1120f, 930f));
            var outline = kingdom.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(225, 190, 104, 170);
            outline.effectDistance = new Vector2(5f, -5f);
            Text("왕국이름", kingdom.transform, "중앙 왕국", 32, TextAlignmentOptions.Center, new Vector2(.28f, .88f), new Vector2(.72f, .98f), new Color32(248, 219, 134, 255));
            Text("왕국설명", kingdom.transform, "용병이 쉬고, 거래하고, 성장하는 생활 거점", 17, TextAlignmentOptions.Center, new Vector2(.18f, .80f), new Vector2(.82f, .88f), new Color32(217, 228, 204, 255));
            Image landmark = PixelImage("왕국픽셀랜드마크", kingdom.transform, visuals.KingdomSprite, new Vector2(.39f, .33f), new Vector2(.61f, .70f));
            landmark.color = new Color(1f, 1f, 1f, .62f);
            landmark.raycastTarget = false;
            BuildFacility(kingdom.transform, "FAC_TAVERN", "주점", MobileLivingWorldLayout.FacilityPosition("FAC_TAVERN"), false, new Color32(178, 113, 66, 255));
            BuildFacility(kingdom.transform, "FAC_STORE", "상점", MobileLivingWorldLayout.FacilityPosition("FAC_STORE"), false, new Color32(207, 151, 64, 255));
            BuildFacility(kingdom.transform, "FAC_BLACKSMITH", "대장간", MobileLivingWorldLayout.FacilityPosition("FAC_BLACKSMITH"), false, new Color32(154, 83, 62, 255));
            BuildFacility(kingdom.transform, "FAC_INFIRMARY", "치료소", MobileLivingWorldLayout.FacilityPosition("FAC_INFIRMARY"), false, new Color32(85, 151, 137, 255));
            BuildFacility(kingdom.transform, "FAC_GUILD", "모험가 길드", MobileLivingWorldLayout.FacilityPosition("FAC_GUILD"), true, new Color32(100, 117, 153, 255));
            for (int index = 0; index < 8; index++) actorViews.Add(CreateActorView(worldContent, index));
        }

        private void BuildRoad(string name, Vector2 position, Vector2 size, float rotation)
        {
            Image road = WorldPanel(name, worldContent, new Color32(130, 103, 69, 255), position, size);
            road.raycastTarget = false;
            road.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void BuildFacility(Transform kingdom, string id, string label, Vector2 worldPosition, bool large, Color tint)
        {
            Vector2 localPosition = worldPosition - MobileLivingWorldLayout.KingdomCenter;
            var root = new GameObject("시설_" + id, typeof(RectTransform));
            root.transform.SetParent(kingdom, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = large ? new Vector2(230f, 210f) : new Vector2(190f, 170f);
            rect.anchoredPosition = localPosition;
            Sprite external = externalVisuals.Facility(large);
            Image building = PixelImage("무료시설", rect, external ?? visuals.KingdomSprite, new Vector2(.05f, .16f), new Vector2(.95f, .96f));
            building.color = external == null ? tint : Color.Lerp(Color.white, tint, .22f);
            Text("시설명", rect, label, 19, TextAlignmentOptions.Center, new Vector2(0f, 0f), new Vector2(1f, .22f), Color.white);
        }

        private void BuildFeedbackRail(Transform parent)
        {
            Image rail = Panel("최근피드", parent, new Color32(8, 20, 23, 225), new Vector2(.61f, .69f), new Vector2(.98f, .895f));
            feedbackRail = rail.gameObject;
            rail.raycastTarget = false;
            Text("최근피드제목", rail.transform, "전투 · 보상 소식", 18, TextAlignmentOptions.Left, new Vector2(.06f, .80f), new Vector2(.94f, .96f), new Color32(242, 210, 126, 255));
            for (int index = 0; index < 5; index++)
            {
                float top = .78f - index * .145f;
                TMP_Text row = Text("최근피드_" + index, rail.transform, string.Empty, 15, TextAlignmentOptions.Left,
                    new Vector2(.06f, top - .12f), new Vector2(.94f, top), new Color32(207, 221, 212, 255));
                row.gameObject.SetActive(false);
                feedbackRows.Add(new FeedbackRow(row));
            }
            feedbackRail.SetActive(false);
        }

        private void BuildMobileMenu(Transform parent)
        {
            Image panel = Panel("우측상단메뉴", parent, new Color32(12, 27, 30, 250), new Vector2(.42f, .34f), new Vector2(.98f, .895f));
            menuPanel = panel.gameObject;
            Text("메뉴제목", panel.transform, "왕국 관리", 27, TextAlignmentOptions.Left, new Vector2(.06f, .89f), new Vector2(.72f, .98f), new Color32(244, 207, 119, 255));
            Text("메뉴설명", panel.transform, "사냥 · 스킬 훈련 · 장비 강화", 15, TextAlignmentOptions.Left, new Vector2(.06f, .82f), new Vector2(.94f, .89f), new Color32(168, 199, 187, 255));
            Button close = MakeButton("메뉴닫기", panel.transform, "닫기", new Color32(86, 67, 56, 255), new Vector2(.75f, .89f), new Vector2(.95f, .98f));
            close.onClick.AddListener(() => menuPanel.SetActive(false));
            AddMenuButton(panel.transform, "메뉴_용병", "용병", 0, () => Navigation()?.OpenMercenaries());
            AddMenuButton(panel.transform, "메뉴_가방", "가방", 1, () => Navigation()?.OpenInventory());
            AddMenuButton(panel.transform, "메뉴_상점", "상점", 2, () => Navigation()?.OpenStore());
            AddMenuButton(panel.transform, "메뉴_제작", "제작", 3, () => Navigation()?.OpenProduction());
            AddMenuButton(panel.transform, "메뉴_장비", "장비 공방", 4, () => Navigation()?.OpenEquipmentGrowth());
            AddMenuButton(panel.transform, "메뉴_승급", "승급", 5, () => Navigation()?.OpenProgression());
            AddMenuButton(panel.transform, "메뉴_모집", "모집", 6, () => Navigation()?.OpenRecruitment());
            AddMenuButton(panel.transform, "메뉴_레이드", "레이드", 7, () => Navigation()?.OpenRaids());
            AddMenuButton(panel.transform, "메뉴_보고", "왕국 보고", 8, () => Navigation()?.OpenReport());
            AddMenuButton(panel.transform, "메뉴_설정", "설정", 9, () => { soundButton.gameObject.SetActive(!soundButton.gameObject.activeSelf); motionButton.gameObject.SetActive(soundButton.gameObject.activeSelf); });
            soundButton = MakeButton("효과음설정", panel.transform, "효과음 켬", new Color32(45, 83, 77, 255), new Vector2(.06f, .015f), new Vector2(.48f, .12f));
            motionButton = MakeButton("모션설정", panel.transform, "모션 보통", new Color32(56, 76, 84, 255), new Vector2(.52f, .015f), new Vector2(.94f, .12f));
            soundButton.onClick.AddListener(ToggleSound);
            motionButton.onClick.AddListener(ToggleMotion);
            soundButton.gameObject.SetActive(false);
            motionButton.gameObject.SetActive(false);
            menuPanel.SetActive(false);
        }

        private void AddMenuButton(Transform parent, string name, string label, int index, Action action)
        {
            int column = index % 2;
            int row = index / 2;
            float left = column == 0 ? .06f : .52f;
            float top = .81f - row * .135f;
            Button button = MakeButton(name, parent, label, column == 0 ? new Color32(48, 86, 78, 255) : new Color32(75, 70, 91, 255),
                new Vector2(left, top - .105f), new Vector2(left + .42f, top));
            button.onClick.AddListener(() => { menuPanel.SetActive(false); action?.Invoke(); });
        }

        private void BuildAssignmentSheet(Transform parent)
        {
            Image drawer = Panel("사냥터배치시트", parent, new Color32(13, 29, 32, 252), new Vector2(.02f, .02f), new Vector2(.98f, .45f));
            assignmentSheet = drawer.gameObject;
            selection = Text("선택지역", drawer.transform, "사냥터를 선택해 주세요.", 25, TextAlignmentOptions.Left, new Vector2(.04f, .86f), new Vector2(.78f, .97f), new Color32(241, 208, 128, 255));
            Button close = MakeButton("배치시트닫기", drawer.transform, "닫기", new Color32(88, 68, 55, 255), new Vector2(.80f, .86f), new Vector2(.96f, .97f));
            close.onClick.AddListener(() => assignmentSheet.SetActive(false));
            statusLine = Text("상태안내", drawer.transform, "상시 자동 사냥 · 판매 · 치료 · 스킬 훈련 · 장비 강화가 계속 이어집니다.", 17, TextAlignmentOptions.Left, new Vector2(.04f, .74f), new Vector2(.96f, .86f), new Color32(170, 200, 190, 255));
            for (int index = 0; index < 8; index++) memberButtons.Add(CreateMemberButton(drawer.transform, index));
            assignmentSheet.SetActive(false);
        }

        private MemberButton CreateMemberButton(Transform parent, int index)
        {
            int column = index % 2;
            int row = index / 2;
            float left = column == 0 ? .04f : .51f;
            float top = .71f - row * .17f;
            Button button = MakeButton("용병_" + index, parent, string.Empty, new Color32(42, 62, 62, 255), new Vector2(left, top - .14f), new Vector2(left + .45f, top));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.fontSize = 16;
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.margin = new Vector4(5, 4, 4, 4);
            SetRect(label.rectTransform, new Vector2(.24f, .03f), new Vector2(.99f, .97f));
            Image icon = PixelImage("직업아이콘", button.transform, visuals.JobSprite("JOB_WARRIOR"), new Vector2(.025f, .16f), new Vector2(.23f, .84f));
            icon.color = Color.white;
            var rowView = new MemberButton(button, label, icon);
            button.onClick.AddListener(() => ToggleAssignment(rowView));
            button.gameObject.SetActive(false);
            return rowView;
        }

        private UnifiedNavigationMenu Navigation()
        {
            navigation ??= FindFirstObjectByType<UnifiedNavigationMenu>(FindObjectsInactive.Include);
            return navigation;
        }

        private void EnsureRegionViews()
        {
            if (overview == null) return;
            foreach (WorldHuntRegionDto region in overview.Regions)
            {
                if (!regionViews.TryGetValue(region.Id, out RegionView view))
                {
                    view = CreateRegionView(region);
                    regionViews.Add(region.Id, view);
                }
                view.Root.anchoredPosition = MobileLivingWorldLayout.RegionPosition(region.Id);
            }
            foreach (ActorView actor in actorViews) actor.Rect.SetAsLastSibling();
        }

        private RegionView CreateRegionView(WorldHuntRegionDto region)
        {
            Sprite groundSprite = externalVisuals.Ground(region.Theme);
            Image rootImage = WorldPanel("월드지역_" + region.Id, worldContent, groundSprite == null ? ThemeColor(region.Theme) : Color.white,
                MobileLivingWorldLayout.RegionPosition(region.Id), new Vector2(720f, 720f));
            if (groundSprite != null)
            {
                rootImage.name = "월드지역_" + region.Id;
                rootImage.sprite = groundSprite;
                rootImage.type = Image.Type.Tiled;
                rootImage.pixelsPerUnitMultiplier = .35f;
            }
            Button button = rootImage.gameObject.AddComponent<Button>();
            button.targetGraphic = rootImage;
            var outline = rootImage.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(4f, -4f);
            outline.effectColor = new Color32(232, 197, 115, 0);
            button.onClick.AddListener(() => SelectRegion(region.Id));
            TMP_Text title = Text("지역명", rootImage.transform, region.DisplayName, 26, TextAlignmentOptions.Left, new Vector2(.04f, .88f), new Vector2(.70f, .98f), new Color32(250, 231, 179, 255));
            TMP_Text meta = Text("지역정보", rootImage.transform, string.Empty, 16, TextAlignmentOptions.Right, new Vector2(.58f, .88f), new Vector2(.96f, .98f), new Color32(225, 235, 219, 255));
            Image riskBackground = Panel("위험도배지", rootImage.transform, RiskColor(region.Tier), new Vector2(.04f, .78f), new Vector2(.28f, .865f));
            TMP_Text risk = Text("위험도", riskBackground.transform, region.RiskLabel, 17, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white);
            TMP_Text drop = Text("주요전리품", rootImage.transform, region.DropPreview, 15, TextAlignmentOptions.Left, new Vector2(.31f, .78f), new Vector2(.96f, .865f), new Color32(227, 214, 174, 255));
            var decorations = new List<Image>();
            for (int index = 0; index < 3; index++)
            {
                Sprite decorationSprite = externalVisuals.Decoration(index);
                Image decoration = PixelImage("환경장식_무료_" + index, rootImage.transform, decorationSprite ?? visuals.DecorationSprite(region.Theme),
                    new Vector2(.08f + index * .29f, .56f - (index % 2) * .08f), new Vector2(.22f + index * .29f, .75f - (index % 2) * .08f));
                decoration.raycastTarget = false;
                decoration.color = decorationSprite == null ? Color.white : new Color(1f, 1f, 1f, .92f);
                decorations.Add(decoration);
            }
            var monsters = new List<MonsterView>();
            for (int index = 0; index < 5; index++) monsters.Add(CreateMonsterView(rootImage.transform, index));
            GameObject locked = Panel("잠금", rootImage.transform, new Color32(12, 18, 21, 225), new Vector2(.03f, .03f), new Vector2(.97f, .97f)).gameObject;
            Text("잠금문구", locked.transform, "아직 개방되지 않은 사냥터입니다.", 23, TextAlignmentOptions.Center, new Vector2(.08f, .35f), new Vector2(.92f, .65f), Color.white);
            return new RegionView(rootImage.rectTransform, rootImage, outline, title, meta, riskBackground, risk, drop, locked, decorations, monsters);
        }

        private MonsterView CreateMonsterView(Transform parent, int index)
        {
            var root = new GameObject("몬스터동작_" + index, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(132f, 104f);
            rect.anchoredPosition = MonsterBasePosition(index);
            Image pulse = Panel("타격펄스", root.transform, new Color(1f, 1f, 1f, 0f), new Vector2(.02f, .10f), new Vector2(.48f, .88f));
            pulse.raycastTarget = false;
            Image body = PixelImage("몬스터몸", root.transform, visuals.MonsterSprite("MEADOW", false), new Vector2(.04f, .13f), new Vector2(.46f, .88f));
            body.color = Color.white;
            TMP_Text label = Text("몬스터이름", root.transform, string.Empty, 13, TextAlignmentOptions.Left, new Vector2(.45f, .32f), new Vector2(1f, .90f), Color.white);
            Image hpBackground = Panel("몬스터체력", root.transform, new Color32(22, 26, 27, 255), new Vector2(.45f, .16f), new Vector2(.98f, .29f));
            Image hp = Panel("몬스터현재체력", hpBackground.transform, new Color32(186, 65, 57, 255), Vector2.zero, Vector2.one);
            hp.rectTransform.anchorMax = new Vector2(1f, 1f);
            TMP_Text feedback = Text("피해표시", root.transform, string.Empty, 18, TextAlignmentOptions.Center, new Vector2(.08f, .76f), new Vector2(.96f, 1.28f), new Color32(255, 236, 190, 255));
            feedback.gameObject.SetActive(false);
            Outline eliteOutline = body.gameObject.AddComponent<Outline>();
            eliteOutline.effectDistance = new Vector2(3f, -3f);
            eliteOutline.enabled = false;
            return new MonsterView(root, rect, body, label, hp, pulse, feedback, eliteOutline, index, rect.anchoredPosition);
        }

        private ActorView CreateActorView(Transform parent, int index)
        {
            var root = new GameObject("용병동작_" + index, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(170f, 92f);
            Image shadow = Panel("용병그림자", root.transform, new Color(0f, 0f, 0f, .28f), new Vector2(.04f, .05f), new Vector2(.40f, .20f));
            shadow.raycastTarget = false;
            Image body = PixelImage("용병몸", root.transform, visuals.JobSprite("JOB_WARRIOR"), new Vector2(.02f, .08f), new Vector2(.42f, .98f));
            body.color = Color.white;
            TMP_Text label = Text("용병이름", root.transform, string.Empty, 14, TextAlignmentOptions.Left, new Vector2(.40f, 0f), new Vector2(1f, 1f), Color.white);
            root.SetActive(false);
            return new ActorView(root, rect, body, label, index);
        }

        private void SelectRegion(string regionId)
        {
            if (dragSurface.ConsumeTapSuppression()) return;
            selectedRegionId = regionId;
            assignmentSheet.SetActive(true);
            menuPanel.SetActive(false);
            Refresh(overview);
        }

        private void ToggleAssignment(MemberButton row)
        {
            if (row.Member == null || service == null) return;
            try
            {
                if (row.Member.AssignedRegionId == selectedRegionId)
                {
                    service.Unassign(row.Member.InstanceId);
                    ShowStatus($"{row.Member.DisplayName}의 사냥터 배치를 해제했습니다.");
                }
                else
                {
                    service.Assign(row.Member.InstanceId, selectedRegionId);
                    ShowStatus($"{row.Member.DisplayName}을(를) 이 사냥터에 배치했습니다.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(exception);
                ShowStatus("배치를 변경하지 못했습니다. 해금 상태와 배치 인원을 확인해 주세요.");
            }
        }

        private void Refresh(ContinuousHuntOverviewDto snapshot = null)
        {
            if (service == null) return;
            overview = snapshot ?? service.GetOverview();
            feedbackAudio.Configure(overview.Feedback);
            IReadOnlyList<WorldHuntFeedbackEvent> feedback = feedbackTracker.Observe(overview);
            EnsureRegionViews();
            summary.text = $"배치 {overview.AssignedCount}명 · 누적 수익 {overview.EarnedGold:N0}골드";
            WorldHuntRegionDto selected = overview.Regions.FirstOrDefault(value => value.Id == selectedRegionId) ?? overview.Regions.First();
            selectedRegionId = selected.Id;
            selection.text = $"{selected.DisplayName} · {MobileLivingWorldLayout.DirectionOf(selected.Id)} · 배치 {selected.AssignedCount}/{selected.MaxActive}";
            for (int index = 0; index < memberButtons.Count; index++)
            {
                MemberButton row = memberButtons[index];
                if (index >= overview.Members.Count) { row.Button.gameObject.SetActive(false); continue; }
                ContinuousHuntMemberDto member = overview.Members[index];
                row.Member = member;
                row.Button.gameObject.SetActive(true);
                bool here = member.AssignedRegionId == selectedRegionId;
                string location = member.AssignedRegionId == null ? "왕국" : overview.Regions.First(value => value.Id == member.AssignedRegionId).DisplayName;
                row.Label.text = $"{member.DisplayName} · {Job(member.JobId)}\n체력 {member.CurrentHpBps / 100f:0}% · {location}\n{(here ? "배치 해제" : "이곳에 배치")}";
                row.Icon.sprite = visuals.JobSprite(member.JobId);
                row.Button.targetGraphic.color = here ? new Color32(42, 112, 83, 255) : new Color32(42, 62, 62, 255);
            }
            foreach (WorldHuntRegionDto region in overview.Regions) RefreshRegion(region, regionViews[region.Id]);
            RefreshActors();
            HandleFeedback(feedback);
            if (feedback.Count == 0)
                ShowStatus(selected.Unlocked ? "용병을 눌러 배치하거나 해제하세요. 여러 명을 함께 배치할 수 있습니다." : "왕국 진행으로 개방해야 배치할 수 있습니다.");
        }

        private void RefreshRegion(WorldHuntRegionDto region, RegionView view)
        {
            bool selected = region.Id == selectedRegionId;
            view.Outline.effectColor = selected ? new Color32(244, 208, 119, 230) : new Color32(232, 197, 115, 0);
            view.Title.text = region.DisplayName;
            view.Meta.text = $"티어 {region.Tier} · 전투력 {region.RecommendedPower:N0}\n배치 {region.AssignedCount}/{region.MaxActive}";
            view.RiskBackground.color = RiskColor(region.Tier);
            view.Risk.text = region.RiskLabel;
            view.Drop.text = "주요 " + region.DropPreview;
            view.Locked.SetActive(!region.Unlocked);
            WorldHuntMonsterDto[] monsters = overview.Monsters.Where(value => value.RegionId == region.Id).OrderBy(value => value.SpawnSlot).ToArray();
            for (int index = 0; index < view.Monsters.Count; index++)
            {
                MonsterView monsterView = view.Monsters[index];
                if (index >= monsters.Length) { monsterView.Root.SetActive(false); monsterView.Monster = null; continue; }
                WorldHuntMonsterDto monster = monsters[index];
                monsterView.Root.SetActive(region.Unlocked);
                monsterView.Monster = monster;
                bool elite = string.Equals(monster.Type, "ELITE", StringComparison.Ordinal);
                monsterView.Body.sprite = visuals.MonsterSprite(region.Theme, elite);
                monsterView.BaseColor = monster.State == "RESPAWNING" ? new Color(1f, 1f, 1f, .36f) : Color.white;
                monsterView.Body.color = monsterView.BaseColor;
                monsterView.EliteOutline.enabled = elite;
                monsterView.EliteOutline.effectColor = new Color32(202, 132, 235, 255);
                monsterView.Label.text = monster.State == "RESPAWNING" ? $"{monster.DisplayName}\n재등장 준비" : $"{(elite ? "정예 " : string.Empty)}{monster.DisplayName}\n레벨 {monster.Level}";
                monsterView.Hp.rectTransform.anchorMax = new Vector2(monster.HpBps / 10000f, 1f);
            }
        }

        private void RefreshActors()
        {
            for (int index = 0; index < actorViews.Count; index++)
            {
                ActorView view = actorViews[index];
                if (index >= overview.Members.Count) { view.Root.SetActive(false); view.Member = null; continue; }
                ContinuousHuntMemberDto member = overview.Members[index];
                bool wasActive = view.Root.activeSelf;
                view.Root.SetActive(true);
                view.Member = member;
                view.Body.sprite = visuals.JobSprite(member.JobId);
                view.Label.text = $"{member.DisplayName}\n{State(member.State)}";
                view.Target = MobileLivingWorldLayout.TargetFor(member);
                if (!wasActive || !view.Initialized)
                {
                    view.Rect.anchoredPosition = view.Target;
                    view.Initialized = true;
                }
            }
        }

        private void AnimateWorld()
        {
            if (overview == null) return;
            float now = Time.unscaledTime;
            UpdateFeedbackRows(now);
            foreach (RegionView region in regionViews.Values)
            {
                foreach (MonsterView monster in region.Monsters)
                {
                    if (!monster.Root.activeSelf || monster.Monster == null) continue;
                    float phase = now * .55f + monster.Index * 1.37f;
                    Vector2 roam = ReducedMotion || monster.Monster.State == "RESPAWNING" ? Vector2.zero : new Vector2(Mathf.Sin(phase) * 13f, Mathf.Cos(phase * .73f) * 8f);
                    monster.Rect.anchoredPosition = monster.BasePosition + roam;
                    monster.Body.color = now < monster.FlashUntil ? Color.Lerp(monster.BaseColor, Color.white, .86f) : monster.BaseColor;
                    float pulse = now < monster.PulseUntil ? 1f - Mathf.Clamp01((now - monster.PulseStarted) / Mathf.Max(.01f, monster.PulseUntil - monster.PulseStarted)) : 0f;
                    Color pulseColor = monster.Pulse.color; pulseColor.a = pulse * .68f; monster.Pulse.color = pulseColor;
                    if (monster.Feedback.gameObject.activeSelf)
                    {
                        float duration = Mathf.Max(.01f, monster.FeedbackUntil - monster.FeedbackStarted);
                        float progress = Mathf.Clamp01((now - monster.FeedbackStarted) / duration);
                        monster.Feedback.rectTransform.anchoredPosition = ReducedMotion ? Vector2.zero : new Vector2(0f, progress * 34f);
                        Color color = monster.FeedbackColor; color.a = 1f - progress; monster.Feedback.color = color;
                        if (now >= monster.FeedbackUntil) monster.Feedback.gameObject.SetActive(false);
                    }
                }
            }
            foreach (ActorView actor in actorViews)
            {
                if (!actor.Root.activeSelf || actor.Member == null) continue;
                float speed = ReducedMotion ? 4f : 2.3f;
                Vector2 livingTarget = actor.Target;
                if (!ReducedMotion && (actor.Member.State is "IDLE_TOWN" or "FIND_TARGET"))
                {
                    float phase = now * .46f + actor.Index * 1.19f;
                    livingTarget += new Vector2(Mathf.Sin(phase) * 34f, Mathf.Cos(phase * .77f) * 20f);
                }
                actor.Rect.anchoredPosition = Vector2.Lerp(actor.Rect.anchoredPosition, livingTarget, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
                float lunge = now < actor.LungeUntil && !ReducedMotion ? Mathf.Sin(Mathf.Clamp01((now - actor.LungeStarted) / Mathf.Max(.01f, actor.LungeUntil - actor.LungeStarted)) * Mathf.PI) * 18f : 0f;
                actor.Body.rectTransform.anchoredPosition = new Vector2(lunge, 0f);
                actor.Rect.SetAsLastSibling();
            }
        }

        private void ToggleSound()
        {
            feedbackAudio.SetSoundEnabled(!feedbackAudio.SoundEnabled);
            UpdatePreferenceLabels();
            ShowStatus(feedbackAudio.SoundEnabled ? "사냥 효과음을 켰습니다." : "사냥 효과음을 껐습니다. 시각 피드백은 유지됩니다.");
        }

        private void ToggleMotion()
        {
            WorldHuntFeedbackPreferences.ReducedMotion = !WorldHuntFeedbackPreferences.ReducedMotion;
            UpdatePreferenceLabels();
            ShowStatus(ReducedMotion ? "모션 감소를 적용했습니다. 숫자와 상태 표시는 유지됩니다." : "기본 전투 모션을 적용했습니다.");
        }

        private void UpdatePreferenceLabels()
        {
            if (soundButton != null) soundButton.GetComponentInChildren<TMP_Text>().text = feedbackAudio != null && feedbackAudio.SoundEnabled ? "효과음 켬" : "효과음 끔";
            if (motionButton != null) motionButton.GetComponentInChildren<TMP_Text>().text = ReducedMotion ? "모션 감소" : "모션 보통";
        }

        private void HandleFeedback(IReadOnlyList<WorldHuntFeedbackEvent> events)
        {
            if (events == null || overview?.Feedback == null) return;
            foreach (WorldHuntFeedbackEvent item in events)
            {
                feedbackAudio.Play(item.Type);
                if (item.IsCombat) TriggerCombatFeedback(item);
                if (!item.IsCombat || item.Type is WorldHuntFeedbackType.MonsterDefeated or WorldHuntFeedbackType.MonsterRespawned) PushFeedbackRow(item);
            }
        }

        private void TriggerCombatFeedback(WorldHuntFeedbackEvent item)
        {
            if (!regionViews.TryGetValue(item.RegionId ?? string.Empty, out RegionView region)) return;
            float now = Time.unscaledTime;
            foreach (MonsterView monster in region.Monsters)
            {
                if (monster.Monster == null || monster.Monster.InstanceId != item.MonsterInstanceId) continue;
                bool skill = item.Type == WorldHuntFeedbackType.SkillHit;
                monster.Feedback.text = item.Type switch
                {
                    WorldHuntFeedbackType.MonsterRespawned => "재등장",
                    WorldHuntFeedbackType.MonsterDefeated => item.Amount > 0 ? $"처치!  -{item.Amount:N0}" : "처치!",
                    _ => $"-{item.Amount:N0}"
                };
                monster.FeedbackColor = item.Type switch
                {
                    WorldHuntFeedbackType.SkillHit => new Color32(255, 210, 95, 255),
                    WorldHuntFeedbackType.MonsterDefeated => new Color32(255, 126, 82, 255),
                    WorldHuntFeedbackType.MonsterRespawned => new Color32(115, 225, 179, 255),
                    _ => new Color32(255, 244, 218, 255)
                };
                monster.Feedback.color = monster.FeedbackColor;
                monster.FeedbackStarted = now;
                monster.FeedbackUntil = now + overview.Feedback.DamageFloatSeconds;
                monster.Feedback.gameObject.SetActive(true);
                monster.FlashStarted = now;
                monster.FlashUntil = now + (skill ? overview.Feedback.SkillPulseSeconds : overview.Feedback.HitFlashSeconds);
                monster.PulseStarted = now;
                monster.PulseUntil = now + (skill || item.Type is WorldHuntFeedbackType.MonsterDefeated or WorldHuntFeedbackType.MonsterRespawned ? overview.Feedback.SkillPulseSeconds : overview.Feedback.HitFlashSeconds);
                Color pulse = monster.FeedbackColor; pulse.a = .7f; monster.Pulse.color = pulse;
                break;
            }
            if (item.Type is not (WorldHuntFeedbackType.BasicHit or WorldHuntFeedbackType.SkillHit or WorldHuntFeedbackType.MonsterDefeated)) return;
            foreach (ActorView actor in actorViews)
            {
                if (actor.Member == null || actor.Member.InstanceId != item.MercenaryInstanceId) continue;
                actor.LungeStarted = now;
                actor.LungeUntil = now + (item.Type == WorldHuntFeedbackType.SkillHit ? overview.Feedback.SkillPulseSeconds : overview.Feedback.HitFlashSeconds + .12f);
                break;
            }
        }

        private void PushFeedbackRow(WorldHuntFeedbackEvent item)
        {
            int limit = Math.Min(feedbackRows.Count, overview.Feedback.MaximumRewardFeedEntries);
            for (int index = limit - 1; index > 0; index--)
            {
                feedbackRows[index].Text.text = feedbackRows[index - 1].Text.text;
                feedbackRows[index].Color = feedbackRows[index - 1].Color;
                feedbackRows[index].ExpiresAt = feedbackRows[index - 1].ExpiresAt;
                feedbackRows[index].Text.color = feedbackRows[index].Color;
                feedbackRows[index].Text.gameObject.SetActive(feedbackRows[index - 1].Text.gameObject.activeSelf);
            }
            FeedbackRow first = feedbackRows[0];
            first.Text.text = item.Message;
            first.Color = FeedbackColor(item.Type);
            first.Text.color = first.Color;
            first.ExpiresAt = Time.unscaledTime + overview.Feedback.RewardFeedSeconds;
            first.Text.gameObject.SetActive(true);
        }

        private void UpdateFeedbackRows(float now)
        {
            foreach (FeedbackRow row in feedbackRows)
            {
                if (!row.Text.gameObject.activeSelf) continue;
                if (now >= row.ExpiresAt) { row.Text.gameObject.SetActive(false); continue; }
                Color color = row.Color;
                float remaining = row.ExpiresAt - now;
                color.a = overview?.Feedback != null && remaining < .5f ? Mathf.Clamp01(remaining / .5f) : 1f;
                row.Text.color = color;
            }
        }

        private void ClearFeedbackRows()
        {
            foreach (FeedbackRow row in feedbackRows)
            {
                row.Text.text = string.Empty;
                row.ExpiresAt = 0f;
                row.Text.gameObject.SetActive(false);
            }
        }

        private void OnChanged(object sender, ContinuousHuntOverviewDto value)
        {
            if (gameObject.activeInHierarchy) Refresh(value); else overview = value;
        }

        private void ShowStatus(string message)
        {
            if (statusLine != null) statusLine.text = message;
        }

        private void OnDestroy()
        {
            if (service != null) service.Changed -= OnChanged;
            visuals?.Dispose();
            externalVisuals?.Dispose();
            visuals = null;
            externalVisuals = null;
        }

        private static Vector2 MonsterBasePosition(int index) => index switch
        {
            0 => new Vector2(-190f, 60f),
            1 => new Vector2(-70f, -70f),
            2 => new Vector2(70f, 90f),
            3 => new Vector2(190f, -40f),
            _ => new Vector2(30f, -190f)
        };

        private static Image WorldPanel(string name, Transform parent, Color color, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return image;
        }

        private static Image Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            SetRect(image.rectTransform, min, max);
            return image;
        }

        private static Image PixelImage(string name, Transform parent, Sprite sprite, Vector2 min, Vector2 max)
        {
            Image image = Panel(name, parent, Color.white, min, max);
            image.sprite = sprite;
            image.preserveAspect = true;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static Button MakeButton(string name, Transform parent, string value, Color color, Vector2 min, Vector2 max)
        {
            Image image = Panel(name, parent, color, min, max);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, .12f);
            colors.pressedColor = Color.Lerp(color, Color.black, .18f);
            button.colors = colors;
            Text("글자", image.transform, value, 20, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white);
            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static Color ThemeColor(string theme) => theme switch
        {
            "MEADOW" => new Color32(77, 119, 68, 255),
            "FOREST" => new Color32(46, 89, 59, 255),
            "MINE" => new Color32(98, 82, 65, 255),
            "SWAMP" => new Color32(75, 91, 56, 255),
            "FROST_RUIN" => new Color32(88, 117, 126, 255),
            _ => new Color32(67, 91, 68, 255)
        };

        private static Color RiskColor(int tier) => tier switch
        {
            1 => new Color32(44, 107, 73, 255),
            2 => new Color32(119, 105, 48, 255),
            3 => new Color32(142, 83, 45, 255),
            4 => new Color32(142, 58, 52, 255),
            _ => new Color32(113, 60, 133, 255)
        };

        private static Color FeedbackColor(WorldHuntFeedbackType type) => type switch
        {
            WorldHuntFeedbackType.LootCollected => new Color32(117, 218, 184, 255),
            WorldHuntFeedbackType.BountyEarned or WorldHuntFeedbackType.Settled => new Color32(244, 199, 91, 255),
            WorldHuntFeedbackType.SkillTrained or WorldHuntFeedbackType.EquipmentEnhanced => new Color32(190, 151, 238, 255),
            WorldHuntFeedbackType.MonsterDefeated => new Color32(255, 137, 90, 255),
            WorldHuntFeedbackType.MonsterRespawned => new Color32(127, 221, 185, 255),
            _ => new Color32(207, 221, 212, 255)
        };

        private static string Job(string id) => id switch
        {
            "JOB_WARRIOR" => "전사",
            "JOB_GUARDIAN" => "수호자",
            "JOB_ARCHER" => "궁수",
            "JOB_MAGE" => "마법사",
            "JOB_CLERIC" => "성직자",
            _ => "용병"
        };

        private static string State(string state) => state switch
        {
            "IDLE_TOWN" => "주점에서 대기",
            "TRAVEL_TO_REGION" => "사냥터로 이동",
            "FIND_TARGET" => "몬스터 탐색",
            "COMBAT" => "전투 중",
            "LOOT" => "전리품 수집",
            "CONTINUE_DECISION" => "사냥 상태 점검",
            "RETURN_TOWN" => "왕국으로 귀환",
            "SELL_LOOT" => "상점에서 판매",
            "HEAL" => "치료소에서 회복",
            "BUY_CONSUMABLES" => "물약 구매",
            "EVALUATE_EQUIPMENT" => "장비 비교",
            "BUY_EQUIPMENT" => "장비 구매",
            "TRAIN_SKILLS" => "길드에서 훈련",
            "ENHANCE_EQUIPMENT" => "대장간에서 강화",
            _ => "왕국에서 정비"
        };

        private sealed class MemberButton
        {
            public MemberButton(Button button, TMP_Text label, Image icon) { Button = button; Label = label; Icon = icon; }
            public Button Button { get; }
            public TMP_Text Label { get; }
            public Image Icon { get; }
            public ContinuousHuntMemberDto Member { get; set; }
        }

        private sealed class RegionView
        {
            public RegionView(RectTransform root, Image background, Outline outline, TMP_Text title, TMP_Text meta, Image riskBackground,
                TMP_Text risk, TMP_Text drop, GameObject locked, List<Image> decorations, List<MonsterView> monsters)
            {
                Root = root; Background = background; Outline = outline; Title = title; Meta = meta; RiskBackground = riskBackground;
                Risk = risk; Drop = drop; Locked = locked; Decorations = decorations; Monsters = monsters;
            }
            public RectTransform Root { get; }
            public Image Background { get; }
            public Outline Outline { get; }
            public TMP_Text Title { get; }
            public TMP_Text Meta { get; }
            public Image RiskBackground { get; }
            public TMP_Text Risk { get; }
            public TMP_Text Drop { get; }
            public GameObject Locked { get; }
            public List<Image> Decorations { get; }
            public List<MonsterView> Monsters { get; }
        }

        private sealed class MonsterView
        {
            public MonsterView(GameObject root, RectTransform rect, Image body, TMP_Text label, Image hp, Image pulse, TMP_Text feedback,
                Outline eliteOutline, int index, Vector2 basePosition)
            {
                Root = root; Rect = rect; Body = body; Label = label; Hp = hp; Pulse = pulse; Feedback = feedback;
                EliteOutline = eliteOutline; Index = index; BasePosition = basePosition;
            }
            public GameObject Root { get; }
            public RectTransform Rect { get; }
            public Image Body { get; }
            public TMP_Text Label { get; }
            public Image Hp { get; }
            public Image Pulse { get; }
            public TMP_Text Feedback { get; }
            public Outline EliteOutline { get; }
            public int Index { get; }
            public Vector2 BasePosition { get; }
            public WorldHuntMonsterDto Monster { get; set; }
            public Color BaseColor { get; set; }
            public Color FeedbackColor { get; set; }
            public float FeedbackStarted { get; set; }
            public float FeedbackUntil { get; set; }
            public float FlashStarted { get; set; }
            public float FlashUntil { get; set; }
            public float PulseStarted { get; set; }
            public float PulseUntil { get; set; }
        }

        private sealed class ActorView
        {
            public ActorView(GameObject root, RectTransform rect, Image body, TMP_Text label, int index)
            { Root = root; Rect = rect; Body = body; Label = label; Index = index; }
            public GameObject Root { get; }
            public RectTransform Rect { get; }
            public Image Body { get; }
            public TMP_Text Label { get; }
            public int Index { get; }
            public ContinuousHuntMemberDto Member { get; set; }
            public Vector2 Target { get; set; }
            public bool Initialized { get; set; }
            public float LungeStarted { get; set; }
            public float LungeUntil { get; set; }
        }

        private sealed class FeedbackRow
        {
            public FeedbackRow(TMP_Text text) { Text = text; Color = new Color32(207, 221, 212, 255); }
            public TMP_Text Text { get; }
            public Color Color { get; set; }
            public float ExpiresAt { get; set; }
        }
    }
}
