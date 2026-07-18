using System;
using System.Linq;
using KingdomTycoon.Application.Combat;
using KingdomTycoon.Bootstrap;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Infrastructure.Save;
using UnityEngine;

namespace KingdomTycoon.Presentation.Combat
{
    public sealed class RegionCombatPresenter : MonoBehaviour
    {
        [SerializeField] private RegionCombatView view;
        private CombatGameService combat;
        private MercenaryRosterService roster;
        private Guid huntOperationId;
        private bool paused;
        private float accumulator;

        public void Configure(RegionCombatView target) => view = target;
        public string LastError { get; private set; }
        public bool IsStarted { get; private set; }

        private void Start()
        {
            IsStarted = true;
            view ??= GetComponentInChildren<RegionCombatView>(true);
            view.StartRequested += StartHunt;
            view.RecallRequested += Recall;
            view.PauseRequested += TogglePause;
            view.RetryRequested += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                if (AppRoot.Instance == null || !AppRoot.Instance.IsInitialized)
                {
                    view.SetState(RegionUiState.Loading);
                    return;
                }

                combat = AppRoot.Instance.Services.Get<CombatGameService>();
                roster = AppRoot.Instance.Services.Get<MercenaryRosterService>();
                LastError = null;
                combat.SnapshotChanged -= OnSnapshot;
                combat.SnapshotChanged += OnSnapshot;
                view.SetState(roster.GetRoster().TotalActive == 0 ? RegionUiState.Empty : RegionUiState.Content);
                view.Render(combat.GetSnapshot());
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                view.SetState(RegionUiState.Error, exception.Message);
            }
        }

        private void StartHunt()
        {
            try
            {
                LastError = null;
                string[] ids = roster.GetRoster().Cards
                    .Where(value => value.Active && value.AutonomyState == "IDLE_TOWN")
                    .Take(4)
                    .Select(value => value.InstanceId)
                    .ToArray();
                if (ids.Length == 0)
                {
                    view.SetState(RegionUiState.Empty);
                    return;
                }

                DateTimeOffset now = DateTimeOffset.UtcNow;
                Guid operationId = Guid.Parse(UuidV7.NewString(now));
                huntOperationId = Guid.Parse(UuidV7.NewString(now.AddMilliseconds(1)));
                var hasher = new CombatRequestHasher();
                var draft = new StartHuntCommand(operationId, huntOperationId, combat.Revision, null, "REGION_R01", ids.Select(Guid.Parse));
                combat.StartHunt(new StartHuntCommand(operationId, huntOperationId, combat.Revision, hasher.ComputeHash(draft), "REGION_R01", ids.Select(Guid.Parse)));
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                view.SetState(RegionUiState.Error, exception.Message);
            }
        }

        private void Recall()
        {
            try
            {
                LastError = null;
                var hasher = new CombatRequestHasher();
                var draft = new RecallHuntCommand(huntOperationId, combat.Revision, null);
                combat.RecallHunt(new RecallHuntCommand(huntOperationId, combat.Revision, hasher.ComputeHash(draft)));
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                view.SetState(RegionUiState.Error, exception.Message);
            }
        }

        private void TogglePause()
        {
            paused = !paused;
            view.SetPaused(paused);
        }

        private void Update()
        {
            if (paused || combat == null || !combat.GetSnapshot().Active) return;
            accumulator += Time.unscaledDeltaTime;
            int ticks = 0;
            while (accumulator >= 0.1f && ticks++ < 5)
            {
                accumulator -= 0.1f;
                combat.Tick();
            }

            if (ticks > 5) accumulator = 0f;
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause) view.SetState(RegionUiState.Offline);
            else Refresh();
        }

        private void OnSnapshot(object sender, HuntSnapshotDto snapshot)
        {
            view.SetState(RegionUiState.Content);
            view.Render(snapshot);
        }

        private void OnDestroy()
        {
            if (combat != null) combat.SnapshotChanged -= OnSnapshot;
            if (view == null) return;
            view.StartRequested -= StartHunt;
            view.RecallRequested -= Recall;
            view.PauseRequested -= TogglePause;
            view.RetryRequested -= Refresh;
        }
    }
}
