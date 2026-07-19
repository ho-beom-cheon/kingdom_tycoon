using System;
using System.Collections;
using System.Linq;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Application.Facilities.Queries;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Domain.Facilities;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Presentation.Kingdom.Views;
using TMPro;
using UnityEngine;

namespace KingdomTycoon.Presentation.Kingdom
{
    public sealed class KingdomScreenPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Text stageLabel;
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private FacilityWorldView[] facilityViews;
        [SerializeField] private FacilityDrawerView drawer;
        private FacilityGameService service;
        private GetKingdomScreenQuery screenQuery;
        private GetFacilityDetailQuery detailQuery;
        private FacilityOperationRequestFactory requests;
        private string selectedFacilityId;

        public bool IsBound { get; private set; }
        public int BoundFacilityCount => facilityViews?.Count(value => value != null && value.BoundState != null) ?? 0;

        public void Configure(TMP_Text stage, TMP_Text gold, TMP_Text status, FacilityWorldView[] views, FacilityDrawerView drawerView)
        {
            stageLabel = stage; goldLabel = gold; statusLabel = status; facilityViews = views; drawer = drawerView;
        }

        private IEnumerator Start()
        {
            while (AppRoot.Instance != null && !AppRoot.Instance.Services.Get<FacilityGameService>().IsBootstrapped) yield return null;
            if (AppRoot.Instance == null)
            {
                statusLabel.text = "Bootstrap 씬에서 실행해 주세요.";
                yield break;
            }
            service = AppRoot.Instance.Services.Get<FacilityGameService>();
            screenQuery = new GetKingdomScreenQuery(service);
            detailQuery = new GetFacilityDetailQuery(service);
            requests = new FacilityOperationRequestFactory(new SystemUuidV7Provider(), new FacilityRequestHasher());
            foreach (FacilityWorldView view in facilityViews) view.Selected += SelectFacility;
            drawer.PrimaryActionRequested += ExecutePrimaryAction;
            drawer.CloseRequested += drawer.Close;
            drawer.Close();
            Refresh();
            StartCoroutine(Tick());
        }

        private void OnDestroy()
        {
            if (facilityViews != null) foreach (FacilityWorldView view in facilityViews) if (view != null) view.Selected -= SelectFacility;
            if (drawer != null) drawer.PrimaryActionRequested -= ExecutePrimaryAction;
        }

        public void Refresh()
        {
            KingdomScreenDto dto = screenQuery.Execute();
            stageLabel.text = "왕국 " + dto.StageId;
            goldLabel.text = "골드 " + dto.KingdomGold.ToString("N0");
            statusLabel.text = dto.Recovered ? "저장 파일을 복구했습니다." : "콘텐츠 " + dto.ContentVersion + " · 저장 r" + dto.SaveRevision;
            foreach (FacilityWorldView view in facilityViews) view.Bind(dto.Facilities.Single(value => value.FacilityId == view.FacilityId));
            if (selectedFacilityId != null) drawer.Bind(detailQuery.Execute(selectedFacilityId));
            IsBound = true;
        }

        private void SelectFacility(string facilityId)
        {
            selectedFacilityId = facilityId;
            drawer.Open(detailQuery.Execute(facilityId));
        }

        private void ExecutePrimaryAction()
        {
            FacilityDetailDto detail = drawer.Current;
            if (detail == null) return;
            try
            {
                long revision = service.Revision;
                switch (detail.PrimaryAction)
                {
                    case "BUILD": service.StartBuild(requests.CreateBuild(revision, detail.World.FacilityId)); break;
                    case "UPGRADE": service.StartUpgrade(requests.CreateUpgrade(revision, detail.World.FacilityId, detail.NextLevel)); break;
                    case "CLAIM": service.Claim(requests.CreateClaim(revision, detail.World.FacilityId, detail.World.JobOperationId.Value)); break;
                    case "ASSIGN":
                        NpcCandidateDto candidate = detail.NpcCandidates.FirstOrDefault(value => !value.Assigned);
                        if (candidate == null) throw new FacilityCommandException("FACILITY_NPC_REQUIRED");
                        service.Assign(requests.CreateAssign(revision, detail.World.FacilityId, candidate.InstanceId));
                        break;
                    case "UNASSIGN":
                        NpcCandidateDto assigned = detail.NpcCandidates.FirstOrDefault(value => value.Assigned && value.Working);
                        if (assigned == null) throw new FacilityCommandException("FACILITY_NPC_REQUIRED");
                        service.Unassign(requests.CreateUnassign(revision, detail.World.FacilityId, assigned.InstanceId));
                        break;
                    default: return;
                }
                Refresh();
            }
            catch (FacilityCommandException exception)
            {
                drawer.ShowError(exception.ErrorCode);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                drawer.ShowError(exception.Message);
            }
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (isActiveAndEnabled && facilityViews.Any(value => value.BoundState is "BUILDING" or "UPGRADING")) Refresh();
            }
        }
    }
}
