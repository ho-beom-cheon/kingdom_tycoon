using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Application.Mercenaries;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Presentation.Navigation;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Combat
{
    /// <summary>Portrait-first continuous kingdom and hunting world. Mutations are delegated to application services.</summary>
    public sealed class ContinuousHuntScreenPresenter : MonoBehaviour
    {
        private const float VisualPublishInterval = .2f;
        private const float InputIdleBeforeSave = 1.5f;
        private const float CatchUpFrameInterval = .05f;
        private const float SimulationPollInterval = .25f;
        private const float InputPriorityGraceSeconds = .15f;
        private const float ActiveWorldAnimationInterval = 1f / 24f;
        private const float PanelWorldAnimationInterval = 1f / 12f;
        private const float IdleRegionAnimationInterval = 1f / 8f;
        private static readonly ProfilerMarker SimulationMarker = new("KingdomTycoon.World.Simulation");
        private static readonly ProfilerMarker RefreshMarker = new("KingdomTycoon.World.Refresh");
        private static readonly ProfilerMarker AnimationMarker = new("KingdomTycoon.World.Animation");
        private static readonly ProfilerMarker PersistenceMarker = new("KingdomTycoon.World.Persistence");
        private static readonly ProfilerMarker InteractionMarker = new("KingdomTycoon.World.Interaction");
        private readonly List<MemberButton> memberButtons = new();
        private readonly List<ActorView> actorViews = new();
        private readonly Dictionary<string, RegionView> regionViews = new(StringComparer.Ordinal);
        private readonly WorldHuntFeedbackTracker feedbackTracker = new();
        private readonly List<FeedbackRow> feedbackRows = new();
        private ContinuousHuntGameService service;
        private MercenaryRosterService mercenaryRoster;
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
        private RectTransform actorLayer;
        private WorldMapDragSurface dragSurface;
        private GameObject assignmentSheet;
        private GameObject menuPanel;
        private GameObject feedbackRail;
        private GameObject facilityPanel;
        private GameObject characterDetailModal;
        private RectTransform characterDetailCard;
        private TMP_Text facilityTitle;
        private TMP_Text facilityDescription;
        private TMP_Text facilityStatus;
        private Image facilityIcon;
        private Button facilityPrimaryButton;
        private Button facilitySecondaryButton;
        private TMP_Text facilityPrimaryLabel;
        private TMP_Text facilitySecondaryLabel;
        private WorldFacilityInteractionDefinition selectedFacility;
        private TMP_Text characterTitle;
        private TMP_Text characterSubtitle;
        private TMP_Text characterStars;
        private TMP_Text characterGold;
        private TMP_Text characterBody;
        private TMP_Text characterStats;
        private TMP_Text characterActivity;
        private Image characterPortrait;
        private Image characterHealthFill;
        private Image characterBagFill;
        private Image characterGrowthFill;
        private readonly List<TMP_Text> equipmentSlotTexts = new();
        private string selectedMemberInstanceId;
        private int selectedCharacterTab;
        private Button soundButton;
        private Button motionButton;
        private string selectedRegionId = "REGION_R01";
        private float nextTick;
        private float nextWorldAnimationFrame;
        private float nextVisualPublish;
        private float nextPersistence;
        private float lastInteractionAt;
        private float inputPriorityUntil;

        public RectTransform WorldViewport => worldViewport;
        public RectTransform WorldContent => worldContent;
        public WorldMapDragSurface DragSurface => dragSurface;
        public string SelectedRegionId => selectedRegionId;
        public bool AssignmentSheetOpen => assignmentSheet != null && assignmentSheet.activeSelf;
        public bool MobileMenuOpen => menuPanel != null && menuPanel.activeSelf;
        public bool FacilityPanelOpen => facilityPanel != null && facilityPanel.activeSelf;
        public bool CharacterDetailOpen => characterDetailModal != null && characterDetailModal.activeSelf;
        public string SelectedFacilityId => selectedFacility?.Id;
        public string SelectedMemberInstanceId => selectedMemberInstanceId;
        public bool SoundEnabled => feedbackAudio != null && feedbackAudio.SoundEnabled;
        public bool ReducedMotion => WorldHuntFeedbackPreferences.ReducedMotion;
        public IReadOnlyList<TMP_Text> FeedbackTexts => feedbackRows.Select(value => value.Text).ToArray();
        public int PixelArtSpriteCount => visuals?.SpriteCount ?? 0;
        public int ExternalSpriteCount => externalVisuals?.LoadedSpriteCount ?? 0;
        public int IsolatedCanvasCount => GetComponentsInChildren<Canvas>(true).Length;

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
            lastInteractionAt = Time.unscaledTime;
            nextVisualPublish = Time.unscaledTime;
            nextPersistence = Time.unscaledTime + ContinuousHuntGameService.PersistenceIntervalSeconds;
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
            mercenaryRoster ??= AppRoot.Instance.Services.Get<MercenaryRosterService>();
            Refresh();
            assignmentSheet.SetActive(false);
            menuPanel.SetActive(false);
            facilityPanel.SetActive(false);
            characterDetailModal.SetActive(false);
            selectedMemberInstanceId = null;
            Canvas.ForceUpdateCanvases();
            dragSurface.ResetView();
        }

        public void Close()
        {
            assignmentSheet?.SetActive(false);
            menuPanel?.SetActive(false);
            facilityPanel?.SetActive(false);
            characterDetailModal?.SetActive(false);
            selectedMemberInstanceId = null;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (HasDirectPointerInput()) MarkInteraction();
            if (CharacterDetailOpen) ApplyResponsiveCharacterLayout();
            bool cameraMoving = dragSurface != null && dragSurface.IsCameraMoving;
            if (cameraMoving) MarkInteraction();
            bool inputHasPriority = cameraMoving || Time.unscaledTime < inputPriorityUntil;
            bool panelOpen = AssignmentSheetOpen || MobileMenuOpen || FacilityPanelOpen || CharacterDetailOpen;
            if (!inputHasPriority && Time.unscaledTime >= nextWorldAnimationFrame)
            {
                nextWorldAnimationFrame = Time.unscaledTime + (panelOpen ? PanelWorldAnimationInterval : ActiveWorldAnimationInterval);
                using (AnimationMarker.Auto()) AnimateWorld();
            }
            if (service == null || inputHasPriority) return;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (Time.unscaledTime >= nextTick)
            {
                try
                {
                    int steps;
                    using (SimulationMarker.Auto()) steps = service.AdvanceBudgetedTo(now, ContinuousHuntGameService.RuntimeStepBudget);
                    bool catchingUp = steps >= ContinuousHuntGameService.RuntimeStepBudget && service.HasDueWork(now);
                    nextTick = Time.unscaledTime + (catchingUp ? CatchUpFrameInterval : SimulationPollInterval);
                }
                catch (Exception exception)
                {
                    nextTick = Time.unscaledTime + 1f;
                    Debug.LogWarning(exception);
                    ShowStatus("사냥 기록을 갱신하지 못했습니다. 저장 상태를 유지하고 다시 시도합니다.");
                }
            }
            if (Time.unscaledTime >= nextVisualPublish)
            {
                service.PublishPending();
                nextVisualPublish = Time.unscaledTime + VisualPublishInterval;
            }
            if (service.HasPendingPersistence && Time.unscaledTime >= nextPersistence && Time.unscaledTime - lastInteractionAt >= InputIdleBeforeSave)
            {
                FlushPendingSafely(now);
                nextPersistence = Time.unscaledTime + ContinuousHuntGameService.PersistenceIntervalSeconds;
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
            Canvas worldCanvas = contentObject.AddComponent<Canvas>();
            worldCanvas.overrideSorting = false;
            contentObject.AddComponent<GraphicRaycaster>();
            dragSurface.Configure(worldViewport, worldContent);
            BuildWorldBase();

            Image hud = Panel("왕국상태", safe.transform, new Color32(9, 22, 24, 225), new Vector2(.02f, .905f), new Vector2(.61f, .985f));
            Text("화면제목", hud.transform, "통합 사냥 월드 · 상시 자동 사냥", 25, TextAlignmentOptions.Left, new Vector2(.045f, .47f), new Vector2(.95f, .94f), new Color32(244, 211, 127, 255));
            summary = Text("전체요약", hud.transform, "배치 0명 · 누적 수익 0골드", 18, TextAlignmentOptions.Left, new Vector2(.045f, .07f), new Vector2(.95f, .48f), new Color32(199, 220, 209, 255));

            Button home = MakeButton("왕국복귀", safe.transform, "왕국", new Color32(61, 84, 70, 245), new Vector2(.64f, .905f), new Vector2(.745f, .985f));
            Button news = MakeButton("소식버튼", safe.transform, "소식", new Color32(58, 79, 88, 245), new Vector2(.755f, .905f), new Vector2(.86f, .985f));
            Button menu = MakeButton("통합메뉴버튼", safe.transform, "메뉴", new Color32(83, 61, 92, 245), new Vector2(.87f, .905f), new Vector2(.98f, .985f));
            home.GetComponentInChildren<TMP_Text>().fontSize = 19;
            home.onClick.AddListener(() => { MarkInteraction(); dragSurface.ResetView(); ShowStatus("중앙 왕국으로 돌아왔습니다."); });
            news.onClick.AddListener(() => { MarkInteraction(); feedbackRail.SetActive(!feedbackRail.activeSelf); });
            menu.onClick.AddListener(() =>
            {
                MarkInteraction();
                bool opening = !menuPanel.activeSelf;
                CloseWorldPanels();
                menuPanel.SetActive(opening);
            });

            BuildFeedbackRail(safe.transform);
            BuildMobileMenu(safe.transform);
            BuildAssignmentSheet(safe.transform);
            BuildFacilityPanel(safe.transform);
            BuildCharacterDetailModal(safe.transform);
        }

        private void BuildWorldBase()
        {
            Panel("월드지면", worldContent, new Color32(43, 74, 52, 255), Vector2.zero, Vector2.one).raycastTarget = false;
            WorldPanel("서쪽숲그늘", worldContent, new Color32(34, 66, 48, 210), new Vector2(-1040f, 260f), new Vector2(920f, 2500f)).raycastTarget = false;
            WorldPanel("동쪽구릉빛", worldContent, new Color32(72, 91, 53, 150), new Vector2(1030f, 180f), new Vector2(850f, 2440f)).raycastTarget = false;
            WorldPanel("남쪽바위그늘", worldContent, new Color32(54, 69, 57, 170), new Vector2(80f, -1320f), new Vector2(2300f, 430f)).raycastTarget = false;

            foreach (string regionId in new[] { "REGION_R01", "REGION_R02", "REGION_R03", "REGION_R04", "REGION_R05" })
                BuildRoadConnection(regionId);

            Image kingdom = WorldPanel("왕국거점", worldContent, new Color32(34, 66, 49, 30), MobileLivingWorldLayout.KingdomCenter, new Vector2(1180f, 900f));
            kingdom.raycastTarget = false;
            Image plaza = WorldPanel("왕국중앙광장", kingdom.transform, new Color32(155, 127, 83, 255), Vector2.zero, new Vector2(730f, 390f));
            plaza.raycastTarget = false;
            plaza.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            Image plazaInner = WorldPanel("왕국광장안길", kingdom.transform, new Color32(187, 158, 105, 255), new Vector2(0f, -4f), new Vector2(590f, 255f));
            plazaInner.raycastTarget = false;
            plazaInner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -7f);
            BuildKingdomWall(kingdom.transform, "북서성벽", new Vector2(-300f, 318f), new Vector2(610f, 34f), -8f);
            BuildKingdomWall(kingdom.transform, "북동성벽", new Vector2(300f, 318f), new Vector2(610f, 34f), 8f);
            BuildKingdomWall(kingdom.transform, "남서성벽", new Vector2(-315f, -315f), new Vector2(630f, 34f), 8f);
            BuildKingdomWall(kingdom.transform, "남동성벽", new Vector2(315f, -315f), new Vector2(630f, 34f), -8f);

            Text("왕국이름", kingdom.transform, "새벽빛 왕국", 32, TextAlignmentOptions.Center, new Vector2(.31f, .88f), new Vector2(.69f, .98f), new Color32(255, 224, 139, 255));
            Text("왕국설명", kingdom.transform, "용병이 생활하고 성장하는 중앙 거점", 17, TextAlignmentOptions.Center, new Vector2(.27f, .82f), new Vector2(.73f, .89f), new Color32(226, 235, 207, 255));
            Image landmark = PixelImage("왕국픽셀랜드마크", kingdom.transform, visuals.KingdomSprite, new Vector2(.42f, .34f), new Vector2(.58f, .67f));
            landmark.color = Color.white;
            landmark.raycastTarget = false;
            BuildFacility(kingdom.transform, "FAC_TAVERN", "주점", MobileLivingWorldLayout.FacilityPosition("FAC_TAVERN"), false);
            BuildFacility(kingdom.transform, "FAC_STORE", "상점", MobileLivingWorldLayout.FacilityPosition("FAC_STORE"), false);
            BuildFacility(kingdom.transform, "FAC_BLACKSMITH", "대장간", MobileLivingWorldLayout.FacilityPosition("FAC_BLACKSMITH"), false);
            BuildFacility(kingdom.transform, "FAC_INFIRMARY", "치료소", MobileLivingWorldLayout.FacilityPosition("FAC_INFIRMARY"), false);
            BuildFacility(kingdom.transform, "FAC_GUILD", "모험가 길드", MobileLivingWorldLayout.FacilityPosition("FAC_GUILD"), true);
            actorLayer = CreateCanvasLayer("용병동적레이어", worldContent);
            for (int index = 0; index < 8; index++) actorViews.Add(CreateActorView(actorLayer, index));
        }

        private void BuildRoadConnection(string regionId)
        {
            Vector2 gate = MobileLivingWorldLayout.KingdomGateForRegion(regionId);
            Vector2 region = MobileLivingWorldLayout.RegionPosition(regionId);
            BuildRoadSegment("길바탕_" + regionId, gate, region, 120f, new Color32(70, 64, 51, 255));
            BuildRoadSegment("흙길_" + regionId, gate, region, 82f, new Color32(139, 108, 70, 255));
            BuildRoadSegment("길빛_" + regionId, gate, region, 18f, new Color32(184, 150, 95, 155));
        }

        private void BuildRoadSegment(string name, Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start;
            Image road = WorldPanel(name, worldContent, color, Vector2.Lerp(start, end, .5f), new Vector2(delta.magnitude, width));
            road.raycastTarget = false;
            road.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static void BuildKingdomWall(Transform parent, string name, Vector2 position, Vector2 size, float rotation)
        {
            Image shadow = WorldPanel(name + "그림자", parent, new Color32(35, 35, 37, 210), position + new Vector2(0f, -14f), size + new Vector2(4f, 12f));
            shadow.raycastTarget = false;
            shadow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image wall = WorldPanel(name, parent, new Color32(126, 119, 101, 255), position, size);
            wall.raycastTarget = false;
            wall.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void BuildFacility(Transform kingdom, string id, string label, Vector2 worldPosition, bool large)
        {
            Vector2 localPosition = worldPosition - MobileLivingWorldLayout.KingdomCenter;
            var root = new GameObject("시설_" + id, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            root.transform.SetParent(kingdom, false);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = large ? new Vector2(236f, 218f) : new Vector2(210f, 192f);
            rect.anchoredPosition = localPosition;
            Image hit = root.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, .01f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = hit;
            Image shadow = Panel("시설그림자", rect, new Color(0f, 0f, 0f, .32f), new Vector2(.12f, .06f), new Vector2(.88f, .25f));
            shadow.raycastTarget = false;
            Image building = PixelImage("시설픽셀아트", rect, visuals.FacilitySprite(id), new Vector2(.07f, .16f), new Vector2(.93f, .98f));
            building.color = Color.white;
            building.raycastTarget = false;
            var outline = building.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(255, 222, 131, 115);
            outline.effectDistance = new Vector2(2f, -2f);
            Image plaque = Panel("시설명패", rect, new Color32(18, 26, 25, 228), new Vector2(.08f, 0f), new Vector2(.92f, .22f));
            plaque.raycastTarget = false;
            Text("시설명", plaque.transform, label + "  ›", large ? 18 : 17, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Color32(255, 231, 166, 255));
            button.onClick.AddListener(() => ShowFacility(id));
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
            close.onClick.AddListener(() => { MarkInteraction(); menuPanel.SetActive(false); });
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
            button.onClick.AddListener(() => { MarkInteraction(); menuPanel.SetActive(false); action?.Invoke(); });
        }

        private void BuildAssignmentSheet(Transform parent)
        {
            Image drawer = Panel("사냥터배치시트", parent, new Color32(13, 29, 32, 252), new Vector2(.02f, .02f), new Vector2(.98f, .45f));
            assignmentSheet = drawer.gameObject;
            selection = Text("선택지역", drawer.transform, "사냥터를 선택해 주세요.", 25, TextAlignmentOptions.Left, new Vector2(.04f, .86f), new Vector2(.78f, .97f), new Color32(241, 208, 128, 255));
            Button close = MakeButton("배치시트닫기", drawer.transform, "닫기", new Color32(88, 68, 55, 255), new Vector2(.80f, .86f), new Vector2(.96f, .97f));
            close.onClick.AddListener(() => { MarkInteraction(); assignmentSheet.SetActive(false); });
            statusLine = Text("상태안내", drawer.transform, "상시 자동 사냥 · 판매 · 치료 · 스킬 훈련 · 장비 강화가 계속 이어집니다.", 17, TextAlignmentOptions.Left, new Vector2(.04f, .74f), new Vector2(.96f, .86f), new Color32(170, 200, 190, 255));
            for (int index = 0; index < 8; index++) memberButtons.Add(CreateMemberButton(drawer.transform, index));
            assignmentSheet.SetActive(false);
        }

        private void BuildFacilityPanel(Transform parent)
        {
            Image panel = Panel("시설기능패널", parent, new Color32(13, 27, 29, 252), new Vector2(.02f, .025f), new Vector2(.98f, .355f));
            facilityPanel = panel.gameObject;
            Image iconFrame = Panel("시설초상틀", panel.transform, new Color32(37, 54, 50, 255), new Vector2(.04f, .33f), new Vector2(.28f, .91f));
            facilityIcon = PixelImage("선택시설그림", iconFrame.transform, visuals.FacilitySprite("FAC_TAVERN"), new Vector2(.06f, .07f), new Vector2(.94f, .93f));
            facilityIcon.raycastTarget = false;
            facilityTitle = Text("시설기능제목", panel.transform, "왕국 시설", 27, TextAlignmentOptions.Left, new Vector2(.32f, .73f), new Vector2(.78f, .94f), new Color32(248, 215, 130, 255));
            facilityDescription = Text("시설기능설명", panel.transform, string.Empty, 17, TextAlignmentOptions.Left, new Vector2(.32f, .48f), new Vector2(.95f, .74f), new Color32(213, 228, 215, 255));
            facilityStatus = Text("시설현재상태", panel.transform, string.Empty, 16, TextAlignmentOptions.Left, new Vector2(.32f, .31f), new Vector2(.95f, .49f), new Color32(145, 202, 181, 255));
            Button close = MakeButton("시설패널닫기", panel.transform, "닫기", new Color32(86, 66, 55, 255), new Vector2(.80f, .78f), new Vector2(.96f, .94f));
            close.onClick.AddListener(() => { MarkInteraction(); facilityPanel.SetActive(false); });
            facilityPrimaryButton = MakeButton("시설주요기능", panel.transform, "주요 기능", new Color32(173, 104, 48, 255), new Vector2(.04f, .055f), new Vector2(.49f, .27f));
            facilitySecondaryButton = MakeButton("시설보조기능", panel.transform, "보조 기능", new Color32(48, 112, 94, 255), new Vector2(.51f, .055f), new Vector2(.96f, .27f));
            facilityPrimaryLabel = facilityPrimaryButton.GetComponentInChildren<TMP_Text>();
            facilitySecondaryLabel = facilitySecondaryButton.GetComponentInChildren<TMP_Text>();
            facilityPrimaryButton.onClick.AddListener(() => RouteFacilityAction(selectedFacility?.PrimaryAction));
            facilitySecondaryButton.onClick.AddListener(() => RouteFacilityAction(selectedFacility?.SecondaryAction));
            facilityPanel.SetActive(false);
        }

        private void BuildCharacterDetailModal(Transform parent)
        {
            Image scrim = Panel("캐릭터상세모달", parent, new Color32(4, 7, 9, 224), Vector2.zero, Vector2.one);
            characterDetailModal = scrim.gameObject;
            Button scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.targetGraphic = scrim;
            scrimButton.onClick.AddListener(CloseCharacterDetail);

            Image card = Panel("캐릭터상세카드", scrim.transform, new Color32(26, 23, 20, 255), new Vector2(.055f, .075f), new Vector2(.945f, .91f));
            characterDetailCard = card.rectTransform;
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(207, 181, 111, 255);
            outline.effectDistance = new Vector2(3f, -3f);
            Image header = Panel("캐릭터상세머리", card.transform, new Color32(43, 35, 28, 255), new Vector2(.018f, .875f), new Vector2(.982f, .985f));
            header.raycastTarget = false;
            characterTitle = Text("캐릭터이름", header.transform, "용병", 29, TextAlignmentOptions.Left, new Vector2(.04f, .42f), new Vector2(.73f, .94f), new Color32(255, 239, 204, 255));
            characterSubtitle = Text("캐릭터등급직업", header.transform, string.Empty, 17, TextAlignmentOptions.Left, new Vector2(.04f, .05f), new Vector2(.73f, .44f), new Color32(189, 216, 205, 255));
            Button close = MakeButton("캐릭터상세닫기", card.transform, "닫기", new Color32(91, 55, 46, 255), new Vector2(.80f, .895f), new Vector2(.96f, .97f));
            close.onClick.AddListener(CloseCharacterDetail);
            characterStars = Text("캐릭터등급별", card.transform, "★", 25, TextAlignmentOptions.Left, new Vector2(.055f, .81f), new Vector2(.42f, .87f), new Color32(221, 128, 215, 255));
            Image gold = Panel("개인골드배경", card.transform, new Color32(57, 48, 36, 255), new Vector2(.66f, .81f), new Vector2(.945f, .87f));
            gold.raycastTarget = false;
            characterGold = Text("개인골드", gold.transform, "개인 골드 0", 16, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Color32(249, 205, 94, 255));

            Image portraitFrame = Panel("캐릭터초상틀", card.transform, new Color32(43, 49, 44, 255), new Vector2(.32f, .49f), new Vector2(.68f, .79f));
            portraitFrame.raycastTarget = false;
            characterPortrait = PixelImage("캐릭터초상", portraitFrame.transform, visuals.JobSprite("JOB_WARRIOR"), new Vector2(.12f, .04f), new Vector2(.88f, .96f));
            characterPortrait.raycastTarget = false;

            AddEquipmentSlot(card.transform, "WEAPON", "무기", new Vector2(.045f, .645f), new Vector2(.275f, .77f));
            AddEquipmentSlot(card.transform, "ARMOR", "갑옷", new Vector2(.045f, .50f), new Vector2(.275f, .625f));
            AddEquipmentSlot(card.transform, "HELMET", "투구", new Vector2(.725f, .645f), new Vector2(.955f, .77f));
            AddEquipmentSlot(card.transform, "ACCESSORY", "장신구", new Vector2(.725f, .50f), new Vector2(.955f, .625f));

            AddGauge(card.transform, "현재체력", "체력", new Vector2(.055f, .435f), new Color32(209, 91, 70, 255), out characterHealthFill);
            AddGauge(card.transform, "가방공간", "가방", new Vector2(.365f, .435f), new Color32(75, 170, 147, 255), out characterBagFill);
            AddGauge(card.transform, "성장진행", "성장", new Vector2(.675f, .435f), new Color32(188, 145, 75, 255), out characterGrowthFill);

            Button infoTab = MakeButton("캐릭터정보탭", card.transform, "정보", new Color32(83, 67, 48, 255), new Vector2(.045f, .35f), new Vector2(.34f, .415f));
            Button growthTab = MakeButton("캐릭터성장탭", card.transform, "특성·성장", new Color32(58, 62, 58, 255), new Vector2(.352f, .35f), new Vector2(.648f, .415f));
            Button recordTab = MakeButton("캐릭터기록탭", card.transform, "장비·기록", new Color32(58, 62, 58, 255), new Vector2(.66f, .35f), new Vector2(.955f, .415f));
            infoTab.onClick.AddListener(() => SelectCharacterTab(0));
            growthTab.onClick.AddListener(() => SelectCharacterTab(1));
            recordTab.onClick.AddListener(() => SelectCharacterTab(2));

            Image content = Panel("캐릭터정보내용", card.transform, new Color32(18, 17, 15, 255), new Vector2(.045f, .115f), new Vector2(.955f, .34f));
            content.raycastTarget = false;
            characterBody = Text("캐릭터정보본문", content.transform, string.Empty, 17, TextAlignmentOptions.TopLeft, new Vector2(.035f, .08f), new Vector2(.49f, .94f), new Color32(228, 219, 193, 255));
            characterStats = Text("캐릭터사냥수치", content.transform, string.Empty, 17, TextAlignmentOptions.TopLeft, new Vector2(.52f, .08f), new Vector2(.965f, .94f), new Color32(177, 216, 201, 255));
            characterActivity = Text("캐릭터현재활동", card.transform, string.Empty, 15, TextAlignmentOptions.Left, new Vector2(.055f, .055f), new Vector2(.67f, .105f), new Color32(160, 198, 185, 255));
            Button equipment = MakeButton("캐릭터장비변경", card.transform, "가방에서 장비 변경", new Color32(163, 67, 43, 255), new Vector2(.69f, .035f), new Vector2(.955f, .105f));
            equipment.onClick.AddListener(OpenInventoryFromCharacter);
            characterDetailModal.SetActive(false);
        }

        private void AddEquipmentSlot(Transform parent, string slotId, string label, Vector2 min, Vector2 max)
        {
            Button button = MakeButton("장비슬롯_" + slotId, parent, label + "\n비어 있음", new Color32(54, 42, 58, 255), min, max);
            TMP_Text text = button.GetComponentInChildren<TMP_Text>();
            text.fontSize = 15;
            equipmentSlotTexts.Add(text);
            button.onClick.AddListener(OpenInventoryFromCharacter);
        }

        private static void AddGauge(Transform parent, string name, string label, Vector2 min, Color fillColor, out Image fill)
        {
            Text(name + "이름", parent, label, 14, TextAlignmentOptions.Left, min, min + new Vector2(.085f, .045f), new Color32(215, 205, 180, 255));
            Image background = Panel(name + "배경", parent, new Color32(37, 38, 35, 255), min + new Vector2(.087f, .006f), min + new Vector2(.285f, .041f));
            background.raycastTarget = false;
            fill = Panel(name + "채움", background.transform, fillColor, Vector2.zero, Vector2.one);
            fill.raycastTarget = false;
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
        }

        private void ShowFacility(string facilityId)
        {
            using (InteractionMarker.Auto())
            {
                if (dragSurface.ConsumeTapSuppression()) return;
                MarkInteraction();
                selectedFacility = WorldFacilityInteractionCatalog.Get(facilityId);
                CloseWorldPanels();
                SetTextIfChanged(facilityTitle, selectedFacility.Title);
                SetTextIfChanged(facilityDescription, selectedFacility.Description);
                SetTextIfChanged(facilityPrimaryLabel, selectedFacility.PrimaryLabel);
                SetTextIfChanged(facilitySecondaryLabel, selectedFacility.SecondaryLabel);
                SetSpriteIfChanged(facilityIcon, visuals.FacilitySprite(facilityId));
                int visitors = overview?.Members.Count(value => MobileLivingWorldLayout.FacilityForState(value.State) == facilityId) ?? 0;
                SetTextIfChanged(facilityStatus, visitors > 0 ? $"현재 이용 중인 용병 {visitors}명" : "현재 대기 중 · 시설을 눌러 기능을 이용하세요.");
                facilityPanel.SetActive(true);
            }
        }

        private void RouteFacilityAction(WorldFacilityAction? action)
        {
            if (!action.HasValue) return;
            MarkInteraction();
            facilityPanel.SetActive(false);
            switch (action.Value)
            {
                case WorldFacilityAction.Recruitment: Navigation()?.OpenRecruitment(); break;
                case WorldFacilityAction.Mercenaries: Navigation()?.OpenMercenaries(); break;
                case WorldFacilityAction.Store: Navigation()?.OpenStore(); break;
                case WorldFacilityAction.Inventory: Navigation()?.OpenInventory(); break;
                case WorldFacilityAction.EquipmentGrowth: Navigation()?.OpenEquipmentGrowth(); break;
                case WorldFacilityAction.Production: Navigation()?.OpenProduction(); break;
                case WorldFacilityAction.Progression: Navigation()?.OpenProgression(); break;
            }
        }

        private void ShowCharacterDetail(ActorView actor)
        {
            using (InteractionMarker.Auto())
            {
                if (actor?.Member == null || dragSurface.ConsumeTapSuppression()) return;
                MarkInteraction();
                try
                {
                    mercenaryRoster ??= AppRoot.Instance?.Services.Get<MercenaryRosterService>();
                    if (mercenaryRoster == null) return;
                    CloseWorldPanels();
                    selectedMemberInstanceId = actor.Member.InstanceId;
                    selectedCharacterTab = 0;
                    RenderCharacterDetail();
                    ApplyResponsiveCharacterLayout();
                    characterDetailModal.SetActive(true);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(exception);
                    ShowStatus("용병 상세 정보를 불러오지 못했습니다. 잠시 후 다시 시도해 주세요.");
                }
            }
        }

        private void SelectCharacterTab(int tab)
        {
            MarkInteraction();
            selectedCharacterTab = Mathf.Clamp(tab, 0, 2);
            RenderCharacterDetail();
        }

        private void RenderCharacterDetail()
        {
            if (mercenaryRoster == null || string.IsNullOrEmpty(selectedMemberInstanceId)) return;
            MercenaryDetailDto detail = mercenaryRoster.GetDetail(selectedMemberInstanceId);
            ContinuousHuntMemberDto live = overview?.Members.FirstOrDefault(value => value.InstanceId == selectedMemberInstanceId);
            SetTextIfChanged(characterTitle, detail.DisplayName);
            SetTextIfChanged(characterSubtitle, $"{detail.GradeName} · {detail.RankName} · {detail.JobName} · 레벨 {detail.Level}");
            SetTextIfChanged(characterStars, new string('★', GradeStars(detail.GradeId)));
            SetTextIfChanged(characterGold, $"개인 골드  {detail.PersonalGold:N0}");
            SetSpriteIfChanged(characterPortrait, visuals.JobSprite(detail.JobId));
            SetAnchorMaxIfChanged(characterHealthFill.rectTransform, new Vector2((live?.CurrentHpBps ?? 0) / 10000f, 1f));
            float bagRatio = live == null || live.BagCapacity <= 0 ? 0f : live.BagFill / (float)live.BagCapacity;
            SetAnchorMaxIfChanged(characterBagFill.rectTransform, new Vector2(Mathf.Clamp01(bagRatio), 1f));
            float growthRatio = detail.RankMaxLevel <= 1 ? 1f : (detail.Level - 1f) / (detail.RankMaxLevel - 1f);
            SetAnchorMaxIfChanged(characterGrowthFill.rectTransform, new Vector2(Mathf.Clamp01(growthRatio), 1f));
            string[] slotIds = { "WEAPON", "ARMOR", "HELMET", "ACCESSORY" };
            string[] slotNames = { "무기", "갑옷", "투구", "장신구" };
            for (int index = 0; index < equipmentSlotTexts.Count; index++)
            {
                bool equipped = detail.EquipmentSlots.TryGetValue(slotIds[index], out string value) && !string.IsNullOrEmpty(value);
                SetTextIfChanged(equipmentSlotTexts[index], $"{slotNames[index]}\n{(equipped ? "장착됨" : "비어 있음")}");
            }

            SetTextIfChanged(characterBody, selectedCharacterTab switch
            {
                1 => $"성격  {detail.PersonalityName}\n특성  {TraitSummary(detail)}\n스킬 합계  {live?.TotalSkillLevels ?? 0}\n최고 강화  +{live?.BestEnhancementLevel ?? 0}",
                2 => $"장착 장비  {detail.EquipmentSlots.Count(value => !string.IsNullOrEmpty(value.Value))}/4\n보유 물약  {detail.PotionStacks.Values.Sum():N0}\n사냥 완료  {(live?.CyclesCompleted ?? 0):N0}회\n누적 수익  {(live?.EarnedGold ?? 0):N0}골드",
                _ => $"등급  {detail.GradeName}\n랭크  {detail.RankName}\n성격  {detail.PersonalityName}\n공헌도  {detail.Contribution:N0}\n개인 골드  {detail.PersonalGold:N0}"
            });
            SetTextIfChanged(characterStats, live == null
                ? "사냥 상태를 확인할 수 없습니다."
                : $"체력  {live.CurrentHpBps / 100f:0}%\n가방  {live.BagFill}/{live.BagCapacity}\n최근 피해  {live.LastCombatDamage:N0}\n미정산 보상  {live.PendingBountyGold:N0}골드");
            SetTextIfChanged(characterActivity, live == null ? "현재 활동 · 왕국에서 대기" : $"현재 활동 · {State(live.State)}");
        }

        private static string TraitSummary(MercenaryDetailDto detail) =>
            detail.TraitNames.Count == 0 ? "없음" : string.Join(" · ", detail.TraitNames);

        private static int GradeStars(string gradeId) => gradeId switch
        {
            "GRADE_SS" => 5,
            "GRADE_S" => 4,
            "GRADE_A" => 3,
            "GRADE_B" => 2,
            _ => 1
        };

        private void OpenInventoryFromCharacter()
        {
            MarkInteraction();
            CloseCharacterDetail();
            Navigation()?.OpenInventory();
        }

        private void CloseCharacterDetail()
        {
            MarkInteraction();
            characterDetailModal?.SetActive(false);
            selectedMemberInstanceId = null;
        }

        private void CloseWorldPanels()
        {
            assignmentSheet?.SetActive(false);
            menuPanel?.SetActive(false);
            facilityPanel?.SetActive(false);
            CloseCharacterDetail();
        }

        private void ApplyResponsiveCharacterLayout()
        {
            if (characterDetailCard == null) return;
            bool landscape = Screen.width > Screen.height;
            Vector2 minimum = landscape ? new Vector2(.16f, .04f) : new Vector2(.055f, .075f);
            Vector2 maximum = landscape ? new Vector2(.84f, .96f) : new Vector2(.945f, .91f);
            if (characterDetailCard.anchorMin == minimum && characterDetailCard.anchorMax == maximum &&
                characterDetailCard.offsetMin == Vector2.zero && characterDetailCard.offsetMax == Vector2.zero) return;
            characterDetailCard.anchorMin = minimum;
            characterDetailCard.anchorMax = maximum;
            characterDetailCard.offsetMin = characterDetailCard.offsetMax = Vector2.zero;
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
            bool created = false;
            foreach (WorldHuntRegionDto region in overview.Regions)
            {
                if (!regionViews.TryGetValue(region.Id, out RegionView view))
                {
                    view = CreateRegionView(region);
                    regionViews.Add(region.Id, view);
                    created = true;
                }
                SetAnchoredPositionIfChanged(view.Root, MobileLivingWorldLayout.RegionPosition(region.Id));
            }
            if (created && actorLayer != null) actorLayer.SetAsLastSibling();
        }

        private RegionView CreateRegionView(WorldHuntRegionDto region)
        {
            Sprite groundSprite = externalVisuals.Ground(region.Theme);
            Image rootImage = WorldPanel("월드지역_" + region.Id, worldContent, new Color(1f, 1f, 1f, .01f),
                MobileLivingWorldLayout.RegionPosition(region.Id), new Vector2(680f, 560f));
            Canvas regionCanvas = rootImage.gameObject.AddComponent<Canvas>();
            regionCanvas.overrideSorting = false;
            rootImage.gameObject.AddComponent<GraphicRaycaster>();
            Button button = rootImage.gameObject.AddComponent<Button>();
            button.targetGraphic = rootImage;
            button.onClick.AddListener(() => SelectRegion(region.Id));
            AddGroundPatch(rootImage.transform, "사냥터지면_1", groundSprite, region.Theme, new Vector2(.02f, .08f), new Vector2(.55f, .70f));
            AddGroundPatch(rootImage.transform, "사냥터지면_2", groundSprite, region.Theme, new Vector2(.38f, .10f), new Vector2(.97f, .68f));
            AddGroundPatch(rootImage.transform, "사냥터지면_3", groundSprite, region.Theme, new Vector2(.13f, .39f), new Vector2(.72f, .94f));
            AddGroundPatch(rootImage.transform, "사냥터지면_4", groundSprite, region.Theme, new Vector2(.52f, .35f), new Vector2(.99f, .88f));
            AddGroundPatch(rootImage.transform, "사냥터지면_5", groundSprite, region.Theme, new Vector2(.05f, .49f), new Vector2(.39f, .86f));
            Image sign = Panel("사냥터표지판", rootImage.transform, new Color32(26, 38, 32, 238), new Vector2(.04f, .77f), new Vector2(.96f, .98f));
            sign.raycastTarget = false;
            var outline = sign.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(4f, -4f);
            outline.effectColor = new Color32(232, 197, 115, 0);
            TMP_Text title = Text("지역명", sign.transform, region.DisplayName, 25, TextAlignmentOptions.Left, new Vector2(.035f, .53f), new Vector2(.64f, .96f), new Color32(250, 231, 179, 255));
            TMP_Text meta = Text("지역정보", sign.transform, string.Empty, 15, TextAlignmentOptions.Right, new Vector2(.61f, .47f), new Vector2(.965f, .96f), new Color32(225, 235, 219, 255));
            Image riskBackground = Panel("위험도배지", sign.transform, RiskColor(region.Tier), new Vector2(.035f, .08f), new Vector2(.27f, .47f));
            TMP_Text risk = Text("위험도", riskBackground.transform, region.RiskLabel, 17, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white);
            TMP_Text drop = Text("주요전리품", sign.transform, region.DropPreview, 15, TextAlignmentOptions.Left, new Vector2(.30f, .08f), new Vector2(.965f, .47f), new Color32(227, 214, 174, 255));
            var decorations = new List<Image>();
            for (int index = 0; index < 3; index++)
            {
                Sprite decorationSprite = externalVisuals.Decoration(index);
                Image decoration = PixelImage("환경장식_무료_" + index, rootImage.transform, decorationSprite ?? visuals.DecorationSprite(region.Theme),
                    new Vector2(.07f + index * .31f, .51f - (index % 2) * .09f), new Vector2(.21f + index * .31f, .70f - (index % 2) * .09f));
                decoration.raycastTarget = false;
                decoration.color = decorationSprite == null ? Color.white : new Color(1f, 1f, 1f, .92f);
                decorations.Add(decoration);
            }
            var monsters = new List<MonsterView>();
            for (int index = 0; index < 5; index++) monsters.Add(CreateMonsterView(rootImage.transform, index));
            GameObject locked = Panel("잠금", rootImage.transform, new Color32(12, 18, 21, 235), new Vector2(.24f, .30f), new Vector2(.76f, .58f)).gameObject;
            Text("잠금문구", locked.transform, "아직 개방되지 않은 사냥터", 20, TextAlignmentOptions.Center, new Vector2(.08f, .20f), new Vector2(.92f, .80f), Color.white);
            return new RegionView(region.Id, rootImage.rectTransform, rootImage, outline, title, meta, riskBackground, risk, drop, locked, decorations, monsters);
        }

        private static void AddGroundPatch(Transform parent, string name, Sprite sprite, string theme, Vector2 min, Vector2 max)
        {
            Image patch = Panel(name, parent, sprite == null ? ThemeColor(theme) : Color.Lerp(ThemeColor(theme), Color.white, .16f), min, max);
            patch.raycastTarget = false;
            if (sprite == null) return;
            patch.sprite = sprite;
            patch.type = Image.Type.Tiled;
            patch.pixelsPerUnitMultiplier = .42f;
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
            rect.sizeDelta = new Vector2(190f, 108f);
            Image hit = Panel("용병선택_" + index, root.transform, new Color(1f, 1f, 1f, .01f), Vector2.zero, Vector2.one);
            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            Image shadow = Panel("용병그림자", root.transform, new Color(0f, 0f, 0f, .32f), new Vector2(.035f, .04f), new Vector2(.39f, .19f));
            shadow.raycastTarget = false;
            Image body = PixelImage("용병몸", root.transform, visuals.JobSprite("JOB_WARRIOR"), new Vector2(.015f, .06f), new Vector2(.43f, .98f));
            body.color = Color.white;
            body.raycastTarget = false;
            var selectionOutline = body.gameObject.AddComponent<Outline>();
            selectionOutline.effectColor = new Color32(255, 218, 112, 0);
            selectionOutline.effectDistance = new Vector2(3f, -3f);
            Image nameplate = Panel("용병이름표", root.transform, new Color32(17, 26, 26, 230), new Vector2(.38f, .10f), new Vector2(1f, .86f));
            nameplate.raycastTarget = false;
            TMP_Text label = Text("용병이름", nameplate.transform, string.Empty, 14, TextAlignmentOptions.Left, new Vector2(.07f, .12f), new Vector2(.95f, .90f), Color.white);
            Image badge = Panel("용병상태점", root.transform, new Color32(101, 201, 152, 255), new Vector2(.36f, .76f), new Vector2(.45f, .92f));
            badge.raycastTarget = false;
            var view = new ActorView(root, rect, button, body, label, badge, selectionOutline, index);
            button.onClick.AddListener(() => ShowCharacterDetail(view));
            button.enabled = false;
            hit.gameObject.AddComponent<WorldActorTapTarget>().Configure(dragSurface, rect, () => button.onClick.Invoke());
            root.SetActive(false);
            return view;
        }

        private void SelectRegion(string regionId)
        {
            if (dragSurface.ConsumeTapSuppression()) return;
            MarkInteraction();
            selectedRegionId = regionId;
            CloseWorldPanels();
            assignmentSheet.SetActive(true);
            Refresh(overview);
        }

        private void ToggleAssignment(MemberButton row)
        {
            if (row.Member == null || service == null) return;
            MarkInteraction();
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
            using (RefreshMarker.Auto()) RefreshCore(snapshot);
        }

        private void RefreshCore(ContinuousHuntOverviewDto snapshot)
        {
            if (service == null) return;
            overview = snapshot ?? service.GetOverview();
            feedbackAudio.Configure(overview.Feedback);
            IReadOnlyList<WorldHuntFeedbackEvent> feedback = feedbackTracker.Observe(overview);
            EnsureRegionViews();
            SetTextIfChanged(summary, $"배치 {overview.AssignedCount}명 · 누적 수익 {overview.EarnedGold:N0}골드");
            WorldHuntRegionDto selected = overview.Regions.FirstOrDefault(value => value.Id == selectedRegionId) ?? overview.Regions.First();
            selectedRegionId = selected.Id;
            SetTextIfChanged(selection, $"{selected.DisplayName} · {MobileLivingWorldLayout.DirectionOf(selected.Id)} · 배치 {selected.AssignedCount}/{selected.MaxActive}");
            for (int index = 0; index < memberButtons.Count; index++)
            {
                MemberButton row = memberButtons[index];
                if (index >= overview.Members.Count) { SetActiveIfChanged(row.Button.gameObject, false); continue; }
                ContinuousHuntMemberDto member = overview.Members[index];
                row.Member = member;
                SetActiveIfChanged(row.Button.gameObject, true);
                bool here = member.AssignedRegionId == selectedRegionId;
                string location = member.AssignedRegionId == null ? "왕국" : overview.Regions.First(value => value.Id == member.AssignedRegionId).DisplayName;
                SetTextIfChanged(row.Label, $"{member.DisplayName} · {Job(member.JobId)}\n체력 {member.CurrentHpBps / 100f:0}% · {location}\n{(here ? "배치 해제" : "이곳에 배치")}");
                SetSpriteIfChanged(row.Icon, visuals.JobSprite(member.JobId));
                SetColorIfChanged(row.Button.targetGraphic, here ? new Color32(42, 112, 83, 255) : new Color32(42, 62, 62, 255));
            }
            foreach (WorldHuntRegionDto region in overview.Regions) RefreshRegion(region, regionViews[region.Id]);
            RefreshActors();
            if (CharacterDetailOpen) RenderCharacterDetail();
            HandleFeedback(feedback);
            if (feedback.Count == 0)
                ShowStatus(selected.Unlocked ? "용병을 눌러 배치하거나 해제하세요. 여러 명을 함께 배치할 수 있습니다." : "왕국 진행으로 개방해야 배치할 수 있습니다.");
        }

        private void RefreshRegion(WorldHuntRegionDto region, RegionView view)
        {
            view.AssignedCount = region.AssignedCount;
            bool selected = region.Id == selectedRegionId;
            SetOutlineColorIfChanged(view.Outline, selected ? new Color32(244, 208, 119, 230) : new Color32(232, 197, 115, 0));
            SetTextIfChanged(view.Title, region.DisplayName);
            SetTextIfChanged(view.Meta, $"티어 {region.Tier} · 전투력 {region.RecommendedPower:N0}\n배치 {region.AssignedCount}/{region.MaxActive}");
            SetColorIfChanged(view.RiskBackground, RiskColor(region.Tier));
            SetTextIfChanged(view.Risk, region.RiskLabel);
            SetTextIfChanged(view.Drop, "주요 " + region.DropPreview);
            SetActiveIfChanged(view.Locked, !region.Unlocked);
            WorldHuntMonsterDto[] monsters = overview.Monsters.Where(value => value.RegionId == region.Id).OrderBy(value => value.SpawnSlot).ToArray();
            for (int index = 0; index < view.Monsters.Count; index++)
            {
                MonsterView monsterView = view.Monsters[index];
                if (index >= monsters.Length) { SetActiveIfChanged(monsterView.Root, false); monsterView.Monster = null; continue; }
                WorldHuntMonsterDto monster = monsters[index];
                SetActiveIfChanged(monsterView.Root, region.Unlocked);
                monsterView.Monster = monster;
                bool elite = string.Equals(monster.Type, "ELITE", StringComparison.Ordinal);
                SetSpriteIfChanged(monsterView.Body, visuals.MonsterSprite(region.Theme, elite));
                monsterView.BaseColor = monster.State == "RESPAWNING" ? new Color(1f, 1f, 1f, .36f) : Color.white;
                SetColorIfChanged(monsterView.Body, monsterView.BaseColor);
                if (monsterView.EliteOutline.enabled != elite) monsterView.EliteOutline.enabled = elite;
                SetOutlineColorIfChanged(monsterView.EliteOutline, new Color32(202, 132, 235, 255));
                SetTextIfChanged(monsterView.Label, monster.State == "RESPAWNING" ? $"{monster.DisplayName}\n재등장 준비" : $"{(elite ? "정예 " : string.Empty)}{monster.DisplayName}\n레벨 {monster.Level}");
                SetAnchorMaxIfChanged(monsterView.Hp.rectTransform, new Vector2(monster.HpBps / 10000f, 1f));
            }
        }

        private void RefreshActors()
        {
            for (int index = 0; index < actorViews.Count; index++)
            {
                ActorView view = actorViews[index];
                if (index >= overview.Members.Count) { SetActiveIfChanged(view.Root, false); view.Member = null; continue; }
                ContinuousHuntMemberDto member = overview.Members[index];
                bool wasActive = view.Root.activeSelf;
                SetActiveIfChanged(view.Root, true);
                view.Member = member;
                SetSpriteIfChanged(view.Body, visuals.JobSprite(member.JobId));
                SetTextIfChanged(view.Label, $"{member.DisplayName}\n{State(member.State)}");
                SetColorIfChanged(view.Badge, StateColor(member.State));
                Color selectionColor = member.InstanceId == selectedMemberInstanceId && CharacterDetailOpen
                    ? new Color32(255, 218, 112, 230)
                    : new Color32(255, 218, 112, 0);
                SetOutlineColorIfChanged(view.SelectionOutline, selectionColor);
                string townFacility = MobileLivingWorldLayout.FacilityForState(member.State);
                view.Target = townFacility == null
                    ? MobileLivingWorldLayout.TargetFor(member)
                    : MobileLivingWorldLayout.FacilityPosition(townFacility) + ActorFormationOffset(index);
                if (!wasActive || !view.Initialized)
                {
                    SetAnchoredPositionIfChanged(view.Rect, view.Target);
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
                if (!IsWorldPointVisible(region.Root.anchoredPosition, 480f)) continue;
                bool activeRegion = region.AssignedCount > 0 || region.RegionId == selectedRegionId;
                if (!activeRegion && now < region.NextAmbientAnimationAt) continue;
                region.NextAmbientAnimationAt = now + (activeRegion ? ActiveWorldAnimationInterval : IdleRegionAnimationInterval);
                foreach (MonsterView monster in region.Monsters)
                {
                    if (!monster.Root.activeSelf || monster.Monster == null) continue;
                    float phase = now * .55f + monster.Index * 1.37f;
                    Vector2 roam = ReducedMotion || monster.Monster.State == "RESPAWNING" ? Vector2.zero : new Vector2(Mathf.Sin(phase) * 13f, Mathf.Cos(phase * .73f) * 8f);
                    SetAnchoredPositionIfChanged(monster.Rect, monster.BasePosition + roam);
                    SetColorIfChanged(monster.Body, now < monster.FlashUntil ? Color.Lerp(monster.BaseColor, Color.white, .86f) : monster.BaseColor);
                    float pulse = now < monster.PulseUntil ? 1f - Mathf.Clamp01((now - monster.PulseStarted) / Mathf.Max(.01f, monster.PulseUntil - monster.PulseStarted)) : 0f;
                    Color pulseColor = monster.Pulse.color; pulseColor.a = pulse * .68f; SetColorIfChanged(monster.Pulse, pulseColor);
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
                if (!IsWorldPointVisible(actor.Rect.anchoredPosition, 220f)) continue;
                float speed = ReducedMotion ? 4f : 2.3f;
                Vector2 livingTarget = actor.Target;
                if (!ReducedMotion && (actor.Member.State is "IDLE_TOWN" or "FIND_TARGET"))
                {
                    float phase = now * .46f + actor.Index * 1.19f;
                    livingTarget += new Vector2(Mathf.Sin(phase) * 34f, Mathf.Cos(phase * .77f) * 20f);
                }
                SetAnchoredPositionIfChanged(actor.Rect, Vector2.Lerp(actor.Rect.anchoredPosition, livingTarget, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime)));
                float lunge = now < actor.LungeUntil && !ReducedMotion ? Mathf.Sin(Mathf.Clamp01((now - actor.LungeStarted) / Mathf.Max(.01f, actor.LungeUntil - actor.LungeStarted)) * Mathf.PI) * 18f : 0f;
                float bob = ReducedMotion ? 0f : Mathf.Abs(Mathf.Sin(now * 4.2f + actor.Index * .8f)) * 3f;
                SetAnchoredPositionIfChanged(actor.Body.rectTransform, new Vector2(lunge, bob));
            }
        }

        private void ToggleSound()
        {
            MarkInteraction();
            feedbackAudio.SetSoundEnabled(!feedbackAudio.SoundEnabled);
            UpdatePreferenceLabels();
            ShowStatus(feedbackAudio.SoundEnabled ? "사냥 효과음을 켰습니다." : "사냥 효과음을 껐습니다. 시각 피드백은 유지됩니다.");
        }

        private void ToggleMotion()
        {
            MarkInteraction();
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
                SetColorIfChanged(row.Text, color);
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
            SetTextIfChanged(statusLine, message);
        }

        private void MarkInteraction()
        {
            lastInteractionAt = Time.unscaledTime;
            inputPriorityUntil = Mathf.Max(inputPriorityUntil, lastInteractionAt + InputPriorityGraceSeconds);
        }

        private static bool HasDirectPointerInput()
        {
            if (Mouse.current?.leftButton.isPressed == true) return true;
            Touchscreen touchscreen = Touchscreen.current;
            return touchscreen != null && touchscreen.primaryTouch.press.isPressed;
        }

        private void FlushPendingSafely(DateTimeOffset now)
        {
            if (service == null || !service.HasPendingPersistence) return;
            try
            {
                using (PersistenceMarker.Auto()) service.FlushPending(now);
            }
            catch (Exception exception)
            {
                nextPersistence = Time.unscaledTime + 5f;
                Debug.LogWarning(exception);
            }
        }

        private bool IsWorldPointVisible(Vector2 worldPoint, float margin)
        {
            if (worldViewport == null || worldContent == null) return true;
            float zoom = Mathf.Max(.01f, worldContent.localScale.x);
            Vector2 viewportPoint = worldContent.anchoredPosition + worldPoint * zoom;
            Vector2 half = worldViewport.rect.size * .5f + Vector2.one * margin;
            return Mathf.Abs(viewportPoint.x) <= half.x && Mathf.Abs(viewportPoint.y) <= half.y;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) FlushPendingSafely(DateTimeOffset.UtcNow);
        }

        private void OnApplicationQuit()
        {
            FlushPendingSafely(DateTimeOffset.UtcNow);
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

        private static Vector2 ActorFormationOffset(int index)
        {
            int ring = index / 5;
            int slot = index % 5;
            float angle = (18f + slot * 72f + ring * 36f) * Mathf.Deg2Rad;
            float radius = 235f + ring * 165f;
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .70f);
        }

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

        private static RectTransform CreateCanvasLayer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.GetComponent<Canvas>().overrideSorting = false;
            return rect;
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

        private static void SetTextIfChanged(TMP_Text target, string value)
        {
            if (target != null && target.text != value) target.text = value;
        }

        private static void SetActiveIfChanged(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        private static void SetColorIfChanged(Graphic target, Color value)
        {
            if (target != null && target.color != value) target.color = value;
        }

        private static void SetSpriteIfChanged(Image target, Sprite value)
        {
            if (target != null && target.sprite != value) target.sprite = value;
        }

        private static void SetOutlineColorIfChanged(Outline target, Color value)
        {
            if (target != null && target.effectColor != value) target.effectColor = value;
        }

        private static void SetAnchorMaxIfChanged(RectTransform target, Vector2 value)
        {
            if (target != null && target.anchorMax != value) target.anchorMax = value;
        }

        private static void SetAnchoredPositionIfChanged(RectTransform target, Vector2 value)
        {
            if (target != null && target.anchoredPosition != value) target.anchoredPosition = value;
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

        private static Color StateColor(string state) => state switch
        {
            "COMBAT" => new Color32(225, 93, 71, 255),
            "TRAVEL_TO_REGION" or "RETURN_TOWN" => new Color32(90, 169, 218, 255),
            "LOOT" or "SELL_LOOT" => new Color32(236, 184, 77, 255),
            "HEAL" => new Color32(91, 201, 150, 255),
            "TRAIN_SKILLS" or "ENHANCE_EQUIPMENT" => new Color32(187, 132, 222, 255),
            _ => new Color32(126, 190, 145, 255)
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
            public RegionView(string regionId, RectTransform root, Image background, Outline outline, TMP_Text title, TMP_Text meta, Image riskBackground,
                TMP_Text risk, TMP_Text drop, GameObject locked, List<Image> decorations, List<MonsterView> monsters)
            {
                RegionId = regionId; Root = root; Background = background; Outline = outline; Title = title; Meta = meta; RiskBackground = riskBackground;
                Risk = risk; Drop = drop; Locked = locked; Decorations = decorations; Monsters = monsters;
            }
            public string RegionId { get; }
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
            public int AssignedCount { get; set; }
            public float NextAmbientAnimationAt { get; set; }
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
            public ActorView(GameObject root, RectTransform rect, Button button, Image body, TMP_Text label, Image badge, Outline selectionOutline, int index)
            { Root = root; Rect = rect; Button = button; Body = body; Label = label; Badge = badge; SelectionOutline = selectionOutline; Index = index; }
            public GameObject Root { get; }
            public RectTransform Rect { get; }
            public Button Button { get; }
            public Image Body { get; }
            public TMP_Text Label { get; }
            public Image Badge { get; }
            public Outline SelectionOutline { get; }
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

    internal sealed class WorldActorTapTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private WorldMapDragSurface dragSurface;
        private RectTransform visualRoot;
        private Action selected;
        private Vector2 pointerDownPosition;
        private int pointerId = int.MinValue;

        public void Configure(WorldMapDragSurface surface, RectTransform root, Action callback)
        {
            dragSurface = surface;
            visualRoot = root;
            selected = callback;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerId = eventData.pointerId;
            pointerDownPosition = eventData.position;
            if (visualRoot != null) visualRoot.localScale = new Vector3(.96f, .96f, 1f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (visualRoot != null) visualRoot.localScale = Vector3.one;
            bool samePointer = pointerId == eventData.pointerId;
            pointerId = int.MinValue;
            if (!samePointer || eventData.dragging ||
                (eventData.position - pointerDownPosition).sqrMagnitude > WorldMapDragSurface.TapThreshold * WorldMapDragSurface.TapThreshold ||
                dragSurface?.TapSuppressed == true)
                return;
            selected?.Invoke();
        }

        private void OnDisable()
        {
            pointerId = int.MinValue;
            if (visualRoot != null) visualRoot.localScale = Vector3.one;
        }
    }
}
