using System;
using System.Collections;
using System.Threading.Tasks;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Application.Mercenaries;
using KingdomTycoon.Application.Mercenaries.Commands;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Domain.Mercenaries;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Presentation.Mercenaries.Views;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace KingdomTycoon.Presentation.Mercenaries
{
    public sealed class MercenaryRosterPresenter : MonoBehaviour
    {
        [SerializeField] private Button navigationButton;
        [SerializeField] private MercenaryRosterView roster;
        [SerializeField] private MercenaryDetailDrawerView drawer;
        [SerializeField] private MercenaryFilterModalView filterModal;
        [SerializeField] private MercenaryOptionModalView sortModal;
        [SerializeField] private TMP_Text recoveryToast;
        [SerializeField] private TMP_Text debugLabel;
        private MercenaryRosterService service;
        private MercenaryOperationRequestFactory requests;
        private MercenaryPortraitAdapter portraits;
        private MercenaryRosterQueryDto query = new();
        private string selectedId;
        private bool busy;
        private bool initialized;
        private GameObject kingdomWorldCanvas;

        public bool IsBound { get; private set; }
        public bool IsReady => initialized;
        public int BoundCardCount => roster?.BoundCardCount ?? 0;
        public int PoolCount => roster?.PoolCount ?? 0;
        public bool IsOpen => roster != null && roster.gameObject.activeSelf;
        public MercenaryRosterUiState CurrentState => roster.CurrentState;
        public string OwnedCountText => roster.OwnedCountText;
        public string ActiveCountText => roster.ActiveCountText;
        public string SelectedMercenaryId => selectedId;
        public string DrawerBoundInstanceId => drawer.BoundInstanceId;
        public bool IsDrawerOpen => drawer.gameObject.activeSelf;
        public MercenaryDetailDrawerView DrawerView => drawer;
        public MercenaryRosterView RosterView => roster;
        public MercenaryFilterModalView FilterModal => filterModal;
        public MercenaryOptionModalView SortModal => sortModal;
        public MercenaryRosterQueryDto CurrentQuery => query;
        public event Action<string, string> SelectionChanged;
        public event Action<string, int> FilterChanged;

        public void Configure(Button nav, MercenaryRosterView rosterView, MercenaryDetailDrawerView drawerView,
            MercenaryFilterModalView filter, MercenaryOptionModalView sort, TMP_Text recovered, TMP_Text debug)
        {
            navigationButton = nav;
            roster = rosterView;
            drawer = drawerView;
            filterModal = filter;
            sortModal = sort;
            recoveryToast = recovered;
            debugLabel = debug;
        }

        private IEnumerator Start()
        {
            SubscribeViews();
            roster.gameObject.SetActive(false);
            drawer.CloseDrawer();
            filterModal.CloseModal();
            sortModal.CloseModal();
            while (AppRoot.Instance != null && !AppRoot.Instance.Services.Get<MercenaryRosterService>().IsBootstrapped) yield return null;
            if (AppRoot.Instance == null)
            {
                roster.gameObject.SetActive(true);
                roster.BindState(MercenaryRosterUiState.ERROR, "첫 화면에서 실행해 주세요.");
                yield break;
            }
            service = AppRoot.Instance.Services.Get<MercenaryRosterService>();
            requests = new MercenaryOperationRequestFactory(new SystemUuidV7Provider(), new MercenaryRequestHasher());
            portraits = new MercenaryPortraitAdapter();
            Task load = portraits.LoadAsync();
            while (!load.IsCompleted) yield return null;
            if (load.IsFaulted)
            {
                Debug.LogException(load.Exception?.GetBaseException());
                roster.gameObject.SetActive(true);
                roster.BindState(MercenaryRosterUiState.ERROR, "용병 초상화를 불러오지 못했습니다.");
                yield break;
            }
            service.ActivityChanged += ActivityChanged;
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            initialized = true;
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (filterModal.gameObject.activeSelf) filterModal.CloseModal();
            else if (sortModal.gameObject.activeSelf) sortModal.CloseModal();
            else if (drawer.gameObject.activeSelf) CloseDrawer();
            else if (IsOpen) CloseRoster();
        }

        private void OnDestroy()
        {
            if (service != null) service.ActivityChanged -= ActivityChanged;
            SceneManager.activeSceneChanged -= ActiveSceneChanged;
            portraits?.Dispose();
            UnsubscribeViews();
        }

        public void OpenRoster()
        {
            if (!initialized) return;
            roster.gameObject.SetActive(true);
            roster.BindState(MercenaryRosterUiState.LOADING, "용병 정보를 불러오는 중…");
            kingdomWorldCanvas = FindKingdomWorldCanvas();
            if (kingdomWorldCanvas != null) kingdomWorldCanvas.SetActive(false);
            if (AppRoot.Instance.Services.Get<FacilityGameService>().WasRecovered) StartCoroutine(ShowRecoveryToast());
            Refresh();
        }

        public void CloseRoster()
        {
            if (busy) return;
            filterModal.CloseModal();
            sortModal.CloseModal();
            CloseDrawer();
            roster.gameObject.SetActive(false);
            if (kingdomWorldCanvas != null) kingdomWorldCanvas.SetActive(true);
            kingdomWorldCanvas = null;
            query = new MercenaryRosterQueryDto();
            roster.SetSearchWithoutNotify(string.Empty);
            roster.BindFilterSummary(query);
            roster.ClearRoster();
            IsBound = false;
        }

        public void Refresh()
        {
            if (!initialized) return;
            try
            {
                MercenaryRosterResultDto dto = service.GetRoster(query);
                roster.BindRoster(dto, portraits.Resolve);
                roster.BindFilterSummary(query);
                roster.BindState(dto.TotalOwned == 0 ? MercenaryRosterUiState.EMPTY : MercenaryRosterUiState.CONTENT,
                    dto.TotalOwned == 0 ? "보유한 용병이 없습니다. 모집 화면에서 첫 용병을 고용해 보세요." : string.Empty);
                if (selectedId != null)
                {
                    bool stillVisible = false;
                    foreach (MercenaryCardDto card in dto.Cards) if (card.InstanceId == selectedId) { stillVisible = true; break; }
                    if (stillVisible) BindDrawer(); else CloseDrawer();
                }
                IsBound = true;
                FilterChanged?.Invoke(MercenaryRosterQueryDigest.Compute(query), dto.FilteredCount);
                if (debugLabel != null)
                {
                    debugLabel.gameObject.SetActive(Debug.isDebugBuild);
                    debugLabel.text = Debug.isDebugBuild
                        ? $"용병 명단 · 저장 {service.Revision}회 · {dto.FilteredCount}/{dto.TotalOwned}명"
                        : string.Empty;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                roster.BindState(MercenaryRosterUiState.ERROR, "용병 정보를 불러오지 못했습니다. 다시 시도해 주세요.");
                IsBound = false;
            }
        }

        public void BindStateForFixture(MercenaryRosterUiState state, string message) => roster.BindState(state, message);
        public void SelectForFixture(string instanceId) => SelectCard(instanceId);

        private void SubscribeViews()
        {
            navigationButton.onClick.AddListener(OpenRoster);
            roster.SearchChanged += Search;
            roster.FilterRequested += OpenFilter;
            roster.SortRequested += OpenSort;
            roster.CloseRequested += CloseRoster;
            roster.RetryRequested += Retry;
            roster.CardSelected += SelectCard;
            drawer.CloseRequested += CloseDrawer;
            drawer.ActiveToggleRequested += SetActive;
            filterModal.Applied += Filter;
            filterModal.CloseRequested += filterModal.CloseModal;
            sortModal.Selected += Sort;
            sortModal.CloseRequested += sortModal.CloseModal;
        }

        private void UnsubscribeViews()
        {
            if (navigationButton != null) navigationButton.onClick.RemoveListener(OpenRoster);
            if (roster != null)
            {
                roster.SearchChanged -= Search;
                roster.FilterRequested -= OpenFilter;
                roster.SortRequested -= OpenSort;
                roster.CloseRequested -= CloseRoster;
                roster.RetryRequested -= Retry;
                roster.CardSelected -= SelectCard;
            }
            if (drawer != null)
            {
                drawer.CloseRequested -= CloseDrawer;
                drawer.ActiveToggleRequested -= SetActive;
            }
            if (filterModal != null)
            {
                filterModal.Applied -= Filter;
                filterModal.CloseRequested -= filterModal.CloseModal;
            }
            if (sortModal != null)
            {
                sortModal.Selected -= Sort;
                sortModal.CloseRequested -= sortModal.CloseModal;
            }
        }

        private void Search(string value)
        {
            query = new MercenaryRosterQueryDto(value, query.JobIds, query.GradeIds, query.RankIds, query.StateIds,
                query.ActiveFilter, query.PromotionReadyOnly, query.InjuredOnly, query.SortId);
            Refresh();
        }

        private void Filter(MercenaryFilterSelection value)
        {
            query = new MercenaryRosterQueryDto(query.Search, value.JobIds, value.GradeIds, value.RankIds, value.StateIds,
                value.ActiveFilter, value.PromotionReadyOnly, value.InjuredOnly, query.SortId);
            filterModal.CloseModal();
            Refresh();
        }

        private void Sort(string value)
        {
            query = new MercenaryRosterQueryDto(query.Search, query.JobIds, query.GradeIds, query.RankIds, query.StateIds,
                query.ActiveFilter, query.PromotionReadyOnly, query.InjuredOnly, value);
            sortModal.CloseModal();
            Refresh();
        }

        private void OpenFilter()
        {
            sortModal.CloseModal();
            filterModal.Open(query);
        }

        private void OpenSort()
        {
            filterModal.CloseModal();
            sortModal.Open();
        }

        private void Retry()
        {
            try { service.Reload(); Refresh(); }
            catch (Exception exception) { Debug.LogException(exception); roster.BindState(MercenaryRosterUiState.ERROR, "용병 정보를 불러오지 못했습니다. 다시 시도해 주세요."); }
        }

        private void SelectCard(string id)
        {
            string previous = selectedId;
            selectedId = id;
            BindDrawer();
            roster.SetDrawerOpen(true);
            SelectionChanged?.Invoke(previous, selectedId);
        }

        private void BindDrawer()
        {
            MercenaryDetailDto detail = service.GetDetail(selectedId);
            MercenaryRosterResultDto summary = service.GetRoster();
            if (drawer.gameObject.activeSelf) drawer.Bind(detail, portraits.Resolve(detail.JobId));
            else drawer.Open(detail, portraits.Resolve(detail.JobId));
            (bool allowed, string reason) = ActivityAvailability(detail, summary);
            drawer.BindActivityAvailability(allowed, reason);
        }

        private void CloseDrawer()
        {
            string previous = selectedId;
            selectedId = null;
            drawer.CloseDrawer();
            roster.SetDrawerOpen(false);
            if (previous != null) SelectionChanged?.Invoke(previous, null);
        }

        private void SetActive(bool desired)
        {
            if (busy || selectedId == null) return;
            busy = true;
            drawer.SetBusy(true);
            try
            {
                SetMercenaryActiveCommand command = requests.CreateSetActive(service.Revision, Guid.Parse(selectedId), desired);
                new SetMercenaryActiveHandler(service).Handle(command);
                Refresh();
            }
            catch (MercenaryDomainException exception)
            {
                drawer.ShowError(UserMessage(exception.ErrorCode));
                if (exception.ErrorCode == "MERCENARY_SAVE_REVISION_CONFLICT")
                {
                    try { service.Reload(); BindDrawer(); }
                    catch (Exception reloadError) { Debug.LogException(reloadError); }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                drawer.ShowError("저장하지 못했습니다. 다시 시도해 주세요.");
            }
            finally
            {
                busy = false;
                drawer.SetBusy(false);
            }
        }

        private void ActivityChanged(object sender, MercenaryActivatedEventArgs args) { }

        private void ActiveSceneChanged(Scene previous, Scene next)
        {
            if (next.name == "Kingdom") return;
            if (IsOpen) CloseRoster();
        }

        private IEnumerator ShowRecoveryToast()
        {
            if (recoveryToast == null) yield break;
            recoveryToast.text = "저장 파일을 복구했습니다.";
            recoveryToast.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(3f);
            recoveryToast.gameObject.SetActive(false);
        }

        public void ShowRecoveryForFixture()
        {
            if (recoveryToast == null) return;
            recoveryToast.text = "저장 파일을 복구했습니다.";
            recoveryToast.gameObject.SetActive(true);
        }

        private static (bool Allowed, string Reason) ActivityAvailability(MercenaryDetailDto detail, MercenaryRosterResultDto summary)
        {
            bool townSafe = detail.AutonomyState is "IDLE_TOWN" or "PROMOTION_READY" or "INJURED";
            if (!townSafe || detail.CurrentRegionId != null || detail.PromotionStatus == "IN_REVIEW")
                return (false, "현재 상태에서는 활동 여부를 변경할 수 없습니다.");
            if (!detail.Active && summary.TotalActive >= summary.ActiveLimit)
                return (false, "활동 가능한 용병 수가 가득 찼습니다.");
            return (true, string.Empty);
        }

        private static string UserMessage(string code) => code switch
        {
            "MERCENARY_ACTIVE_LIMIT_REACHED" => "활동 가능한 용병 수가 가득 찼습니다.",
            "MERCENARY_ACTIVE_CHANGE_STATE_FORBIDDEN" => "현재 상태에서는 활동 여부를 변경할 수 없습니다.",
            "MERCENARY_SAVE_REVISION_CONFLICT" => "다른 변경 사항이 있어 다시 불러왔습니다.",
            "MERCENARY_NOT_FOUND" => "용병을 찾을 수 없습니다.",
            _ => "요청을 처리하지 못했습니다. 잠시 후 다시 시도해 주세요."
        };

        private static GameObject FindKingdomWorldCanvas()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform kingdomScreen = root.transform.Find("KingdomScreen");
                Transform world = kingdomScreen?.Find("WorldCanvas");
                if (world != null) return world.gameObject;
            }
            return null;
        }
    }
}
