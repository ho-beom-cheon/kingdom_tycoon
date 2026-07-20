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
    /// <summary>Runtime-built screen so the corrected hunt loop is available in every upgraded P15 scene.</summary>
    public sealed class ContinuousHuntScreenPresenter : MonoBehaviour
    {
        private static readonly string[] RegionIds = { "REGION_R01", "REGION_R02", "REGION_R03", "REGION_R04", "REGION_R05" };
        private static readonly string[] RegionNames = { "초록바람 들판", "잿빛 채석장", "독안개 습지", "설원 협곡", "붉은 고원" };
        private readonly List<MemberRow> rows = new();
        private readonly List<ActorView> actors = new();
        private ContinuousHuntGameService service;
        private TMP_Text title;
        private TMP_Text summary;
        private TMP_Text toast;
        private TMP_Text fieldHint;
        private Image monster;
        private int selectedRegion;
        private float nextTick;
        private ContinuousHuntOverviewDto overview;

        public static ContinuousHuntScreenPresenter Install()
        {
            ContinuousHuntScreenPresenter current = FindFirstObjectByType<ContinuousHuntScreenPresenter>(FindObjectsInactive.Include);
            if (current != null) return current;
            var root = new GameObject("CONTINUOUS_HUNT_SCREEN", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ContinuousHuntScreenPresenter));
            if (AppRoot.Instance != null) root.transform.SetParent(AppRoot.Instance.transform, false);
            else DontDestroyOnLoad(root);
            return root.GetComponent<ContinuousHuntScreenPresenter>();
        }

        private void Awake()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 850;
            CanvasScaler scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            RectTransform root = (RectTransform)transform; root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = root.offsetMax = Vector2.zero;
            BuildUi(); gameObject.SetActive(false);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (service == null)
            {
                if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized) { ShowToast("게임 정보를 준비하고 있습니다."); return; }
                service = AppRoot.Instance.Services.Get<ContinuousHuntGameService>();
                service.Changed += OnChanged;
            }
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            KingdomTycoon.Presentation.Regions.RegionMapScreenPresenter map = FindFirstObjectByType<KingdomTycoon.Presentation.Regions.RegionMapScreenPresenter>(FindObjectsInactive.Include);
            if (map != null && map.gameObject.activeSelf) map.gameObject.SetActive(false);
        }

        private void Update()
        {
            AnimateActors();
            if (service == null || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 1f;
            try { service.AdvanceTo(DateTimeOffset.UtcNow); Refresh(); }
            catch (Exception exception) { Debug.LogWarning(exception); ShowToast("사냥 기록을 갱신하지 못했습니다. 잠시 뒤 다시 시도합니다."); }
        }

        private void BuildUi()
        {
            Image background = Panel("배경", transform, new Color32(8, 15, 18, 255), Vector2.zero, Vector2.one);
            Image safe = Panel("안전영역", background.transform, new Color32(14, 25, 27, 255), new Vector2(.018f, .025f), new Vector2(.982f, .975f));
            safe.gameObject.AddComponent<KingdomTycoon.UI.SafeAreaFitter>();

            Text("표식", safe.transform, "상시 자동 사냥", 18, TextAlignmentOptions.Left, new Vector2(.025f, .925f), new Vector2(.25f, .975f), new Color32(116, 190, 169, 255));
            title = Text("제목", safe.transform, RegionNames[0], 40, TextAlignmentOptions.Left, new Vector2(.025f, .855f), new Vector2(.46f, .94f), new Color32(235, 198, 116, 255));
            summary = Text("요약", safe.transform, "배치 0명  ·  누적 수익 0골드", 21, TextAlignmentOptions.Right, new Vector2(.48f, .87f), new Vector2(.90f, .93f), new Color32(189, 205, 196, 255));
            Button close = MakeButton("닫기", safe.transform, "왕국으로", new Color32(83, 61, 42, 255), new Vector2(.905f, .865f), new Vector2(.985f, .955f)); close.onClick.AddListener(Close);

            Image regions = Panel("사냥터목록", safe.transform, new Color32(20, 34, 36, 255), new Vector2(.025f, .12f), new Vector2(.185f, .84f));
            Text("목록제목", regions.transform, "사냥터", 24, TextAlignmentOptions.Left, new Vector2(.08f, .90f), new Vector2(.92f, .98f), new Color32(225, 225, 211, 255));
            for (int i = 0; i < RegionIds.Length; i++)
            {
                int captured = i; float top = .86f - i * .16f;
                Button tab = MakeButton("사냥터_" + (i + 1), regions.transform, $"{i + 1:00}  {RegionNames[i]}", i == 0 ? new Color32(49, 111, 94, 255) : new Color32(43, 58, 57, 255), new Vector2(.06f, top - .12f), new Vector2(.94f, top));
                tab.onClick.AddListener(() => SelectRegion(captured));
            }

            Image field = Panel("전장", safe.transform, new Color32(22, 45, 42, 255), new Vector2(.195f, .12f), new Vector2(.715f, .84f));
            Image horizon = Panel("지면", field.transform, new Color32(49, 74, 56, 255), new Vector2(.02f, .04f), new Vector2(.98f, .42f));
            Text("전장제목", field.transform, "용병들이 각자 목표를 찾아 사냥합니다", 23, TextAlignmentOptions.Left, new Vector2(.035f, .91f), new Vector2(.78f, .98f), new Color32(217, 226, 203, 255));
            fieldHint = Text("전장안내", field.transform, "오른쪽 명단에서 용병을 배치하세요.", 24, TextAlignmentOptions.Center, new Vector2(.16f, .48f), new Vector2(.84f, .62f), new Color32(158, 180, 169, 255));
            monster = Panel("몬스터", horizon.transform, new Color32(152, 65, 55, 255), new Vector2(.72f, .34f), new Vector2(.80f, .62f));
            Text("몬스터표시", monster.transform, "짐승", 16, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white);
            for (int i = 0; i < 8; i++) actors.Add(CreateActor(horizon.transform, i));

            Image roster = Panel("용병명단", safe.transform, new Color32(20, 33, 37, 255), new Vector2(.725f, .12f), new Vector2(.985f, .84f));
            Text("명단제목", roster.transform, "용병 배치", 24, TextAlignmentOptions.Left, new Vector2(.05f, .91f), new Vector2(.95f, .985f), new Color32(225, 225, 211, 255));
            Text("명단안내", roster.transform, "여러 명을 같은 사냥터에 배치할 수 있습니다.", 16, TextAlignmentOptions.Left, new Vector2(.05f, .855f), new Vector2(.95f, .92f), new Color32(142, 174, 165, 255));
            for (int i = 0; i < 8; i++) rows.Add(CreateRow(roster.transform, i));

            toast = Text("알림", safe.transform, "배치한 용병은 귀환과 정비 후 자동으로 다시 출발합니다.", 19, TextAlignmentOptions.Center, new Vector2(.195f, .035f), new Vector2(.985f, .105f), new Color32(222, 196, 128, 255));
        }

        private MemberRow CreateRow(Transform parent, int index)
        {
            float top = .84f - index * .098f;
            Button button = MakeButton("용병_" + index, parent, "", new Color32(39, 55, 57, 255), new Vector2(.045f, top - .085f), new Vector2(.955f, top));
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(); label.alignment = TextAlignmentOptions.Left; label.margin = new Vector4(16, 2, 12, 2); label.fontSize = 17;
            var row = new MemberRow(button, label); button.onClick.AddListener(() => Toggle(row)); return row;
        }

        private ActorView CreateActor(Transform parent, int index)
        {
            var root = new GameObject("용병동작_" + index, typeof(RectTransform)); root.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)root.transform; rect.anchorMin = rect.anchorMax = new Vector2(.12f, .12f + index * .105f); rect.sizeDelta = new Vector2(132, 58);
            Image body = Panel("몸", root.transform, new Color32(67, 146, 126, 255), new Vector2(0, .18f), new Vector2(.34f, .9f));
            TMP_Text label = Text("이름", root.transform, "", 15, TextAlignmentOptions.Left, new Vector2(.36f, 0), new Vector2(1, 1), Color.white);
            root.SetActive(false); return new ActorView(root, rect, body, label, index);
        }

        private void SelectRegion(int index)
        {
            selectedRegion = Mathf.Clamp(index, 0, RegionIds.Length - 1); title.text = RegionNames[selectedRegion]; Refresh();
            ShowToast(selectedRegion == 0 ? "용병을 눌러 배치하거나 복귀시킬 수 있습니다." : "잠긴 사냥터는 지역 진행으로 개방할 수 있습니다.");
        }

        private void Toggle(MemberRow row)
        {
            if (service == null || row.Member == null) return;
            try
            {
                if (row.Member.AssignedRegionId == RegionIds[selectedRegion]) { service.Unassign(row.Member.InstanceId); ShowToast($"{row.Member.DisplayName}에게 귀환 명령을 내렸습니다."); }
                else { service.Assign(row.Member.InstanceId, RegionIds[selectedRegion]); ShowToast($"{row.Member.DisplayName}을(를) {RegionNames[selectedRegion]}에 배치했습니다."); }
                Refresh();
            }
            catch (Exception exception) { Debug.LogWarning(exception); ShowToast("아직 배치할 수 없는 사냥터입니다. 지역 개방 조건을 확인하세요."); }
        }

        private void Refresh()
        {
            if (service == null || !service.IsBootstrapped) return;
            overview = service.GetOverview();
            IReadOnlyList<ContinuousHuntMemberDto> members = overview.Members;
            int here = members.Count(value => value.AssignedRegionId == RegionIds[selectedRegion]);
            long gold = members.Where(value => value.AssignedRegionId == RegionIds[selectedRegion]).Sum(value => value.EarnedGold);
            summary.text = $"배치 {here}명  ·  누적 수익 {gold:N0}골드";
            fieldHint.gameObject.SetActive(here == 0);
            monster.gameObject.SetActive(here > 0);
            for (int i = 0; i < rows.Count; i++)
            {
                MemberRow row = rows[i]; row.Member = i < members.Count ? members[i] : null; row.Button.gameObject.SetActive(row.Member != null);
                if (row.Member == null) continue;
                bool selected = row.Member.AssignedRegionId == RegionIds[selectedRegion];
                string assignment = row.Member.AssignedRegionId == null ? "대기" : selected ? "이곳에서 사냥 중" : "다른 사냥터 배치";
                row.Label.text = $"<b>{row.Member.DisplayName}</b>  {Job(row.Member.JobId)}\n체력 {row.Member.CurrentHpBps / 100}%  ·  가방 {row.Member.BagFill}/{row.Member.BagCapacity}  ·  {assignment}";
                row.Button.image.color = selected ? new Color32(47, 111, 91, 255) : new Color32(39, 55, 57, 255);
            }
            ContinuousHuntMemberDto[] visible = members.Where(value => value.AssignedRegionId == RegionIds[selectedRegion]).Take(actors.Count).ToArray();
            for (int i = 0; i < actors.Count; i++)
            {
                ActorView actor = actors[i]; actor.Member = i < visible.Length ? visible[i] : null; actor.Root.SetActive(actor.Member != null);
                if (actor.Member != null) { actor.Label.text = $"{actor.Member.DisplayName}\n{State(actor.Member.State)}"; actor.Body.color = JobColor(actor.Member.JobId); }
            }
        }

        private void AnimateActors()
        {
            if (!gameObject.activeInHierarchy) return;
            float time = Time.unscaledTime;
            foreach (ActorView actor in actors)
            {
                if (actor.Member == null || !actor.Root.activeSelf) continue;
                float baseX = actor.Member.State switch { "TRAVEL_TO_REGION" => .12f + Mathf.PingPong(time * .10f, .28f), "FIND_TARGET" => .42f + Mathf.Sin(time * 1.5f + actor.Index) * .05f, "COMBAT" => .58f + Mathf.PingPong(time * .8f, .08f), "LOOT" => .68f, "RETURN_TOWN" => .72f - Mathf.PingPong(time * .16f, .6f), _ => .12f };
                actor.Rect.anchorMin = actor.Rect.anchorMax = new Vector2(baseX, .12f + actor.Index * .105f + Mathf.Sin(time * 2f + actor.Index) * .008f);
                float pulse = actor.Member.State == "COMBAT" ? 1f + Mathf.PingPong(time * 2.8f, .12f) : 1f; actor.Rect.localScale = Vector3.one * pulse;
            }
            if (monster != null && monster.gameObject.activeSelf) monster.rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(time * 4f) * .04f);
        }

        private void OnChanged(object sender, ContinuousHuntOverviewDto value) { overview = value; if (gameObject.activeInHierarchy) Refresh(); }
        private void ShowToast(string message) { if (toast != null) toast.text = message; }
        private void OnDestroy() { if (service != null) service.Changed -= OnChanged; }

        private static Image Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>(); image.color = color; SetRect(image.rectTransform, min, max); return image;
        }
        private static TMP_Text Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>(); text.text = value; text.fontSize = size; text.alignment = alignment; text.color = color; text.textWrappingMode = TextWrappingModes.Normal; SetRect(text.rectTransform, min, max); return text;
        }
        private static Button MakeButton(string name, Transform parent, string value, Color color, Vector2 min, Vector2 max)
        {
            Image image = Panel(name, parent, color, min, max); Button button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            TMP_Text label = Text("글자", image.transform, value, 19, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Color.white); label.raycastTarget = false; return button;
        }
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static string Job(string id) => id switch { "JOB_WARRIOR" => "전사", "JOB_ARCHER" => "궁수", "JOB_ROGUE" => "도적", "JOB_MAGE" => "마법사", "JOB_CLERIC" => "성직자", _ => "용병" };
        private static string State(string state) => state switch { "IDLE_TOWN" => "마을 대기", "TRAVEL_TO_REGION" => "이동 중", "FIND_TARGET" => "목표 탐색", "COMBAT" => "전투 중", "LOOT" => "전리품 수집", "CONTINUE_DECISION" => "상태 점검", "RETURN_TOWN" => "마을 귀환", "SELL_LOOT" => "전리품 판매", "HEAL" => "치료", "BUY_CONSUMABLES" => "물약 구매", "EVALUATE_EQUIPMENT" => "장비 점검", "BUY_EQUIPMENT" => "장비 구매", _ => "정비 중" };
        private static Color JobColor(string id) => id switch { "JOB_WARRIOR" => new Color32(174, 91, 65, 255), "JOB_ARCHER" => new Color32(72, 151, 102, 255), "JOB_ROGUE" => new Color32(126, 95, 162, 255), "JOB_MAGE" => new Color32(66, 117, 166, 255), "JOB_CLERIC" => new Color32(194, 164, 92, 255), _ => new Color32(93, 137, 128, 255) };

        private sealed class MemberRow
        {
            public MemberRow(Button button, TMP_Text label) { Button = button; Label = label; }
            public Button Button { get; }
            public TMP_Text Label { get; }
            public ContinuousHuntMemberDto Member { get; set; }
        }
        private sealed class ActorView
        {
            public ActorView(GameObject root, RectTransform rect, Image body, TMP_Text label, int index) { Root = root; Rect = rect; Body = body; Label = label; Index = index; }
            public GameObject Root { get; }
            public RectTransform Rect { get; }
            public Image Body { get; }
            public TMP_Text Label { get; }
            public int Index { get; }
            public ContinuousHuntMemberDto Member { get; set; }
        }
    }
}
