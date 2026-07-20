using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingdomTycoon.Presentation.Combat
{
    /// <summary>Runtime-built integrated kingdom and hunting world. All mutations go through ContinuousHuntGameService.</summary>
    public sealed class ContinuousHuntScreenPresenter : MonoBehaviour
    {
        private const float WorldWidth = 4180f;
        private readonly List<MemberButton> memberButtons = new();
        private readonly Dictionary<string, RegionView> regionViews = new(StringComparer.Ordinal);
        private readonly WorldHuntFeedbackTracker feedbackTracker = new();
        private readonly List<FeedbackRow> feedbackRows = new();
        private ContinuousHuntGameService service;
        private WorldHuntFeedbackAudio feedbackAudio;
        private ContinuousHuntOverviewDto overview;
        private TMP_Text summary;
        private TMP_Text selection;
        private TMP_Text statusLine;
        private RectTransform worldViewport;
        private RectTransform worldContent;
        private WorldMapDragSurface dragSurface;
        private Button soundButton;
        private Button motionButton;
        private string selectedRegionId = "REGION_R01";
        private float nextTick;

        public RectTransform WorldViewport => worldViewport;
        public RectTransform WorldContent => worldContent;
        public WorldMapDragSurface DragSurface => dragSurface;
        public string SelectedRegionId => selectedRegionId;
        public bool SoundEnabled => feedbackAudio != null && feedbackAudio.SoundEnabled;
        public bool ReducedMotion => WorldHuntFeedbackPreferences.ReducedMotion;
        public IReadOnlyList<TMP_Text> FeedbackTexts => feedbackRows.Select(value => value.Text).ToArray();

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
            Canvas canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.overrideSorting = true; canvas.sortingOrder = 850;
            CanvasScaler scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            RectTransform root = (RectTransform)transform; root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            feedbackAudio = gameObject.AddComponent<WorldHuntFeedbackAudio>(); BuildUi(); UpdatePreferenceLabels(); gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true); transform.SetAsLastSibling();
            feedbackTracker.Reset(); ClearFeedbackRows();
            if (service == null)
            {
                if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) { ShowStatus("게임 정보를 준비하고 있습니다."); return; }
                service = AppRoot.Instance.Services.Get<ContinuousHuntGameService>(); service.Changed += OnChanged;
            }
            Refresh(); Canvas.ForceUpdateCanvases(); dragSurface.ClampToBounds();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            KingdomTycoon.Presentation.Regions.RegionMapScreenPresenter legacy = FindFirstObjectByType<KingdomTycoon.Presentation.Regions.RegionMapScreenPresenter>(FindObjectsInactive.Include);
            if (legacy != null && legacy.gameObject.activeSelf) legacy.gameObject.SetActive(false);
        }

        private void Update()
        {
            AnimateWorld();
            if (service == null || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            try { service.AdvanceTo(DateTimeOffset.UtcNow); }
            catch (Exception exception) { Debug.LogWarning(exception); ShowStatus("사냥 기록을 갱신하지 못했습니다. 저장 상태를 유지하고 다시 시도합니다."); }
        }

        private void BuildUi()
        {
            Image background = Panel("월드배경", transform, new Color32(7, 13, 16, 255), Vector2.zero, Vector2.one);
            Image safe = Panel("안전영역", background.transform, new Color32(13, 23, 25, 255), new Vector2(.014f, .02f), new Vector2(.986f, .98f));
            safe.gameObject.AddComponent<KingdomTycoon.UI.SafeAreaFitter>();

            Image header = Panel("상단정보", safe.transform, new Color32(20, 34, 35, 255), new Vector2(.012f, .895f), new Vector2(.988f, .988f));
            Text("화면분류", header.transform, "왕국 관찰 · 상시 자동 사냥", 17, TextAlignmentOptions.Left, new Vector2(.018f, .54f), new Vector2(.42f, .94f), new Color32(119, 193, 171, 255));
            Text("화면제목", header.transform, "통합 사냥 월드", 34, TextAlignmentOptions.Left, new Vector2(.018f, .05f), new Vector2(.38f, .62f), new Color32(238, 203, 123, 255));
            summary = Text("전체요약", header.transform, "배치 0명 · 누적 수익 0골드", 18, TextAlignmentOptions.Right, new Vector2(.38f, .16f), new Vector2(.65f, .84f), new Color32(193, 211, 201, 255));
            soundButton = MakeButton("효과음설정", header.transform, "효과음 켬", new Color32(45, 83, 77, 255), new Vector2(.66f, .12f), new Vector2(.765f, .88f));
            motionButton = MakeButton("모션설정", header.transform, "모션 보통", new Color32(56, 76, 84, 255), new Vector2(.775f, .12f), new Vector2(.88f, .88f));
            Button close = MakeButton("왕국으로", header.transform, "왕국으로", new Color32(91, 66, 45, 255), new Vector2(.89f, .12f), new Vector2(.986f, .88f));
            soundButton.onClick.AddListener(ToggleSound); motionButton.onClick.AddListener(ToggleMotion); close.onClick.AddListener(Close);
            soundButton.GetComponentInChildren<TMP_Text>().fontSize = 15; motionButton.GetComponentInChildren<TMP_Text>().fontSize = 15;

            Image viewportImage = Panel("월드뷰포트", safe.transform, new Color32(19, 43, 47, 255), new Vector2(.012f, .17f), new Vector2(.988f, .885f));
            viewportImage.raycastTarget = true; viewportImage.gameObject.AddComponent<RectMask2D>(); worldViewport = viewportImage.rectTransform;
            dragSurface = viewportImage.gameObject.AddComponent<WorldMapDragSurface>();
            var contentObject = new GameObject("드래그월드", typeof(RectTransform)); contentObject.transform.SetParent(viewportImage.transform, false); worldContent = (RectTransform)contentObject.transform;
            worldContent.anchorMin = new Vector2(0f, 0f); worldContent.anchorMax = new Vector2(0f, 1f); worldContent.pivot = new Vector2(0f, .5f); worldContent.sizeDelta = new Vector2(WorldWidth, 0f); worldContent.anchoredPosition = Vector2.zero;
            dragSurface.Configure(worldViewport, worldContent); BuildWorldBase();
            BuildFeedbackRail(viewportImage.transform);

            Image drawer = Panel("용병배치서랍", safe.transform, new Color32(18, 31, 34, 255), new Vector2(.012f, .03f), new Vector2(.988f, .158f));
            selection = Text("선택지역", drawer.transform, "초록바람 들판 · 용병을 눌러 배치", 19, TextAlignmentOptions.Left, new Vector2(.012f, .60f), new Vector2(.33f, .96f), new Color32(232, 206, 139, 255));
            statusLine = Text("상태안내", drawer.transform, "월드를 좌우로 드래그해 사냥터를 관찰하세요.", 15, TextAlignmentOptions.Left, new Vector2(.012f, .08f), new Vector2(.33f, .58f), new Color32(151, 179, 171, 255));
            for (int index = 0; index < 8; index++) memberButtons.Add(CreateMemberButton(drawer.transform, index));
        }

        private void BuildWorldBase()
        {
            Panel("하늘", worldContent, new Color32(28, 61, 64, 255), Vector2.zero, Vector2.one);
            Panel("먼산", worldContent, new Color32(34, 72, 65, 255), new Vector2(0f, .35f), new Vector2(1f, .68f));
            Panel("대지", worldContent, new Color32(45, 69, 51, 255), new Vector2(0f, 0f), new Vector2(1f, .38f));
            Image road = FixedPanel("왕국길", worldContent, new Color32(119, 91, 58, 255), 200f, 3700f, .16f, .24f); road.raycastTarget = false;

            Image kingdom = FixedPanel("왕국거점", worldContent, new Color32(41, 55, 48, 255), 50f, 580f, .08f, .92f);
            Text("왕국이름", kingdom.transform, "재건 중인 왕국", 30, TextAlignmentOptions.Center, new Vector2(.05f, .82f), new Vector2(.95f, .95f), new Color32(235, 201, 122, 255));
            Text("왕국설명", kingdom.transform, "귀환 · 판매 · 물약 · 장비 · 훈련 · 강화", 17, TextAlignmentOptions.Center, new Vector2(.08f, .68f), new Vector2(.92f, .80f), new Color32(174, 194, 182, 255));
            Building(kingdom.transform, "성채", new Vector2(.34f, .26f), new Vector2(.66f, .66f), new Color32(111, 107, 94, 255));
            Building(kingdom.transform, "상점", new Vector2(.08f, .15f), new Vector2(.30f, .44f), new Color32(155, 104, 56, 255));
            Building(kingdom.transform, "대장간", new Vector2(.70f, .15f), new Vector2(.92f, .44f), new Color32(102, 82, 78, 255));
            Text("드래그안내", worldContent, "← 드래그하여 모든 사냥터 관찰 →", 18, TextAlignmentOptions.Center, new Vector2(.08f, .90f), new Vector2(.40f, .98f), new Color32(201, 218, 205, 210));
        }

        private void BuildFeedbackRail(Transform parent)
        {
            Image rail = Panel("최근피드", parent, new Color32(10, 21, 24, 205), new Vector2(.715f, .665f), new Vector2(.985f, .965f)); rail.raycastTarget = false;
            Text("최근피드제목", rail.transform, "전투 · 보상 소식", 15, TextAlignmentOptions.Left, new Vector2(.055f, .78f), new Vector2(.945f, .96f), new Color32(235, 204, 127, 255));
            for (int index = 0; index < 5; index++)
            {
                float top = .75f - index * .145f; TMP_Text row = Text("최근피드_" + index, rail.transform, "", 14, TextAlignmentOptions.Left,
                    new Vector2(.055f, top - .12f), new Vector2(.945f, top), new Color32(207, 221, 212, 255));
                row.gameObject.SetActive(false); feedbackRows.Add(new FeedbackRow(row));
            }
        }

        private void ToggleSound()
        {
            feedbackAudio.SetSoundEnabled(!feedbackAudio.SoundEnabled); UpdatePreferenceLabels();
            ShowStatus(feedbackAudio.SoundEnabled ? "사냥 효과음을 켰습니다." : "사냥 효과음을 껐습니다. 시각 피드백은 유지됩니다.");
        }

        private void ToggleMotion()
        {
            WorldHuntFeedbackPreferences.ReducedMotion = !WorldHuntFeedbackPreferences.ReducedMotion; UpdatePreferenceLabels();
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
                bool skill = item.Type == WorldHuntFeedbackType.SkillHit; bool defeated = item.Type == WorldHuntFeedbackType.MonsterDefeated;
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
                monster.Feedback.color = monster.FeedbackColor; monster.FeedbackStarted = now; monster.FeedbackUntil = now + overview.Feedback.DamageFloatSeconds; monster.Feedback.gameObject.SetActive(true);
                monster.FlashStarted = now; monster.FlashUntil = now + (skill ? overview.Feedback.SkillPulseSeconds : overview.Feedback.HitFlashSeconds);
                monster.PulseStarted = now; monster.PulseUntil = now + (skill || item.Type is WorldHuntFeedbackType.MonsterDefeated or WorldHuntFeedbackType.MonsterRespawned ? overview.Feedback.SkillPulseSeconds : overview.Feedback.HitFlashSeconds);
                Color pulse = monster.FeedbackColor; pulse.a = .7f; monster.Pulse.color = pulse;
                break;
            }
            if (item.Type is not (WorldHuntFeedbackType.BasicHit or WorldHuntFeedbackType.SkillHit or WorldHuntFeedbackType.MonsterDefeated)) return;
            foreach (ActorView actor in region.Actors)
            {
                if (actor.Member == null || actor.Member.InstanceId != item.MercenaryInstanceId) continue;
                actor.LungeStarted = now; actor.LungeUntil = now + (item.Type == WorldHuntFeedbackType.SkillHit ? overview.Feedback.SkillPulseSeconds : overview.Feedback.HitFlashSeconds + .12f); break;
            }
        }

        private void PushFeedbackRow(WorldHuntFeedbackEvent item)
        {
            int limit = Math.Min(feedbackRows.Count, overview.Feedback.MaximumRewardFeedEntries);
            for (int index = limit - 1; index > 0; index--)
            {
                feedbackRows[index].Text.text = feedbackRows[index - 1].Text.text; feedbackRows[index].Color = feedbackRows[index - 1].Color;
                feedbackRows[index].ExpiresAt = feedbackRows[index - 1].ExpiresAt; feedbackRows[index].Text.color = feedbackRows[index].Color;
                feedbackRows[index].Text.gameObject.SetActive(feedbackRows[index - 1].Text.gameObject.activeSelf);
            }
            FeedbackRow first = feedbackRows[0]; first.Text.text = item.Message; first.Color = FeedbackColor(item.Type); first.Text.color = first.Color;
            first.ExpiresAt = Time.unscaledTime + overview.Feedback.RewardFeedSeconds; first.Text.gameObject.SetActive(true);
        }

        private void UpdateFeedbackRows(float now)
        {
            foreach (FeedbackRow row in feedbackRows)
            {
                if (!row.Text.gameObject.activeSelf) continue;
                if (now >= row.ExpiresAt) { row.Text.gameObject.SetActive(false); continue; }
                Color color = row.Color; float remaining = row.ExpiresAt - now; color.a = overview?.Feedback != null && remaining < .5f ? Mathf.Clamp01(remaining / .5f) : 1f; row.Text.color = color;
            }
        }

        private void ClearFeedbackRows()
        {
            foreach (FeedbackRow row in feedbackRows) { row.Text.text = string.Empty; row.ExpiresAt = 0f; row.Text.gameObject.SetActive(false); }
        }

        private MemberButton CreateMemberButton(Transform parent, int index)
        {
            const float start = .345f, gap = .007f; float width = (.985f - start - gap * 7f) / 8f; float left = start + index * (width + gap);
            Button button = MakeButton("용병_" + index, parent, "", new Color32(42, 59, 60, 255), new Vector2(left, .10f), new Vector2(left + width, .90f));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(); label.fontSize = 15; label.textWrappingMode = TextWrappingModes.Normal; label.margin = new Vector4(6, 3, 6, 3);
            var row = new MemberButton(button, label); button.onClick.AddListener(() => ToggleAssignment(row)); button.gameObject.SetActive(false); return row;
        }

        private void EnsureRegionViews()
        {
            if (overview == null) return;
            foreach (WorldHuntRegionDto region in overview.Regions)
            {
                if (!regionViews.TryGetValue(region.Id, out RegionView view))
                {
                    view = CreateRegionView(region); regionViews.Add(region.Id, view);
                }
                view.Root.anchoredPosition = new Vector2(region.WorldX, 0f);
            }
        }

        private RegionView CreateRegionView(WorldHuntRegionDto region)
        {
            Image rootImage = FixedPanel("월드지역_" + region.Id, worldContent, ThemeColor(region.Theme), region.WorldX, 600f, .07f, .93f);
            Button button = rootImage.gameObject.AddComponent<Button>(); button.targetGraphic = rootImage;
            var outline = rootImage.gameObject.AddComponent<Outline>(); outline.effectDistance = new Vector2(3f, -3f); outline.effectColor = new Color32(232, 197, 115, 0);
            button.onClick.AddListener(() => SelectRegion(region.Id, true));
            TMP_Text title = Text("지역명", rootImage.transform, region.DisplayName, 25, TextAlignmentOptions.Left, new Vector2(.04f, .87f), new Vector2(.58f, .97f), new Color32(242, 225, 176, 255));
            TMP_Text meta = Text("지역정보", rootImage.transform, "", 15, TextAlignmentOptions.Right, new Vector2(.55f, .88f), new Vector2(.96f, .96f), new Color32(205, 217, 205, 255));
            Image riskBackground = Panel("위험도배지", rootImage.transform, new Color32(44, 99, 72, 255), new Vector2(.04f, .775f), new Vector2(.25f, .855f));
            TMP_Text risk = Text("위험도", riskBackground.transform, "안정", 14, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white);
            TMP_Text drop = Text("대표전리품", rootImage.transform, "주요 전리품", 14, TextAlignmentOptions.Left, new Vector2(.28f, .775f), new Vector2(.96f, .855f), new Color32(224, 205, 151, 255));
            Panel("지역지면", rootImage.transform, GroundColor(region.Theme), new Vector2(.02f, .06f), new Vector2(.98f, .48f));
            Text("지역길", rootImage.transform, "━━━━━━  사냥 순환로  ━━━━━━", 14, TextAlignmentOptions.Center, new Vector2(.06f, .43f), new Vector2(.94f, .50f), new Color32(210, 184, 126, 180));
            Image locked = Panel("잠금표시", rootImage.transform, new Color32(12, 18, 21, 220), new Vector2(.02f, .05f), new Vector2(.98f, .86f));
            Text("잠금문구", locked.transform, "아직 개방되지 않은 사냥터", 22, TextAlignmentOptions.Center, new Vector2(.12f, .38f), new Vector2(.88f, .62f), new Color32(161, 167, 164, 255));
            var monsters = new List<MonsterView>(); for (int index = 0; index < 5; index++) monsters.Add(CreateMonsterView(rootImage.transform, index));
            var actors = new List<ActorView>(); for (int index = 0; index < 8; index++) actors.Add(CreateActorView(rootImage.transform, index));
            return new RegionView(rootImage.rectTransform, rootImage, outline, title, meta, riskBackground, risk, drop, locked.gameObject, monsters, actors);
        }

        private MonsterView CreateMonsterView(Transform parent, int index)
        {
            float x = .10f + (index % 3) * .30f, y = index < 3 ? .60f : .32f;
            var root = new GameObject("몬스터동작_" + index, typeof(RectTransform)); root.transform.SetParent(parent, false); RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(x, y); rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(150, 92);
            Image pulse = Panel("타격펄스", root.transform, new Color32(255, 230, 155, 0), new Vector2(.28f, .16f), new Vector2(.72f, .74f)); pulse.raycastTarget = false;
            Image body = Panel("몬스터몸", root.transform, new Color32(157, 72, 60, 255), new Vector2(.36f, .22f), new Vector2(.64f, .68f));
            Outline eliteOutline = body.gameObject.AddComponent<Outline>(); eliteOutline.effectDistance = new Vector2(2f, -2f); eliteOutline.effectColor = new Color32(211, 146, 235, 0);
            TMP_Text label = Text("몬스터이름", root.transform, "", 13, TextAlignmentOptions.Center, new Vector2(0f, .72f), new Vector2(1f, 1f), Color.white);
            TMP_Text feedback = Text("피해표시", root.transform, "", 20, TextAlignmentOptions.Center, new Vector2(-.10f, .32f), new Vector2(1.10f, .78f), new Color32(255, 244, 218, 255)); feedback.fontStyle = FontStyles.Bold; feedback.gameObject.SetActive(false);
            Image hpBack = Panel("몬스터체력바", root.transform, new Color32(37, 30, 30, 255), new Vector2(.12f, .08f), new Vector2(.88f, .22f));
            Image hp = Panel("몬스터현재체력", hpBack.transform, new Color32(197, 69, 55, 255), Vector2.zero, Vector2.one); hp.type = Image.Type.Filled; hp.fillMethod = Image.FillMethod.Horizontal;
            root.SetActive(false); return new MonsterView(root, rect, body, label, hp, pulse, feedback, eliteOutline, index);
        }

        private ActorView CreateActorView(Transform parent, int index)
        {
            var root = new GameObject("용병동작_" + index, typeof(RectTransform)); root.transform.SetParent(parent, false); RectTransform rect = (RectTransform)root.transform;
            float x = .12f + (index % 4) * .23f, y = index < 4 ? .18f : .08f; rect.anchorMin = rect.anchorMax = new Vector2(x, y); rect.sizeDelta = new Vector2(112, 58);
            Image body = Panel("용병몸", root.transform, new Color32(68, 147, 126, 255), new Vector2(.02f, .18f), new Vector2(.30f, .88f));
            TMP_Text label = Text("용병이름", root.transform, "", 12, TextAlignmentOptions.Left, new Vector2(.33f, 0f), new Vector2(1f, 1f), Color.white);
            root.SetActive(false); return new ActorView(root, rect, body, label, index, rect.anchoredPosition);
        }

        private void SelectRegion(string regionId, bool pan)
        {
            selectedRegionId = regionId; Refresh();
            WorldHuntRegionDto region = overview?.Regions.SingleOrDefault(value => value.Id == regionId); if (pan && region != null) dragSurface.PanToWorldX(region.WorldX + 300f);
        }

        private void ToggleAssignment(MemberButton row)
        {
            if (service == null || row.Member == null || overview == null) return;
            try
            {
                if (row.Member.AssignedRegionId == selectedRegionId) { service.Unassign(row.Member.InstanceId); ShowStatus($"{row.Member.DisplayName}에게 귀환 명령을 내렸습니다."); }
                else { service.Assign(row.Member.InstanceId, selectedRegionId); ShowStatus($"{row.Member.DisplayName}을(를) 선택 사냥터에 배치했습니다."); }
                Refresh();
            }
            catch (Exception exception) { Debug.LogWarning(exception); ShowStatus("배치할 수 없습니다. 지역 개방 상태와 최대 배치 인원을 확인하세요."); }
        }

        private void Refresh(ContinuousHuntOverviewDto snapshot = null)
        {
            if (service == null || !service.IsBootstrapped) return;
            ContinuousHuntOverviewDto next = snapshot ?? service.GetOverview(); IReadOnlyList<WorldHuntFeedbackEvent> feedback = feedbackTracker.Observe(next); overview = next;
            feedbackAudio.Configure(overview.Feedback); EnsureRegionViews();
            summary.text = $"배치 {overview.AssignedCount}명 · 누적 수익 {overview.EarnedGold:N0}골드 · 몬스터 {overview.Monsters.Count(value => value.State == "ACTIVE")}마리";
            WorldHuntRegionDto selected = overview.Regions.Single(value => value.Id == selectedRegionId);
            selection.text = $"{selected.DisplayName} · 배치 {selected.AssignedCount}/{selected.MaxActive}명";
            foreach (WorldHuntRegionDto region in overview.Regions) RefreshRegion(region, regionViews[region.Id]);
            for (int index = 0; index < memberButtons.Count; index++)
            {
                MemberButton row = memberButtons[index]; row.Member = index < overview.Members.Count ? overview.Members[index] : null; row.Button.gameObject.SetActive(row.Member != null);
                if (row.Member == null) continue;
                bool here = row.Member.AssignedRegionId == selectedRegionId; string assigned = row.Member.AssignedRegionId == null ? "마을" : here ? "이곳" : "타지역";
                row.Label.text = $"<b>{row.Member.DisplayName}</b>\n{Job(row.Member.JobId)} · {assigned}\n체력 {row.Member.CurrentHpBps / 100}% · 가방 {row.Member.BagFill}/{row.Member.BagCapacity}\n스킬 {row.Member.TotalSkillLevels} · 강화 +{row.Member.BestEnhancementLevel}";
                row.Button.image.color = here ? new Color32(49, 118, 94, 255) : row.Member.AssignedRegionId == null ? new Color32(49, 61, 61, 255) : new Color32(62, 72, 68, 255);
            }
            ContinuousHuntMemberDto active = overview.Members.FirstOrDefault(value => value.AssignedRegionId == selectedRegionId && value.State == "COMBAT") ?? overview.Members.FirstOrDefault(value => value.AssignedRegionId == selectedRegionId);
            if (active != null) ShowStatus($"{active.DisplayName} · {State(active.State)} · 최근 피해 {active.LastCombatDamage:N0} · {Skill(active.LastSkillId)} · 가방 {active.BagFill}/{active.BagCapacity}");
            else ShowStatus(selected.Unlocked ? "용병을 눌러 이 사냥터에 배치하세요. 여러 명을 함께 배치할 수 있습니다." : "왕국 진행으로 개방해야 배치할 수 있습니다.");
            HandleFeedback(feedback);
        }

        private void RefreshRegion(WorldHuntRegionDto region, RegionView view)
        {
            bool selected = region.Id == selectedRegionId; view.Outline.effectColor = selected ? new Color32(244, 197, 91, 255) : new Color32(0, 0, 0, 0);
            view.Title.text = region.DisplayName; view.Meta.text = $"권장 {region.RecommendedPower} · 배치 {region.AssignedCount}/{region.MaxActive}";
            view.Risk.text = region.RiskLabel; view.RiskBackground.color = RiskColor(region.Tier); view.Drop.text = "주요 " + region.DropPreview; view.Locked.SetActive(!region.Unlocked);
            WorldHuntMonsterDto[] monsters = overview.Monsters.Where(value => value.RegionId == region.Id).OrderBy(value => value.SpawnSlot).ToArray();
            for (int index = 0; index < view.Monsters.Count; index++)
            {
                MonsterView actor = view.Monsters[index]; actor.Monster = index < monsters.Length ? monsters[index] : null; actor.Root.SetActive(actor.Monster != null && region.Unlocked);
                if (actor.Monster == null) continue;
                actor.Label.text = actor.Monster.State == "RESPAWNING" ? "재생성 중" : actor.Monster.Type == "ELITE" ? $"정예 · {actor.Monster.DisplayName}  레벨 {actor.Monster.Level}" : $"{actor.Monster.DisplayName}  레벨 {actor.Monster.Level}";
                actor.Hp.fillAmount = actor.Monster.HpBps / 10000f; actor.BaseColor = actor.Monster.Type == "ELITE" ? new Color32(142, 78, 166, 255) : new Color32(164, 72, 58, 255);
                if (actor.Monster.State == "RESPAWNING") actor.BaseColor = new Color32(82, 85, 83, 180); actor.Body.color = actor.BaseColor;
                actor.EliteOutline.effectColor = actor.Monster.Type == "ELITE" ? new Color32(211, 146, 235, 230) : new Color32(0, 0, 0, 0);
            }
            ContinuousHuntMemberDto[] members = overview.Members.Where(value => value.AssignedRegionId == region.Id).Take(view.Actors.Count).ToArray();
            for (int index = 0; index < view.Actors.Count; index++)
            {
                ActorView actor = view.Actors[index]; actor.Member = index < members.Length ? members[index] : null; actor.Root.SetActive(actor.Member != null && region.Unlocked);
                if (actor.Member != null) { actor.Label.text = $"{actor.Member.DisplayName}\n{State(actor.Member.State)}"; actor.Body.color = JobColor(actor.Member.JobId); }
            }
        }

        private void AnimateWorld()
        {
            if (!gameObject.activeInHierarchy) return; float time = Time.unscaledTime; bool reduced = ReducedMotion;
            UpdateFeedbackRows(time);
            foreach (RegionView region in regionViews.Values)
            {
                foreach (MonsterView monster in region.Monsters)
                {
                    if (monster.Monster == null || !monster.Root.activeSelf) continue;
                    float pulse = monster.Monster.State == "ACTIVE" ? reduced ? 1f : 1f + Mathf.Sin(time * 3.2f + monster.Index) * .025f : .72f;
                    if (time < monster.PulseUntil && !reduced) pulse += Mathf.Sin(Mathf.InverseLerp(monster.PulseStarted, monster.PulseUntil, time) * Mathf.PI) * .18f;
                    monster.Body.rectTransform.localScale = Vector3.one * pulse;
                    float flash = time < monster.FlashUntil ? Mathf.Sin(Mathf.InverseLerp(monster.FlashStarted, monster.FlashUntil, time) * Mathf.PI) : 0f;
                    monster.Body.color = Color.Lerp(monster.BaseColor, Color.white, flash * .82f);
                    if (monster.Feedback.gameObject.activeSelf)
                    {
                        float progress = Mathf.InverseLerp(monster.FeedbackStarted, monster.FeedbackUntil, time); Color color = monster.FeedbackColor; color.a = 1f - progress; monster.Feedback.color = color;
                        monster.Feedback.rectTransform.anchoredPosition = reduced ? Vector2.zero : new Vector2(0f, progress * 36f);
                        if (time >= monster.FeedbackUntil) { monster.Feedback.gameObject.SetActive(false); monster.Feedback.rectTransform.anchoredPosition = Vector2.zero; }
                    }
                    float pulseProgress = Mathf.InverseLerp(monster.PulseStarted, monster.PulseUntil, time); Color pulseColor = monster.Pulse.color; pulseColor.a = time < monster.PulseUntil ? (1f - pulseProgress) * .7f : 0f; monster.Pulse.color = pulseColor;
                }
                foreach (ActorView actor in region.Actors)
                {
                    if (actor.Member == null || !actor.Root.activeSelf) continue;
                    float movement = reduced ? 0f : actor.Member.State switch { "TRAVEL_TO_REGION" => Mathf.PingPong(time * 20f, 34f), "FIND_TARGET" => Mathf.Sin(time * 2f + actor.Index) * 8f, "LOOT" => 18f, "RETURN_TOWN" => -Mathf.PingPong(time * 28f, 36f), _ => 0f };
                    if (!reduced && time < actor.LungeUntil) movement += Mathf.Sin(Mathf.InverseLerp(actor.LungeStarted, actor.LungeUntil, time) * Mathf.PI) * 24f;
                    actor.Rect.anchoredPosition = actor.BasePosition + new Vector2(movement, reduced ? 0f : Mathf.Sin(time * 2.4f + actor.Index) * 3f); actor.Rect.localScale = Vector3.one;
                }
            }
        }

        private void OnChanged(object sender, ContinuousHuntOverviewDto value) { if (gameObject.activeInHierarchy) Refresh(value); else overview = value; }
        private void ShowStatus(string message) { if (statusLine != null) statusLine.text = message; }
        private void OnDestroy() { if (service != null) service.Changed -= OnChanged; }

        private static void Building(Transform parent, string label, Vector2 min, Vector2 max, Color color)
        {
            Image body = Panel(label, parent, color, min, max); Text(label + "표시", body.transform, label, 15, TextAlignmentOptions.Center, new Vector2(0f, .15f), new Vector2(1f, .85f), Color.white);
        }
        private static Image FixedPanel(string name, Transform parent, Color color, float x, float width, float minY, float maxY)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = color;
            RectTransform rect = image.rectTransform; rect.anchorMin = new Vector2(0f, minY); rect.anchorMax = new Vector2(0f, maxY); rect.pivot = new Vector2(0f, .5f); rect.sizeDelta = new Vector2(width, 0f); rect.anchoredPosition = new Vector2(x, 0f); return image;
        }
        private static Image Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false); Image image = go.GetComponent<Image>(); image.color = color; SetRect(image.rectTransform, min, max); return image;
        }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false); TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value; text.fontSize = size; text.alignment = alignment; text.color = color; text.textWrappingMode = TextWrappingModes.Normal; text.raycastTarget = false; SetRect(text.rectTransform, min, max); return text;
        }
        private static Button MakeButton(string name, Transform parent, string value, Color color, Vector2 min, Vector2 max)
        {
            Image image = Panel(name, parent, color, min, max); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; Text("글자", image.transform, value, 18, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white); return button;
        }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static Color ThemeColor(string theme) => theme switch { "MEADOW" => new Color32(49, 91, 67, 255), "FOREST" => new Color32(38, 75, 64, 255), "MINE" => new Color32(76, 68, 61, 255), "SWAMP" => new Color32(61, 74, 53, 255), "FROST_RUIN" => new Color32(57, 78, 86, 255), _ => new Color32(50, 72, 66, 255) };
        private static Color GroundColor(string theme) => theme switch { "MEADOW" => new Color32(71, 112, 65, 255), "FOREST" => new Color32(49, 88, 61, 255), "MINE" => new Color32(92, 78, 64, 255), "SWAMP" => new Color32(74, 89, 56, 255), "FROST_RUIN" => new Color32(82, 107, 114, 255), _ => new Color32(67, 91, 68, 255) };
        private static Color RiskColor(int tier) => tier switch { 1 => new Color32(44, 107, 73, 255), 2 => new Color32(119, 105, 48, 255), 3 => new Color32(142, 83, 45, 255), 4 => new Color32(142, 58, 52, 255), _ => new Color32(113, 60, 133, 255) };
        private static Color FeedbackColor(WorldHuntFeedbackType type) => type switch
        {
            WorldHuntFeedbackType.LootCollected => new Color32(117, 218, 184, 255),
            WorldHuntFeedbackType.BountyEarned or WorldHuntFeedbackType.Settled => new Color32(244, 199, 91, 255),
            WorldHuntFeedbackType.SkillTrained or WorldHuntFeedbackType.EquipmentEnhanced => new Color32(190, 151, 238, 255),
            WorldHuntFeedbackType.MonsterDefeated => new Color32(255, 137, 90, 255),
            WorldHuntFeedbackType.MonsterRespawned => new Color32(127, 221, 185, 255),
            _ => new Color32(207, 221, 212, 255)
        };
        private static string Job(string id) => id switch { "JOB_WARRIOR" => "전사", "JOB_GUARDIAN" => "수호자", "JOB_ARCHER" => "궁수", "JOB_MAGE" => "마법사", "JOB_CLERIC" => "성직자", _ => "용병" };
        private static string State(string state) => state switch { "IDLE_TOWN" => "마을 대기", "TRAVEL_TO_REGION" => "이동 중", "FIND_TARGET" => "목표 탐색", "COMBAT" => "전투 중", "LOOT" => "전리품 수집", "CONTINUE_DECISION" => "상태 점검", "RETURN_TOWN" => "마을 귀환", "SELL_LOOT" => "전리품 정산", "HEAL" => "치료", "BUY_CONSUMABLES" => "물약 구매", "EVALUATE_EQUIPMENT" => "장비 비교", "BUY_EQUIPMENT" => "장비 구매", "TRAIN_SKILLS" => "스킬 훈련", "ENHANCE_EQUIPMENT" => "장비 강화", _ => "정비 중" };
        private static string Skill(string id) => string.IsNullOrEmpty(id) ? "기본 공격" : "자동 스킬";
        private static Color JobColor(string id) => id switch { "JOB_WARRIOR" => new Color32(181, 91, 64, 255), "JOB_GUARDIAN" => new Color32(105, 124, 151, 255), "JOB_ARCHER" => new Color32(75, 157, 102, 255), "JOB_MAGE" => new Color32(72, 119, 174, 255), "JOB_CLERIC" => new Color32(199, 167, 89, 255), _ => new Color32(93, 137, 128, 255) };

        private sealed class MemberButton
        {
            public MemberButton(Button button, TMP_Text label) { Button = button; Label = label; }
            public Button Button { get; } public TMP_Text Label { get; } public ContinuousHuntMemberDto Member { get; set; }
        }
        private sealed class RegionView
        {
            public RegionView(RectTransform root, Image background, Outline outline, TMP_Text title, TMP_Text meta, Image riskBackground, TMP_Text risk, TMP_Text drop, GameObject locked, List<MonsterView> monsters, List<ActorView> actors)
            { Root = root; Background = background; Outline = outline; Title = title; Meta = meta; RiskBackground = riskBackground; Risk = risk; Drop = drop; Locked = locked; Monsters = monsters; Actors = actors; }
            public RectTransform Root { get; } public Image Background { get; } public Outline Outline { get; } public TMP_Text Title { get; } public TMP_Text Meta { get; }
            public Image RiskBackground { get; } public TMP_Text Risk { get; } public TMP_Text Drop { get; } public GameObject Locked { get; }
            public List<MonsterView> Monsters { get; } public List<ActorView> Actors { get; }
        }
        private sealed class MonsterView
        {
            public MonsterView(GameObject root, RectTransform rect, Image body, TMP_Text label, Image hp, Image pulse, TMP_Text feedback, Outline eliteOutline, int index)
            { Root = root; Rect = rect; Body = body; Label = label; Hp = hp; Pulse = pulse; Feedback = feedback; EliteOutline = eliteOutline; Index = index; }
            public GameObject Root { get; } public RectTransform Rect { get; } public Image Body { get; } public TMP_Text Label { get; } public Image Hp { get; }
            public Image Pulse { get; } public TMP_Text Feedback { get; } public Outline EliteOutline { get; } public int Index { get; } public WorldHuntMonsterDto Monster { get; set; }
            public Color BaseColor { get; set; } public Color FeedbackColor { get; set; } public float FeedbackStarted { get; set; } public float FeedbackUntil { get; set; }
            public float FlashStarted { get; set; } public float FlashUntil { get; set; } public float PulseStarted { get; set; } public float PulseUntil { get; set; }
        }
        private sealed class ActorView
        {
            public ActorView(GameObject root, RectTransform rect, Image body, TMP_Text label, int index, Vector2 basePosition) { Root = root; Rect = rect; Body = body; Label = label; Index = index; BasePosition = basePosition; }
            public GameObject Root { get; } public RectTransform Rect { get; } public Image Body { get; } public TMP_Text Label { get; } public int Index { get; } public Vector2 BasePosition { get; } public ContinuousHuntMemberDto Member { get; set; }
            public float LungeStarted { get; set; } public float LungeUntil { get; set; }
        }
        private sealed class FeedbackRow
        {
            public FeedbackRow(TMP_Text text) { Text = text; Color = new Color32(207, 221, 212, 255); }
            public TMP_Text Text { get; } public Color Color { get; set; } public float ExpiresAt { get; set; }
        }
    }
}
