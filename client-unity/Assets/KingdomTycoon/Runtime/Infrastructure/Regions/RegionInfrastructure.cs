using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Regions;
using KingdomTycoon.Domain.Regions;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Regions
{
    public sealed class P12RegionCatalog
    {
        public sealed class RegionRule
        {
            public string Id; public string NameKey; public int Order; public int Tier; public string MinimumRankId;
            public int RecommendedPower; public int MaxActive; public string EnvironmentTag;
        }

        public sealed class UnlockRule
        {
            public string RegionId; public string PreviousRegionId; public int PreviousProgressRequired; public string KingdomStageId;
            public string EliteSourceRegionId; public long EliteKillRequired; public string FacilityId; public int FacilityLevelRequired;
            public string RaidId; public long RaidClearRequired;
        }

        public sealed class AccessRule
        {
            public string RegionId; public int PartyMin; public int PartyMax; public bool DefaultAllowed; public bool ManualToggleAllowed; public bool RequireTownSafe;
        }

        public sealed class ProgressRule
        {
            public string RegionId; public int VictoryProgress; public int EliteBonusProgress; public int ProgressCap;
        }

        public sealed class HuntRule
        {
            public string RegionId; public int RecommendedPartySize; public int RecommendedPotions; public int ReserveSlots;
        }

        private readonly Dictionary<string, RegionRule> regions;
        private readonly Dictionary<string, UnlockRule> unlocks;
        private readonly Dictionary<string, AccessRule> access;
        private readonly Dictionary<string, ProgressRule> progress;
        private readonly Dictionary<string, HuntRule> hunts;
        private readonly Dictionary<string, int> rankOrder;

        public P12RegionCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion))
                throw new RegionDomainException("P12_CONTENT_VERSION_UNSUPPORTED");
            regions = catalog.GetTable("regions.csv").Rows.Where(Enabled).Select(row => new RegionRule
            {
                Id = row["region_id"], NameKey = row["name_text_key"], Order = Int(row, "order"), Tier = Int(row, "tier"),
                MinimumRankId = row["min_rank_id"], RecommendedPower = Int(row, "recommended_power"),
                MaxActive = Int(row, "max_active"), EnvironmentTag = row["environment_tag"]
            }).ToDictionary(value => value.Id, StringComparer.Ordinal);
            unlocks = catalog.GetTable("region_unlock_rules.csv").Rows.Where(Enabled).Select(row => new UnlockRule
            {
                RegionId = row["region_id"], PreviousRegionId = Null(row["previous_region_id"]), PreviousProgressRequired = Int(row, "previous_progress_required"),
                KingdomStageId = row["kingdom_stage_id"], EliteSourceRegionId = Null(row["elite_source_region_id"]), EliteKillRequired = Long(row, "elite_kill_required"),
                FacilityId = Null(row["facility_id"]), FacilityLevelRequired = Int(row, "facility_level_required"),
                RaidId = Null(row["raid_id"]), RaidClearRequired = Long(row, "raid_clear_required")
            }).ToDictionary(value => value.RegionId, StringComparer.Ordinal);
            access = catalog.GetTable("region_access_policy_rules.csv").Rows.Where(Enabled).Select(row => new AccessRule
            {
                RegionId = row["region_id"], PartyMin = Int(row, "party_min"), PartyMax = Int(row, "party_max"),
                DefaultAllowed = Bool(row, "default_allowed_on_unlock"), ManualToggleAllowed = Bool(row, "manual_toggle_allowed"),
                RequireTownSafe = Bool(row, "require_town_safe")
            }).ToDictionary(value => value.RegionId, StringComparer.Ordinal);
            progress = catalog.GetTable("region_progress_rules.csv").Rows.Where(Enabled).Select(row => new ProgressRule
            {
                RegionId = row["region_id"], VictoryProgress = Int(row, "victory_progress"),
                EliteBonusProgress = Int(row, "elite_bonus_progress"), ProgressCap = Int(row, "progress_cap")
            }).ToDictionary(value => value.RegionId, StringComparer.Ordinal);
            hunts = catalog.GetTable("region_hunt_policy_rules.csv").Rows.Where(Enabled).Select(row => new HuntRule
            {
                RegionId = row["region_id"], RecommendedPartySize = Int(row, "recommended_party_size"),
                RecommendedPotions = Int(row, "recommended_potion_quantity"), ReserveSlots = Int(row, "inventory_reserve_slots")
            }).ToDictionary(value => value.RegionId, StringComparer.Ordinal);
            rankOrder = catalog.GetTable("mercenary_ranks.csv").Rows.Where(Enabled).ToDictionary(row => row["rank_id"], row => Int(row, "order"), StringComparer.Ordinal);
            string[] ids = Enumerable.Range(1, 5).Select(value => $"REGION_R0{value}").ToArray();
            if (regions.Count != 5 || unlocks.Count != 5 || access.Count != 5 || progress.Count != 5 || hunts.Count != 5 ||
                !regions.Keys.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(ids) || regions.Values.Select(value => value.Order).OrderBy(value => value).SequenceEqual(Enumerable.Range(1, 5)) == false)
                throw new RegionDomainException("P12_CONTENT_INVALID");
            foreach (string id in ids)
                if (!unlocks.ContainsKey(id) || !access.ContainsKey(id) || !progress.ContainsKey(id) || !hunts.ContainsKey(id) || !rankOrder.ContainsKey(regions[id].MinimumRankId))
                    throw new RegionDomainException("P12_CONTENT_INVALID");
        }

        public IReadOnlyList<RegionRule> Regions => regions.Values.OrderBy(value => value.Order).ToArray();
        public RegionRule Region(string id) => Get(regions, id);
        public UnlockRule Unlock(string id) => Get(unlocks, id);
        public AccessRule Access(string id) => Get(access, id);
        public ProgressRule Progress(string id) => Get(progress, id);
        public HuntRule Hunt(string id) => Get(hunts, id);
        public int RankOrder(string id) => rankOrder.TryGetValue(id ?? string.Empty, out int value) ? value : throw new RegionDomainException("P12_RANK_INVALID");
        private static T Get<T>(IReadOnlyDictionary<string, T> source, string id) => source.TryGetValue(id ?? string.Empty, out T value) ? value : throw new RegionDomainException("P12_REGION_NOT_FOUND");
        private static string Null(string value) => string.IsNullOrEmpty(value) ? null : value;
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static bool Bool(IReadOnlyDictionary<string, string> row, string key) => row[key] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public sealed class RegionRequestHasher
    {
        public string Compute(SetRegionAccessPolicyCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class RegionGameService : IAppService, IRegionDeploymentPolicy
    {
        private static readonly IReadOnlyDictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["REGION_R01"] = "왕국 외곽 초원", ["REGION_R02"] = "안개 낀 고대림", ["REGION_R03"] = "폐광 심층부",
            ["REGION_R04"] = "독안개 습지", ["REGION_R05"] = "서리 왕국 유적"
        };
        private static readonly IReadOnlyDictionary<string, int> StageOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        { ["KINGDOM_1"] = 1, ["KINGDOM_2"] = 2, ["KINGDOM_3"] = 3, ["KINGDOM_4"] = 4, ["KINGDOM_5"] = 5 };
        private readonly ITrustedUtcClock clock;
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private P12RegionCatalog catalog;

        public RegionGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 72;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<RegionOverviewDto> Changed;

        public void Initialize(ServiceRegistry services)
        { save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>(); }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || game.CurrentDocument.Value<string>("contentVersion") is not (CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion))
                throw new RegionDomainException("P12_CONTENT_VERSION_UNSUPPORTED");
            catalog = new P12RegionCatalog(content.Catalog);
            IsBootstrapped = true;
            NormalizeUnlocks();
        }

        public RegionOverviewDto GetOverview()
        {
            EnsureReady(); NormalizeUnlocks(); JObject document = game.Snapshot(); string stage = document["payload"]!["kingdom"]!.Value<string>("kingdomStageId");
            var mercenaries = document["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal)
                .Select(value => new RegionMercenaryDto(value.Value<string>("instanceId"), value.Value<string>("displayName"), value.Value<string>("jobId"),
                    value.Value<string>("rankId"), catalog.RankOrder(value.Value<string>("rankId")), value.Value<bool>("active"),
                    value["autonomy"]!.Value<string>("state"), value["autonomy"]!["currentRegionId"]!.Type == JTokenType.Null ? null : value["autonomy"]!.Value<string>("currentRegionId"))).ToArray();
            var rows = new List<RegionSummaryDto>();
            foreach (P12RegionCatalog.RegionRule rule in catalog.Regions)
            {
                JObject state = Progress(document, rule.Id); JObject policy = Policy(document, rule.Id); P12RegionCatalog.HuntRule hunt = catalog.Hunt(rule.Id);
                IReadOnlyList<RegionRequirementDto> requirements = Requirements(document, catalog.Unlock(rule.Id));
                rows.Add(new RegionSummaryDto(rule.Id, Names[rule.Id], rule.Order, rule.Tier, rule.EnvironmentTag, rule.MinimumRankId,
                    catalog.RankOrder(rule.MinimumRankId), rule.RecommendedPower, state.Value<bool>("unlocked"), policy.Value<bool>("allowed"),
                    state.Value<int>("progressPercent"), state.Value<long>("huntCount"), state.Value<long>("eliteKillCount"),
                    state["highestRankReachedId"]!.Type == JTokenType.Null ? null : state.Value<string>("highestRankReachedId"),
                    mercenaries.Count(value => value.CurrentRegionId == rule.Id), hunt.RecommendedPartySize, hunt.RecommendedPotions,
                    hunt.ReserveSlots, requirements.All(value => value.Met) ? string.Empty : requirements.First(value => !value.Met).Label, requirements));
            }
            string last = document["payload"]!["regions"]!["events"]!.Children<JObject>().LastOrDefault()?.Value<string>("resultCode") ?? "P12_READY";
            return new RegionOverviewDto(document.Value<long>("revision"), stage, rows.Count(value => value.Unlocked), rows, mercenaries, last);
        }

        public RegionPolicyOperationResult SetAccessPolicy(SetRegionAccessPolicyCommand command)
        {
            EnsureReady(); if (command == null) throw new ArgumentNullException(nameof(command));
            VerifyHash(command.RequestHash, new RegionRequestHasher().Compute(command)); JObject current = game.Snapshot();
            JObject replay = FindJournal(current, command.OperationId);
            if (replay != null)
            {
                VerifyHash(command.RequestHash, replay.Value<string>("requestHash"));
                return new RegionPolicyOperationResult(command.OperationId, current.Value<long>("revision"), current.Value<long>("revision"), true, "P12_POLICY_REPLAYED", replay.Value<string>("resultDigest"));
            }
            Require(command.ExpectedRevision == current.Value<long>("revision"), "P12_SAVE_REVISION_CONFLICT");
            JObject region = Progress(current, command.RegionId); Require(region.Value<bool>("unlocked"), "P12_REGION_LOCKED");
            P12RegionCatalog.AccessRule access = catalog.Access(command.RegionId); Require(access.ManualToggleAllowed, "P12_POLICY_TOGGLE_FORBIDDEN");
            if (!command.Allowed)
                Require(!current["payload"]!["mercenaries"]!.Children<JObject>().Any(value => value["autonomy"]!.Value<string>("currentRegionId") == command.RegionId), "P12_REGION_BUSY");
            JObject policy = Policy(current, command.RegionId); bool beforeAllowed = policy.Value<bool>("allowed"); long before = current.Value<long>("revision");
            if (beforeAllowed != command.Allowed)
            {
                policy["allowed"] = command.Allowed;
                AppendEvent(current, "AccessPolicyChanged", command.RegionId, Array.Empty<string>(), null, null, beforeAllowed, command.Allowed, "P12_POLICY_UPDATED", clock.UtcNow);
            }
            string digest = Rfc8785Canonicalizer.ComputeSha256(new JObject
            {
                ["operationId"] = command.OperationId.ToString("D"), ["revisionBefore"] = before, ["revisionAfter"] = before + 1,
                ["regionId"] = command.RegionId, ["allowed"] = command.Allowed, ["resultCode"] = beforeAllowed == command.Allowed ? "P12_POLICY_UNCHANGED" : "P12_POLICY_UPDATED"
            });
            AppendJournal(current, command, digest, clock.UtcNow); Commit(current, before);
            var result = new RegionPolicyOperationResult(command.OperationId, before, game.Revision, false, beforeAllowed == command.Allowed ? "P12_POLICY_UNCHANGED" : "P12_POLICY_UPDATED", digest);
            Changed?.Invoke(this, GetOverview()); return result;
        }

        public void ValidateDeployment(JObject document, string regionId, IEnumerable<string> partyMercenaryInstanceIds)
        {
            EnsureReady(); if (document == null) throw new ArgumentNullException(nameof(document));
            JObject region = Progress(document, regionId); Require(region.Value<bool>("unlocked"), "P12_REGION_LOCKED");
            Require(Policy(document, regionId).Value<bool>("allowed"), "P12_REGION_ACCESS_DENIED");
            P12RegionCatalog.AccessRule access = catalog.Access(regionId); string[] party = partyMercenaryInstanceIds?.ToArray() ?? Array.Empty<string>();
            Require(party.Length >= access.PartyMin && party.Length <= access.PartyMax, "P12_PARTY_SIZE_INVALID");
            Require(party.Distinct(StringComparer.Ordinal).Count() == party.Length, "P12_PARTY_DUPLICATE");
            var mercenaries = document["payload"]!["mercenaries"]!.Children<JObject>().ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal);
            int minimumRank = catalog.RankOrder(catalog.Region(regionId).MinimumRankId);
            foreach (string id in party)
            {
                Require(mercenaries.TryGetValue(id, out JObject mercenary), "P12_PARTY_MEMBER_NOT_FOUND");
                Require(mercenary.Value<bool>("active"), "P12_PARTY_MEMBER_INACTIVE");
                Require(catalog.RankOrder(mercenary.Value<string>("rankId")) >= minimumRank, "P12_PARTY_RANK_TOO_LOW");
                Require(mercenary["promotion"]!.Value<string>("status") != "IN_REVIEW", "P12_PARTY_PROMOTION_BUSY");
                if (access.RequireTownSafe)
                    Require(mercenary["autonomy"]!.Value<string>("state") == "IDLE_TOWN" && mercenary["autonomy"]!["currentRegionId"]!.Type == JTokenType.Null, "P12_PARTY_NOT_TOWN_SAFE");
            }
        }

        public void ApplyHuntSettlement(JObject draft, string regionId, IEnumerable<string> partyMercenaryInstanceIds, int eliteKills, bool victory, DateTimeOffset now)
        {
            EnsureReady(); if (!victory) return; P12RegionCatalog.ProgressRule rule = catalog.Progress(regionId); JObject state = Progress(draft, regionId);
            int before = state.Value<int>("progressPercent"); int after = RegionProgressMath.Apply(before, rule.VictoryProgress, rule.EliteBonusProgress, eliteKills, rule.ProgressCap);
            state["progressPercent"] = after; state["huntCount"] = checked(state.Value<long>("huntCount") + 1);
            state["eliteKillCount"] = checked(state.Value<long>("eliteKillCount") + eliteKills); state["lastVisitedAtUtc"] = FormatUtc(now);
            string[] party = partyMercenaryInstanceIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string highest = draft["payload"]!["mercenaries"]!.Children<JObject>().Where(value => party.Contains(value.Value<string>("instanceId"), StringComparer.Ordinal))
                .Select(value => value.Value<string>("rankId")).OrderByDescending(catalog.RankOrder).FirstOrDefault();
            string existing = state["highestRankReachedId"]!.Type == JTokenType.Null ? null : state.Value<string>("highestRankReachedId");
            if (highest != null && (existing == null || catalog.RankOrder(highest) > catalog.RankOrder(existing))) state["highestRankReachedId"] = highest;
            AppendEvent(draft, "HuntSettled", regionId, party, before, after, null, null, "P12_HUNT_SETTLED", now);
            NormalizeDraft(draft, now);
        }

        public bool NormalizeUnlocks()
        {
            EnsureReady(); JObject draft = game.Snapshot(); long before = draft.Value<long>("revision"); bool changed = NormalizeDraft(draft, clock.UtcNow);
            if (changed) { Commit(draft, before); Changed?.Invoke(this, GetOverview()); }
            return changed;
        }

        public bool NormalizeUnlocksInDraft(JObject draft, DateTimeOffset now)
        {
            EnsureReady();
            return NormalizeDraft(draft ?? throw new ArgumentNullException(nameof(draft)), now);
        }

        public void Shutdown()
        { Changed = null; catalog = null; game = null; content = null; save = null; IsBootstrapped = false; }

        private bool NormalizeDraft(JObject document, DateTimeOffset now)
        {
            bool changed = false;
            foreach (P12RegionCatalog.RegionRule rule in catalog.Regions)
            {
                JObject state = Progress(document, rule.Id); if (state.Value<bool>("unlocked")) continue;
                if (!Requirements(document, catalog.Unlock(rule.Id)).All(value => value.Met)) continue;
                state["unlocked"] = true; state["firstUnlockedAtUtc"] = FormatUtc(now); Policy(document, rule.Id)["allowed"] = catalog.Access(rule.Id).DefaultAllowed;
                AppendEvent(document, "RegionUnlocked", rule.Id, Array.Empty<string>(), null, null, null, catalog.Access(rule.Id).DefaultAllowed, "P12_REGION_UNLOCKED", now); changed = true;
            }
            return changed;
        }

        private IReadOnlyList<RegionRequirementDto> Requirements(JObject document, P12RegionCatalog.UnlockRule rule)
        {
            var result = new List<RegionRequirementDto>();
            int currentStage = StageOrder.GetValueOrDefault(document["payload"]!["kingdom"]!.Value<string>("kingdomStageId"));
            int requiredStage = StageOrder.GetValueOrDefault(rule.KingdomStageId);
            result.Add(new RegionRequirementDto("KINGDOM_STAGE", $"왕국 단계 {requiredStage}", currentStage, requiredStage, currentStage >= requiredStage));
            if (rule.PreviousRegionId != null)
            {
                int current = Progress(document, rule.PreviousRegionId).Value<int>("progressPercent");
                result.Add(new RegionRequirementDto("PREVIOUS_PROGRESS", $"이전 지역 진행도 {rule.PreviousProgressRequired}%", current, rule.PreviousProgressRequired, current >= rule.PreviousProgressRequired));
            }
            if (rule.EliteSourceRegionId != null)
            {
                long current = Progress(document, rule.EliteSourceRegionId).Value<long>("eliteKillCount");
                result.Add(new RegionRequirementDto("ELITE_KILL", "정예 몬스터 처치", current, rule.EliteKillRequired, current >= rule.EliteKillRequired));
            }
            if (rule.FacilityId != null)
            {
                JObject facility = document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == rule.FacilityId);
                int current = facility.Value<string>("state") == "ACTIVE" ? facility.Value<int>("level") : 0;
                result.Add(new RegionRequirementDto("FACILITY_LEVEL", $"{rule.FacilityId} Lv.{rule.FacilityLevelRequired}", current, rule.FacilityLevelRequired, current >= rule.FacilityLevelRequired));
            }
            if (rule.RaidId != null)
            {
                long current = document["payload"]!["regions"]!["raids"]!.Children<JObject>().Where(value => value.Value<string>("raidId") == rule.RaidId).Sum(value => value.Value<long?>("clearCount") ?? 0);
                result.Add(new RegionRequirementDto("RAID_CLEAR", "히드라 레이드 클리어", current, rule.RaidClearRequired, current >= rule.RaidClearRequired));
            }
            return result;
        }

        private static JObject Progress(JObject document, string regionId) => document["payload"]!["regions"]!["progress"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("regionId") == regionId) ?? throw new RegionDomainException("P12_REGION_NOT_FOUND");
        private static JObject Policy(JObject document, string regionId) => document["payload"]!["kingdom"]!["regionAccessPolicies"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("regionId") == regionId) ?? throw new RegionDomainException("P12_REGION_POLICY_NOT_FOUND");
        private static JObject FindJournal(JObject document, Guid operationId) => document["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == operationId.ToString("D"));
        private static void AppendJournal(JObject document, SetRegionAccessPolicyCommand command, string digest, DateTimeOffset now)
        {
            string timestamp = FormatUtc(now); ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject
            {
                ["operationId"] = command.OperationId.ToString("D"), ["operationType"] = "REGION_POLICY", ["facilityJobType"] = null,
                ["requestHash"] = command.RequestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = timestamp, ["updatedAtUtc"] = timestamp,
                ["completedAtUtc"] = timestamp, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest,
                ["failureResolution"] = null, ["resolvedAtUtc"] = null
            });
        }
        private static void AppendEvent(JObject document, string eventType, string regionId, IEnumerable<string> party, int? beforeProgress, int? afterProgress, bool? beforeAllowed, bool? afterAllowed, string code, DateTimeOffset now)
        {
            JObject regions = (JObject)document["payload"]!["regions"]!; var events = (JArray)regions["events"]!; long sequence = regions.Value<long>("nextEventSequence");
            events.Add(new JObject
            {
                ["sequence"] = sequence, ["eventType"] = eventType, ["regionId"] = regionId, ["mercenaryInstanceIds"] = new JArray(party),
                ["progressBefore"] = beforeProgress.HasValue ? new JValue(beforeProgress.Value) : JValue.CreateNull(),
                ["progressAfter"] = afterProgress.HasValue ? new JValue(afterProgress.Value) : JValue.CreateNull(),
                ["allowedBefore"] = beforeAllowed.HasValue ? new JValue(beforeAllowed.Value) : JValue.CreateNull(),
                ["allowedAfter"] = afterAllowed.HasValue ? new JValue(afterAllowed.Value) : JValue.CreateNull(),
                ["resultCode"] = code, ["createdAtUtc"] = FormatUtc(now)
            });
            regions["nextEventSequence"] = checked(sequence + 1); while (events.Count > 200) events.RemoveAt(0);
        }
        private void Commit(JObject draft, long expectedRevision)
        {
            SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, expectedRevision, clock.UtcNow);
            if (!written.Success) throw new RegionDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P12_SAVE_REVISION_CONFLICT" : "P12_SAVE_WRITE_FAILED");
            game.SynchronizeCommittedDocument(written.Document);
        }
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static void VerifyHash(string expected, string actual) { if (expected == null || actual == null || expected.Length != actual.Length || !expected.SequenceEqual(actual)) throw new RegionDomainException("P12_OPERATION_HASH_MISMATCH"); }
        private static void Require(bool condition, string code) { if (!condition) throw new RegionDomainException(code); }
        private void EnsureReady() { if (!IsBootstrapped) throw new RegionDomainException("P12_CONTENT_MISSING"); }
    }
}
