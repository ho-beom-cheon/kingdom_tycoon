using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Infrastructure.Economy;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Combat
{
    public sealed class ContinuousHuntMemberDto
    {
        public ContinuousHuntMemberDto(string instanceId, string displayName, string jobId, string state, string assignedRegionId,
            int currentHpBps, int bagFill, int bagCapacity, long cyclesCompleted, long earnedGold)
        {
            InstanceId = instanceId; DisplayName = displayName; JobId = jobId; State = state; AssignedRegionId = assignedRegionId;
            CurrentHpBps = currentHpBps; BagFill = bagFill; BagCapacity = bagCapacity; CyclesCompleted = cyclesCompleted; EarnedGold = earnedGold;
        }

        public string InstanceId { get; }
        public string DisplayName { get; }
        public string JobId { get; }
        public string State { get; }
        public string AssignedRegionId { get; }
        public int CurrentHpBps { get; }
        public int BagFill { get; }
        public int BagCapacity { get; }
        public long CyclesCompleted { get; }
        public long EarnedGold { get; }
        public bool IsAssigned => AssignedRegionId != null;
    }

    public sealed class ContinuousHuntOverviewDto
    {
        public ContinuousHuntOverviewDto(long revision, IReadOnlyList<ContinuousHuntMemberDto> members)
        { Revision = revision; Members = members; }
        public long Revision { get; }
        public IReadOnlyList<ContinuousHuntMemberDto> Members { get; }
        public int AssignedCount => Members.Count(value => value.IsAssigned);
        public long EarnedGold => Members.Sum(value => value.EarnedGold);
    }

    /// <summary>
    /// General hunting is a persistent per-mercenary loop. P06 party combat remains available for
    /// compatibility and raids, but this service owns ordinary hunting-ground assignment.
    /// </summary>
    public sealed class ContinuousHuntGameService : IAppService
    {
        internal const int DecisionSeconds = 2;
        internal const int ReturnHpBps = 3000;
        internal const int ExperiencePerCycle = 12;
        internal const int ContributionPerCycle = 8;
        internal const int SaleGoldPerCycle = 18;
        private const int MaxCatchUpSteps = 180;

        private readonly ITrustedUtcClock clock;
        private SaveService save;
        private FacilityGameService game;
        private EconomyGameService economy;

        public ContinuousHuntGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 75;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<ContinuousHuntOverviewDto> Changed;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>();
            game = services.Get<FacilityGameService>();
            economy = services.Get<EconomyGameService>();
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped) throw new InvalidOperationException("CONTINUOUS_HUNT_SAVE_NOT_READY");
            JObject draft = game.Snapshot();
            if (Normalize(draft, clock.UtcNow)) Commit(draft, game.Revision, clock.UtcNow);
            IsBootstrapped = true;
            AdvanceTo(clock.UtcNow);
        }

        public ContinuousHuntOverviewDto GetOverview()
        {
            EnsureReady();
            return BuildOverview(game.Snapshot());
        }

        public static bool NormalizeDocument(JObject document, DateTimeOffset now)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            return Normalize(document, now);
        }

        public void Assign(string mercenaryInstanceId, string regionId)
        {
            EnsureReady();
            if (string.IsNullOrWhiteSpace(mercenaryInstanceId) || string.IsNullOrWhiteSpace(regionId))
                throw new InvalidOperationException("CONTINUOUS_HUNT_ARGUMENT_INVALID");
            JObject draft = game.Snapshot();
            if (!RegionUnlocked(draft, regionId)) throw new InvalidOperationException("CONTINUOUS_HUNT_REGION_LOCKED");
            JObject mercenary = FindMercenary(draft, mercenaryInstanceId);
            if (!mercenary.Value<bool>("active")) throw new InvalidOperationException("CONTINUOUS_HUNT_MERCENARY_INACTIVE");
            JObject autonomy = (JObject)mercenary["autonomy"]!;
            string now = FormatUtc(clock.UtcNow);
            autonomy["assignedRegionId"] = regionId;
            autonomy["autoResume"] = true;
            autonomy["state"] = "TRAVEL_TO_REGION";
            autonomy["reasonCode"] = "POLICY";
            autonomy["currentRegionId"] = regionId;
            autonomy["targetInstanceId"] = null;
            autonomy["stateStartedAtUtc"] = now;
            autonomy["nextDecisionAtUtc"] = FormatUtc(clock.UtcNow.AddSeconds(DecisionSeconds));
            Commit(draft, game.Revision, clock.UtcNow);
            Publish();
        }

        public void Unassign(string mercenaryInstanceId)
        {
            EnsureReady();
            JObject draft = game.Snapshot();
            JObject autonomy = (JObject)FindMercenary(draft, mercenaryInstanceId)["autonomy"]!;
            autonomy["assignedRegionId"] = null;
            autonomy["autoResume"] = false;
            autonomy["reasonCode"] = "PLAYER_RECALL";
            if (IsTownState(autonomy.Value<string>("state")))
            {
                SetState(autonomy, "IDLE_TOWN", "PLAYER_RECALL", clock.UtcNow);
                autonomy["currentRegionId"] = null;
            }
            Commit(draft, game.Revision, clock.UtcNow);
            Publish();
        }

        public int AdvanceTo(DateTimeOffset now)
        {
            EnsureReady();
            JObject draft = game.Snapshot();
            bool changed = Normalize(draft, now);
            int steps = 0;
            var returned = new List<string>();
            foreach (JObject mercenary in draft["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal))
            {
                JObject autonomy = (JObject)mercenary["autonomy"]!;
                int memberSteps = 0;
                while (memberSteps < MaxCatchUpSteps && ShouldAdvance(autonomy) && Due(autonomy, now))
                {
                    bool enteredTown = AdvanceMember(mercenary, autonomy, NextDecision(autonomy));
                    if (enteredTown) returned.Add(mercenary.Value<string>("instanceId"));
                    memberSteps++; steps++; changed = true;
                }
            }
            if (!changed) return 0;
            Commit(draft, game.Revision, now);
            Publish();

            // Existing store autonomy is best-effort. Closed facilities or empty stock never block hunting.
            if (returned.Count > 0)
            {
                try { economy.RunStoreAutonomyCycle(DeterministicCycleId(game.ActiveProfileId, now)); }
                catch (Exception) { }
            }
            return steps;
        }

        public void Shutdown()
        {
            Changed = null; economy = null; game = null; save = null; IsBootstrapped = false;
        }

        internal static bool Normalize(JObject document, DateTimeOffset now)
        {
            bool changed = false;
            foreach (JObject mercenary in document["payload"]!["mercenaries"]!.Children<JObject>())
            {
                JObject autonomy = (JObject)mercenary["autonomy"]!;
                bool oldHunting = autonomy.Value<string>("state") is "TRAVEL_TO_REGION" or "FIND_TARGET" or "COMBAT" or "LOOT" or "CONTINUE_DECISION" or "RETURN_TOWN";
                changed |= AddIfMissing(autonomy, "assignedRegionId", oldHunting ? autonomy["currentRegionId"]?.DeepClone() ?? JValue.CreateNull() : JValue.CreateNull());
                changed |= AddIfMissing(autonomy, "autoResume", true);
                changed |= AddIfMissing(autonomy, "currentHpBps", 10000);
                changed |= AddIfMissing(autonomy, "bagFill", 0);
                changed |= AddIfMissing(autonomy, "bagCapacity", 6);
                changed |= AddIfMissing(autonomy, "pendingSaleGold", 0L);
                changed |= AddIfMissing(autonomy, "cyclesCompleted", 0L);
                changed |= AddIfMissing(autonomy, "earnedGold", 0L);
                int capacity = Math.Max(1, autonomy.Value<int>("bagCapacity"));
                if (autonomy.Value<int>("bagFill") > capacity) { autonomy["bagFill"] = capacity; changed = true; }
                if (autonomy.Value<int>("currentHpBps") is < 0 or > 10000) { autonomy["currentHpBps"] = 10000; changed = true; }
                if (!KnownState(autonomy.Value<string>("state")))
                {
                    autonomy["assignedRegionId"] = null;
                    autonomy["currentRegionId"] = null;
                    SetState(autonomy, "IDLE_TOWN", "NONE", now);
                    changed = true;
                }
            }
            return changed;
        }

        private static bool AdvanceMember(JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            string state = autonomy.Value<string>("state");
            string assigned = autonomy["assignedRegionId"]!.Type == JTokenType.Null ? null : autonomy.Value<string>("assignedRegionId");
            switch (state)
            {
                case "IDLE_TOWN":
                case "BUY_EQUIPMENT":
                    if (assigned != null && autonomy.Value<bool>("autoResume"))
                    {
                        autonomy["currentRegionId"] = assigned;
                        SetState(autonomy, "TRAVEL_TO_REGION", "POLICY", at);
                    }
                    else { autonomy["currentRegionId"] = null; SetState(autonomy, "IDLE_TOWN", "NONE", at); }
                    return false;
                case "TRAVEL_TO_REGION": SetState(autonomy, "FIND_TARGET", "TARGET_FOUND", at); return false;
                case "FIND_TARGET": SetState(autonomy, "COMBAT", "TARGET_FOUND", at); return false;
                case "COMBAT":
                    int damage = 900 + (int)(autonomy.Value<long>("cyclesCompleted") % 5L) * 200;
                    autonomy["currentHpBps"] = Math.Max(0, autonomy.Value<int>("currentHpBps") - damage);
                    SetState(autonomy, "LOOT", "LOOT_COMPLETE", at);
                    return false;
                case "LOOT":
                    autonomy["bagFill"] = Math.Min(autonomy.Value<int>("bagCapacity"), checked(autonomy.Value<int>("bagFill") + 1));
                    autonomy["pendingSaleGold"] = checked(autonomy.Value<long>("pendingSaleGold") + SaleGoldPerCycle);
                    autonomy["cyclesCompleted"] = checked(autonomy.Value<long>("cyclesCompleted") + 1);
                    mercenary["exp"] = checked(mercenary.Value<long>("exp") + ExperiencePerCycle);
                    mercenary["contribution"] = checked(mercenary.Value<long>("contribution") + ContributionPerCycle);
                    mercenary["records"]!["killCount"] = checked(mercenary["records"]!.Value<long>("killCount") + 1);
                    mercenary["records"]!["huntCount"] = checked(mercenary["records"]!.Value<long>("huntCount") + 1);
                    mercenary["records"]!["itemsCollected"] = checked(mercenary["records"]!.Value<long>("itemsCollected") + 1);
                    SetState(autonomy, "CONTINUE_DECISION", "LOOT_COMPLETE", at);
                    return false;
                case "CONTINUE_DECISION":
                    if (assigned == null || autonomy.Value<int>("currentHpBps") <= ReturnHpBps || autonomy.Value<int>("bagFill") >= autonomy.Value<int>("bagCapacity"))
                        SetState(autonomy, "RETURN_TOWN", assigned == null ? "PLAYER_RECALL" : autonomy.Value<int>("currentHpBps") <= ReturnHpBps ? "HP_LOW" : "INVENTORY_FULL", at);
                    else SetState(autonomy, "FIND_TARGET", "POLICY", at);
                    return false;
                case "RETURN_TOWN": autonomy["currentRegionId"] = null; SetState(autonomy, "SELL_LOOT", "RETURNED_TO_STORE", at); return true;
                case "SELL_LOOT":
                    long sale = autonomy.Value<long>("pendingSaleGold");
                    mercenary["personalGold"] = checked(mercenary.Value<long>("personalGold") + sale);
                    autonomy["earnedGold"] = checked(autonomy.Value<long>("earnedGold") + sale);
                    autonomy["pendingSaleGold"] = 0L; autonomy["bagFill"] = 0;
                    SetState(autonomy, "HEAL", "AUTO_SELL_ELIGIBLE", at); return false;
                case "HEAL":
                    ConsumePotion(mercenary); autonomy["currentHpBps"] = 10000;
                    SetState(autonomy, "BUY_CONSUMABLES", "POTION_TARGET_LOW", at); return false;
                case "BUY_CONSUMABLES": SetState(autonomy, "EVALUATE_EQUIPMENT", "POLICY", at); return false;
                case "EVALUATE_EQUIPMENT": SetState(autonomy, "BUY_EQUIPMENT", "EQUIPMENT_UPGRADE", at); return false;
                case "INJURED": SetState(autonomy, "HEAL", "HP_LOW", at); return true;
                default: SetState(autonomy, "IDLE_TOWN", "NONE", at); return false;
            }
        }

        private void Commit(JObject draft, long expectedRevision, DateTimeOffset now)
        {
            SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, expectedRevision, now);
            if (!result.Success) throw new InvalidOperationException((result.ErrorCode ?? "CONTINUOUS_HUNT_SAVE_FAILED") + ": " + string.Join(" | ", result.Report.Issues.Select(value => value.ToString())));
            game.SynchronizeCommittedDocument(result.Document);
        }

        private void Publish() => Changed?.Invoke(this, BuildOverview(game.Snapshot()));
        private static ContinuousHuntOverviewDto BuildOverview(JObject document) => new(document.Value<long>("revision"), document["payload"]!["mercenaries"]!.Children<JObject>()
            .Where(value => value.Value<bool>("active"))
            .OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal)
            .Select(value =>
            {
                JObject autonomy = (JObject)value["autonomy"]!;
                return new ContinuousHuntMemberDto(value.Value<string>("instanceId"), value.Value<string>("displayName"), value.Value<string>("jobId"),
                    autonomy.Value<string>("state"), autonomy["assignedRegionId"]!.Type == JTokenType.Null ? null : autonomy.Value<string>("assignedRegionId"),
                    autonomy.Value<int>("currentHpBps"), autonomy.Value<int>("bagFill"), autonomy.Value<int>("bagCapacity"), autonomy.Value<long>("cyclesCompleted"), autonomy.Value<long>("earnedGold"));
            }).ToArray());

        private static JObject FindMercenary(JObject document, string id) => document["payload"]!["mercenaries"]!.Children<JObject>()
            .SingleOrDefault(value => value.Value<string>("instanceId") == id) ?? throw new InvalidOperationException("CONTINUOUS_HUNT_MERCENARY_NOT_FOUND");
        private static bool RegionUnlocked(JObject document, string regionId) => document["payload"]!["regions"]!["progress"]!.Children<JObject>()
            .Any(value => value.Value<string>("regionId") == regionId && value.Value<bool>("unlocked"));
        private static bool AddIfMissing(JObject target, string property, object value)
        { if (target[property] != null) return false; target[property] = value is JToken token ? token : JToken.FromObject(value); return true; }
        private static bool KnownState(string state) => state is "IDLE_TOWN" or "PREPARE" or "TRAVEL_TO_REGION" or "FIND_TARGET" or "COMBAT" or "LOOT" or "CONTINUE_DECISION" or "RETURN_TOWN" or "SELL_LOOT" or "HEAL" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED" or "RAID_READY";
        private static bool IsTownState(string state) => state is "IDLE_TOWN" or "SELL_LOOT" or "HEAL" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED" or "RAID_READY";
        private static bool ShouldAdvance(JObject autonomy) => autonomy["assignedRegionId"]!.Type != JTokenType.Null || autonomy.Value<string>("state") is not "IDLE_TOWN";
        private static DateTimeOffset NextDecision(JObject autonomy) => DateTimeOffset.TryParse(autonomy.Value<string>("nextDecisionAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset value) ? value : DateTimeOffset.MinValue;
        private static bool Due(JObject autonomy, DateTimeOffset now) => NextDecision(autonomy) <= now;
        private static void SetState(JObject autonomy, string state, string reason, DateTimeOffset at)
        { autonomy["state"] = state; autonomy["reasonCode"] = reason; autonomy["stateStartedAtUtc"] = FormatUtc(at); autonomy["nextDecisionAtUtc"] = FormatUtc(at.AddSeconds(DecisionSeconds)); }
        private static void ConsumePotion(JObject mercenary)
        {
            JObject line = mercenary["potions"]!.Children<JObject>().FirstOrDefault(value => value.Value<string>("potionId") == "POT_HEAL_SMALL" && value.Value<long>("quantity") > 0);
            if (line == null) return;
            long remaining = line.Value<long>("quantity") - 1;
            if (remaining == 0) line.Remove();
            else line["quantity"] = remaining;
        }
        private static Guid DeterministicCycleId(string profileId, DateTimeOffset at)
        {
            byte[] bytes = System.Security.Cryptography.SHA256.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes($"CONTINUOUS_HUNT:{profileId}:{at.ToUniversalTime():yyyyMMddHHmm}"));
            byte[] id = bytes.Take(16).ToArray(); id[6] = (byte)((id[6] & 0x0f) | 0x70); id[8] = (byte)((id[8] & 0x3f) | 0x80); return new Guid(id);
        }
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private void EnsureReady() { if (!IsBootstrapped || game == null || save == null) throw new InvalidOperationException("CONTINUOUS_HUNT_NOT_READY"); }
    }
}
