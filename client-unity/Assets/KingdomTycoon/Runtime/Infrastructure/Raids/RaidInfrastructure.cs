using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Application.Raids;
using KingdomTycoon.Domain.Raids;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Regions;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Raids
{
    public sealed class RaidRequestHasher
    {
        public string Compute(ResolveRaidCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class P14RaidCatalog
    {
        public sealed class RaidRule { public string Id; public string BossId; public string MinimumRankId; public int PartyMin; public int PartyMax; public string FirstRewardGroupId; public int TimeLimitSeconds; }
        public sealed class DifficultyRule { public string RaidId; public string Difficulty; public int RecommendedPower; public string RewardGroupId; }
        public sealed class PartRule { public string RaidId; public string Id; public decimal MaxHpRatio; public string RewardGroupId; public string BehaviorChange; public string PrerequisitePartId; public int BossDamageShareBps; public int TargetOrder; }
        public sealed class PhaseRule { public string RaidId; public int PhaseNo; public int TriggerBossHpBps; public string AbilityTag; public int ActionIntervalTicks; public int AttackMultiplierBps; }
        public sealed class UnlockRule { public string RaidId; public string Difficulty; public string StageId; public string RegionId; public int RegionProgressRequired; public string PreviousDifficulty; public long PreviousClearRequired; public string FlagId; }
        public sealed class RewardRule { public string GroupId; public int EntryNo; public string Type; public string Id; public long Quantity; }
        public sealed class MonsterRule { public string Id; public int MaxHp; public int Attack; public int Defense; }
        public sealed class JobRule { public string Id; public int MaxHp; public int Attack; public int Defense; public int HealPower; }

        private readonly Dictionary<string, RaidRule> raids;
        private readonly Dictionary<string, DifficultyRule> difficulties;
        private readonly Dictionary<string, IReadOnlyList<PartRule>> parts;
        private readonly Dictionary<string, IReadOnlyList<PhaseRule>> phases;
        private readonly Dictionary<string, UnlockRule> unlocks;
        private readonly Dictionary<string, IReadOnlyList<RewardRule>> rewards;
        private readonly Dictionary<string, MonsterRule> monsters;
        private readonly Dictionary<string, JobRule> jobs;
        private readonly Dictionary<string, int> rankOrder;
        private readonly Dictionary<string, decimal> gradeMultiplier;

        public P14RaidCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion != CompileTimeActiveContentVersionProvider.P14ContentVersion) throw new RaidDomainException("P14_CONTENT_VERSION_UNSUPPORTED");
            raids = catalog.GetTable("raids.csv").Rows.Where(Enabled).ToDictionary(row => row["raid_id"], row => new RaidRule
            { Id = row["raid_id"], BossId = row["boss_monster_id"], MinimumRankId = row["min_rank_id"], PartyMin = Int(row, "party_min"), PartyMax = Int(row, "party_max"), FirstRewardGroupId = row["first_clear_reward_group_id"], TimeLimitSeconds = Int(row, "time_limit_sec") }, StringComparer.Ordinal);
            difficulties = catalog.GetTable("raid_difficulties.csv").Rows.Where(Enabled).ToDictionary(row => Key(row["raid_id"], row["difficulty"]), row => new DifficultyRule
            { RaidId = row["raid_id"], Difficulty = row["difficulty"], RecommendedPower = Int(row, "recommended_power"), RewardGroupId = row["reward_group_id"] }, StringComparer.Ordinal);
            var targeting = catalog.GetTable("raid_part_target_rules.csv").Rows.Where(Enabled).ToDictionary(row => Key(row["raid_id"], row["part_id"]), StringComparer.Ordinal);
            parts = catalog.GetTable("raid_parts.csv").Rows.Where(Enabled).Select(row =>
            {
                IReadOnlyDictionary<string, string> target = targeting[Key(row["raid_id"], row["part_id"])];
                return new PartRule { RaidId = row["raid_id"], Id = row["part_id"], MaxHpRatio = Decimal(row, "max_hp_ratio"), RewardGroupId = row["break_reward_group_id"], BehaviorChange = row["behavior_change"], PrerequisitePartId = Null(target["prerequisite_part_id"]), BossDamageShareBps = Int(target, "boss_damage_share_bps"), TargetOrder = Int(target, "target_order") };
            }).GroupBy(value => value.RaidId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<PartRule>)group.OrderBy(value => value.TargetOrder).ToArray(), StringComparer.Ordinal);
            phases = catalog.GetTable("raid_boss_phase_rules.csv").Rows.Where(Enabled).Select(row => new PhaseRule
            { RaidId = row["raid_id"], PhaseNo = Int(row, "phase_no"), TriggerBossHpBps = Int(row, "trigger_boss_hp_bps"), AbilityTag = row["ability_tag"], ActionIntervalTicks = Int(row, "action_interval_ticks"), AttackMultiplierBps = Int(row, "attack_multiplier_bps") })
                .GroupBy(value => value.RaidId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<PhaseRule>)group.OrderBy(value => value.PhaseNo).ToArray(), StringComparer.Ordinal);
            unlocks = catalog.GetTable("raid_unlock_rules.csv").Rows.Where(Enabled).ToDictionary(row => Key(row["raid_id"], row["difficulty"]), row => new UnlockRule
            { RaidId = row["raid_id"], Difficulty = row["difficulty"], StageId = row["kingdom_stage_id"], RegionId = row["region_id"], RegionProgressRequired = Int(row, "region_progress_required"), PreviousDifficulty = Null(row["previous_difficulty"]), PreviousClearRequired = Long(row, "previous_clear_required"), FlagId = Null(row["progression_flag_id"]) }, StringComparer.Ordinal);
            rewards = catalog.GetTable("reward_entries.csv").Rows.Where(Enabled).Select(row => new RewardRule
            { GroupId = row["reward_group_id"], EntryNo = Int(row, "entry_no"), Type = row["reward_type"], Id = row["reward_id"], Quantity = Long(row, "quantity") })
                .GroupBy(value => value.GroupId, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<RewardRule>)group.OrderBy(value => value.EntryNo).ToArray(), StringComparer.Ordinal);
            monsters = catalog.GetTable("monsters.csv").Rows.Where(Enabled).Where(row => row["type"] == "RAID").ToDictionary(row => row["monster_id"], row => new MonsterRule { Id = row["monster_id"], MaxHp = Int(row, "hp"), Attack = Int(row, "attack"), Defense = Int(row, "defense") }, StringComparer.Ordinal);
            jobs = catalog.GetTable("combat_job_profiles.csv").Rows.Where(Enabled).ToDictionary(row => row["job_id"], row => new JobRule { Id = row["job_id"], MaxHp = Int(row, "max_hp"), Attack = Int(row, "attack"), Defense = Int(row, "defense"), HealPower = Int(row, "heal_power") }, StringComparer.Ordinal);
            rankOrder = catalog.GetTable("mercenary_ranks.csv").Rows.Where(Enabled).ToDictionary(row => row["rank_id"], row => Int(row, "order"), StringComparer.Ordinal);
            gradeMultiplier = catalog.GetTable("mercenary_grades.csv").Rows.Where(Enabled).ToDictionary(row => row["grade_id"], row => Decimal(row, "base_stat_multiplier"), StringComparer.Ordinal);
            if (raids.Count != 2 || difficulties.Count != 6 || parts.Values.Sum(value => value.Count) != 7 || phases.Values.Sum(value => value.Count) != 6) throw new RaidDomainException("P14_CONTENT_INVALID");
        }

        public IReadOnlyList<RaidRule> Raids => raids.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        public RaidRule Raid(string id) => Get(raids, id, "P14_RAID_NOT_FOUND");
        public DifficultyRule Difficulty(string raid, string difficulty) => Get(difficulties, Key(raid, difficulty), "P14_DIFFICULTY_NOT_FOUND");
        public IReadOnlyList<PartRule> Parts(string raid) => Get(parts, raid, "P14_RAID_NOT_FOUND");
        public IReadOnlyList<PhaseRule> Phases(string raid) => Get(phases, raid, "P14_RAID_NOT_FOUND");
        public UnlockRule Unlock(string raid, string difficulty) => Get(unlocks, Key(raid, difficulty), "P14_DIFFICULTY_NOT_FOUND");
        public IReadOnlyList<RewardRule> Rewards(string group) => Get(rewards, group, "P14_REWARD_GROUP_NOT_FOUND");
        public MonsterRule Monster(string id) => Get(monsters, id, "P14_BOSS_NOT_FOUND");
        public JobRule Job(string id) => Get(jobs, id, "P14_JOB_NOT_FOUND");
        public int RankOrder(string id) => Get(rankOrder, id, "P14_RANK_NOT_FOUND");
        public decimal GradeMultiplier(string id) => Get(gradeMultiplier, id, "P14_GRADE_NOT_FOUND");
        private static T Get<T>(IReadOnlyDictionary<string, T> source, string key, string code) => source.TryGetValue(key ?? string.Empty, out T value) ? value : throw new RaidDomainException(code);
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static string Null(string value) => string.IsNullOrEmpty(value) ? null : value;
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static decimal Decimal(IReadOnlyDictionary<string, string> row, string key) => decimal.Parse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture);
        private static string Key(string raid, string difficulty) => raid + "|" + difficulty;
    }

    public sealed class RaidGameService : IAppService
    {
        private static readonly IReadOnlyDictionary<string, int> StageOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        { ["KINGDOM_1"] = 1, ["KINGDOM_2"] = 2, ["KINGDOM_3"] = 3, ["KINGDOM_4"] = 4, ["KINGDOM_5"] = 5 };
        private readonly ITrustedUtcClock clock;
        private readonly IUuidV7Provider ids;
        private readonly SemaphoreSlim gate = new(1, 1);
        private SaveService save; private ContentCatalogService content; private FacilityGameService game; private RegionGameService regions; private P14RaidCatalog catalog;

        public RaidGameService(ITrustedUtcClock clock, IUuidV7Provider ids = null) { this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); this.ids = ids ?? new SystemUuidV7Provider(); }
        public int InitializationOrder => 130;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler Changed;
        public void Initialize(ServiceRegistry services) { save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>(); regions = services.Get<RegionGameService>(); }
        public void Bootstrap()
        {
            if (!game.IsBootstrapped || content.Catalog?.ContentVersion != CompileTimeActiveContentVersionProvider.P14ContentVersion || !regions.IsBootstrapped) throw new RaidDomainException("P14_CONTENT_VERSION_UNSUPPORTED");
            catalog = new P14RaidCatalog(content.Catalog); IsBootstrapped = true; NormalizeUnlocks();
        }

        public RaidOverviewDto GetOverview()
        {
            EnsureReady(); NormalizeUnlocks(); JObject document = game.Snapshot(); JObject regionState = (JObject)document["payload"]!["regions"]!;
            var rows = new List<RaidSummaryDto>();
            foreach (P14RaidCatalog.RaidRule raid in catalog.Raids)
            foreach (string difficulty in new[] { "NORMAL", "HARD", "CORRUPTED" })
            {
                JObject progress = Progress(document, raid.Id, difficulty); P14RaidCatalog.DifficultyRule rule = catalog.Difficulty(raid.Id, difficulty);
                var parts = catalog.Parts(raid.Id).Select(part =>
                {
                    JObject state = progress["partStates"]!.Children<JObject>().Single(value => value.Value<string>("partId") == part.Id);
                    bool available = part.PrerequisitePartId == null || progress["partStates"]!.Children<JObject>().Single(value => value.Value<string>("partId") == part.PrerequisitePartId).Value<long>("breakCount") > 0;
                    return new RaidPartDto(part.Id, part.BehaviorChange, state.Value<long>("breakCount"), available);
                }).ToArray();
                long? best = progress["bestClearTimeMs"]!.Type == JTokenType.Null ? null : progress.Value<long>("bestClearTimeMs");
                rows.Add(new RaidSummaryDto(raid.Id, difficulty, progress.Value<bool>("unlocked"), rule.RecommendedPower, raid.PartyMin, raid.PartyMax, raid.TimeLimitSeconds, progress.Value<long>("clearCount"), best, parts));
            }
            var candidates = document["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).Select(value =>
            {
                P14RaidCatalog.JobRule job = catalog.Job(value.Value<string>("jobId")); int power = MemberPower(value, job);
                bool eligible = value.Value<bool>("active") && value["autonomy"]!.Value<string>("state") == "IDLE_TOWN" && value["promotion"]!.Value<string>("status") != "IN_REVIEW";
                return new RaidPartyMemberDto(value.Value<string>("instanceId"), value.Value<string>("displayName"), value.Value<string>("jobId"), value.Value<string>("rankId"), eligible, power, value["potions"]!.Children<JObject>().Sum(potion => potion.Value<int>("quantity")));
            }).ToArray();
            JObject last = regionState["raidHistory"]!.Children<JObject>().LastOrDefault();
            return new RaidOverviewDto(game.Revision, rows, candidates, last?.Value<string>("resultCode") ?? "P14_READY", regionState["raidHistory"]!.Count());
        }

        public ResolveRaidCommand CreateResolveCommand(string raidId, string difficulty, IReadOnlyList<string> party, string targetPartId, bool warningsAccepted)
        {
            Guid id = ids.NewId(); var draft = new ResolveRaidCommand(id, game.Revision, null, raidId, difficulty, party, targetPartId, warningsAccepted);
            return new ResolveRaidCommand(id, game.Revision, new RaidRequestHasher().Compute(draft), raidId, difficulty, party, targetPartId, warningsAccepted);
        }

        public RaidOperationResult Resolve(ResolveRaidCommand command)
        {
            EnsureReady(); if (command == null) throw new ArgumentNullException(nameof(command)); gate.Wait();
            try
            {
                VerifyHash(new RaidRequestHasher().Compute(command), command.RequestHash); JObject current = game.Snapshot(); JObject replay = FindJournal(current, command.OperationId);
                if (replay != null) { VerifyHash(replay.Value<string>("requestHash"), command.RequestHash); return FromPayload(command.OperationId, game.Revision, (JObject)replay["resultPayload"]!, true); }
                Require(command.ExpectedRevision == game.Revision, "P14_SAVE_REVISION_CONFLICT"); P14RaidCatalog.RaidRule raid = catalog.Raid(command.RaidId); P14RaidCatalog.DifficultyRule difficulty = catalog.Difficulty(command.RaidId, command.Difficulty);
                JObject progress = Progress(current, command.RaidId, command.Difficulty); Require(progress.Value<bool>("unlocked"), "P14_RAID_LOCKED");
                string[] partyIds = command.PartyMercenaryInstanceIds.ToArray(); Require(partyIds.Length >= raid.PartyMin && partyIds.Length <= raid.PartyMax, "P14_PARTY_SIZE_INVALID"); Require(partyIds.Distinct(StringComparer.Ordinal).Count() == partyIds.Length, "P14_PARTY_DUPLICATE");
                P14RaidCatalog.PartRule target = catalog.Parts(command.RaidId).SingleOrDefault(value => value.Id == command.TargetPartId) ?? throw new RaidDomainException("P14_TARGET_PART_INVALID");
                if (target.PrerequisitePartId != null) Require(progress["partStates"]!.Children<JObject>().Single(value => value.Value<string>("partId") == target.PrerequisitePartId).Value<long>("breakCount") > 0, "P14_TARGET_PART_NOT_EXPOSED");
                var mercenaries = current["payload"]!["mercenaries"]!.Children<JObject>().ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal); int requiredRank = catalog.RankOrder(raid.MinimumRankId);
                foreach (string id in partyIds)
                {
                    Require(mercenaries.TryGetValue(id, out JObject member), "P14_PARTY_MEMBER_NOT_FOUND"); Require(member.Value<bool>("active"), "P14_PARTY_MEMBER_INACTIVE"); Require(catalog.RankOrder(member.Value<string>("rankId")) >= requiredRank, "P14_PARTY_RANK_TOO_LOW");
                    Require(member["autonomy"]!.Value<string>("state") == "IDLE_TOWN" && member["autonomy"]!["currentRegionId"]!.Type == JTokenType.Null, "P14_PARTY_NOT_TOWN_SAFE"); Require(member["promotion"]!.Value<string>("status") != "IN_REVIEW", "P14_PARTY_MEMBER_BUSY");
                }
                string[] warnings = PartyWarnings(partyIds.Select(id => mercenaries[id])).ToArray(); Require(warnings.Length == 0 || command.WarningsAccepted, "P14_PARTY_WARNING_CONFIRMATION_REQUIRED");
                P14RaidCatalog.MonsterRule boss = catalog.Monster(raid.BossId); int scale = command.Difficulty == "NORMAL" ? 10000 : command.Difficulty == "HARD" ? 13500 : 17500;
                var spec = new RaidSimulationSpec
                {
                    RaidId = raid.Id, TargetPartId = target.Id, TimeLimitTicks = raid.TimeLimitSeconds * 10, BossMaxHp = boss.MaxHp, BossAttack = boss.Attack, BossDefense = boss.Defense, DifficultyScaleBps = scale,
                    Party = partyIds.Select(id => Combatant(mercenaries[id])).ToArray(),
                    Parts = catalog.Parts(raid.Id).Select(value => new RaidPartSpec { Id = value.Id, MaxHp = Math.Max(1, (int)(boss.MaxHp * value.MaxHpRatio)), BossDamageShareBps = value.BossDamageShareBps, TargetOrder = value.TargetOrder, PrerequisitePartId = value.PrerequisitePartId }).ToArray(),
                    Phases = catalog.Phases(raid.Id).Select(value => new RaidPhaseSpec { PhaseNo = value.PhaseNo, TriggerBossHpBps = value.TriggerBossHpBps, ActionIntervalTicks = value.ActionIntervalTicks, AttackMultiplierBps = value.AttackMultiplierBps, AbilityTag = value.AbilityTag }).ToArray()
                };
                RaidSimulationOutcome outcome = new DeterministicRaidSimulation().Run(spec); JObject draft = (JObject)current.DeepClone(); DateTimeOffset now = clock.UtcNow;
                JObject draftProgress = Progress(draft, raid.Id, command.Difficulty); long previousClears = draftProgress.Value<long>("clearCount"); bool firstClear = outcome.Success && command.Difficulty == "NORMAL" && previousClears == 0;
                draftProgress["attemptCount"] = draftProgress.Value<long>("attemptCount") + 1; draftProgress["lastResultCode"] = outcome.Success ? "P14_RAID_CLEARED" : "P14_RAID_FAILED";
                if (outcome.Success) { draftProgress["clearCount"] = previousClears + 1; long duration = outcome.DurationTicks * 100L; if (draftProgress["bestClearTimeMs"]!.Type == JTokenType.Null || duration < draftProgress.Value<long>("bestClearTimeMs")) draftProgress["bestClearTimeMs"] = duration; if (previousClears == 0) draftProgress["firstClearedAtUtc"] = Format(now); draftProgress["lastClearedAtUtc"] = Format(now); if (firstClear) draftProgress["firstClearRewardOperationId"] = command.OperationId.ToString("D"); }
                ApplyMembers(draft, outcome, partyIds, now); JArray rewardSnapshot = outcome.Success ? ApplyRewards(draft, difficulty.RewardGroupId, raid.FirstRewardGroupId, firstClear, outcome.BrokenPartIds, raid.Id) : new JArray();
                if (outcome.Success) ApplyProgress(draft, draftProgress, command.OperationId, now);
                var unlockedRegions = new JArray(); var flags = new JArray(); foreach (JObject reward in rewardSnapshot.Children<JObject>()) { if (reward.Value<string>("rewardType") == "REGION_UNLOCK") unlockedRegions.Add(reward.Value<string>("rewardId")); if (reward.Value<string>("rewardType") == "PROGRESSION_FLAG") flags.Add(reward.Value<string>("rewardId")); }
                JObject payload = BuildPayload(command, outcome, rewardSnapshot, firstClear, unlockedRegions, flags); string digest = Rfc8785Canonicalizer.ComputeSha256(payload); payload["resultDigest"] = digest;
                AppendHistory(draft, payload, digest, now); AppendJournal(draft, command, payload, digest, now); SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, command.ExpectedRevision, now);
                if (!written.Success) throw new RaidDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P14_SAVE_REVISION_CONFLICT" : "P14_SAVE_WRITE_FAILED", written.ErrorCode + " | " + string.Join(" | ", written.Report.Issues));
                game.SynchronizeCommittedDocument(written.Document); Changed?.Invoke(this, EventArgs.Empty); return FromPayload(command.OperationId, game.Revision, payload, false);
            }
            finally { gate.Release(); }
        }

        private RaidCombatantSpec Combatant(JObject member)
        {
            P14RaidCatalog.JobRule job = catalog.Job(member.Value<string>("jobId")); decimal multiplier = catalog.GradeMultiplier(member.Value<string>("gradeId")) * (1m + (member.Value<int>("level") - 1) * 0.025m) * (1m + (catalog.RankOrder(member.Value<string>("rankId")) - 1) * 0.18m);
            return new RaidCombatantSpec { Id = member.Value<string>("instanceId"), JobId = job.Id, MaxHp = Math.Max(1, (int)(job.MaxHp * multiplier)), Attack = Math.Max(1, (int)(job.Attack * multiplier)), Defense = Math.Max(0, (int)(job.Defense * multiplier)), HealPower = Math.Max(0, (int)(job.HealPower * multiplier)), AvailablePotions = member["potions"]!.Children<JObject>().Where(value => value.Value<string>("potionId") == "POT_HEAL_SMALL").Sum(value => value.Value<int>("quantity")) };
        }

        private int MemberPower(JObject member, P14RaidCatalog.JobRule job)
        { RaidCombatantSpec spec = Combatant(member); return spec.MaxHp / 5 + spec.Attack * 4 + spec.Defense * 3 + spec.HealPower * 4; }
        private IEnumerable<string> PartyWarnings(IEnumerable<JObject> party)
        {
            string[] jobs = party.Select(value => value.Value<string>("jobId")).ToArray(); if (!jobs.Contains("JOB_GUARDIAN", StringComparer.Ordinal)) yield return "P14_WARNING_TANK"; if (!jobs.Contains("JOB_CLERIC", StringComparer.Ordinal)) yield return "P14_WARNING_HEALER"; if (!jobs.Any(value => value is "JOB_ARCHER" or "JOB_MAGE")) yield return "P14_WARNING_RANGED"; if (party.Sum(value => value["potions"]!.Children<JObject>().Sum(potion => potion.Value<int>("quantity"))) < party.Count()) yield return "P14_WARNING_POTIONS";
        }
        private void ApplyMembers(JObject draft, RaidSimulationOutcome outcome, string[] partyIds, DateTimeOffset now)
        {
            var members = draft["payload"]!["mercenaries"]!.Children<JObject>().Where(value => partyIds.Contains(value.Value<string>("instanceId"), StringComparer.Ordinal)).ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal);
            foreach (RaidMemberOutcome result in outcome.Members)
            {
                JObject member = members[result.Id]; JObject potion = member["potions"]!.Children<JObject>().Where(value => value.Value<int>("quantity") > 0).OrderByDescending(value => value.Value<string>("potionId") == "POT_HEAL_SMALL").FirstOrDefault();
                if (potion != null && result.PotionsUsed > 0) { int remaining = potion.Value<int>("quantity") - result.PotionsUsed; if (remaining <= 0) potion.Remove(); else potion["quantity"] = remaining; }
                member["contribution"] = checked(member.Value<long>("contribution") + result.Total); member["records"]!["bossContributionCount"] = member["records"]!.Value<long>("bossContributionCount") + 1; if (outcome.Success) member["records"]!["raidClearCount"] = member["records"]!.Value<long>("raidClearCount") + 1;
                if (result.Injured) { member["autonomy"]!["state"] = "INJURED"; member["autonomy"]!["reasonCode"] = "RAID_KO"; } else { member["autonomy"]!["state"] = "IDLE_TOWN"; member["autonomy"]!["reasonCode"] = "NONE"; }
                member["autonomy"]!["currentRegionId"] = null; member["autonomy"]!["targetInstanceId"] = null; member["autonomy"]!["stateStartedAtUtc"] = Format(now); member["autonomy"]!["nextDecisionAtUtc"] = Format(now);
            }
        }
        private JArray ApplyRewards(JObject draft, string repeatGroup, string firstGroup, bool firstClear, IReadOnlyList<string> brokenParts, string raidId)
        {
            var rules = new List<P14RaidCatalog.RewardRule>(); rules.AddRange(catalog.Rewards(repeatGroup)); if (firstClear) rules.AddRange(catalog.Rewards(firstGroup)); foreach (string part in brokenParts) rules.AddRange(catalog.Rewards(catalog.Parts(raidId).Single(value => value.Id == part).RewardGroupId));
            var snapshot = new JArray(); foreach (P14RaidCatalog.RewardRule reward in rules.OrderBy(value => value.Type, StringComparer.Ordinal).ThenBy(value => value.Id, StringComparer.Ordinal))
            {
                snapshot.Add(new JObject { ["rewardType"] = reward.Type, ["rewardId"] = reward.Id, ["quantity"] = reward.Quantity });
                if (reward.Type == "ITEM") AddItem(draft, reward.Id, reward.Quantity); else if (reward.Type == "PROGRESSION_FLAG") AddUnique((JArray)draft["payload"]!["kingdom"]!["progressionFlagIds"]!, reward.Id);
            } return snapshot;
        }
        private void ApplyProgress(JObject draft, JObject current, Guid operationId, DateTimeOffset now)
        {
            foreach (string difficulty in new[] { "HARD", "CORRUPTED" }) { JObject target = Progress(draft, current.Value<string>("raidId"), difficulty); if (RequirementsMet(draft, catalog.Unlock(target.Value<string>("raidId"), difficulty))) target["unlocked"] = true; }
            regions.NormalizeUnlocksInDraft(draft, now);
        }
        private bool NormalizeUnlocks()
        {
            JObject draft = game.Snapshot(); long before = game.Revision; bool changed = false; foreach (JObject progress in draft["payload"]!["regions"]!["raids"]!.Children<JObject>()) if (!progress.Value<bool>("unlocked") && RequirementsMet(draft, catalog.Unlock(progress.Value<string>("raidId"), progress.Value<string>("difficulty")))) { progress["unlocked"] = true; changed = true; }
            if (!changed) return false; SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, before, clock.UtcNow); if (!result.Success) throw new RaidDomainException("P14_SAVE_WRITE_FAILED", result.ErrorCode); game.SynchronizeCommittedDocument(result.Document); return true;
        }
        private bool RequirementsMet(JObject document, P14RaidCatalog.UnlockRule rule)
        {
            int stage = StageOrder.GetValueOrDefault(document["payload"]!["kingdom"]!.Value<string>("kingdomStageId")); if (stage < StageOrder.GetValueOrDefault(rule.StageId)) return false;
            JObject region = document["payload"]!["regions"]!["progress"]!.Children<JObject>().Single(value => value.Value<string>("regionId") == rule.RegionId); if (region.Value<int>("progressPercent") < rule.RegionProgressRequired) return false;
            if (rule.PreviousDifficulty != null && Progress(document, rule.RaidId, rule.PreviousDifficulty).Value<long>("clearCount") < rule.PreviousClearRequired) return false;
            return rule.FlagId == null || document["payload"]!["kingdom"]!["progressionFlagIds"]!.Values<string>().Contains(rule.FlagId, StringComparer.Ordinal);
        }
        private static JObject BuildPayload(ResolveRaidCommand command, RaidSimulationOutcome outcome, JArray rewards, bool firstClear, JArray regions, JArray flags) => new()
        {
            ["operationId"] = command.OperationId.ToString("D"), ["raidId"] = command.RaidId, ["difficulty"] = command.Difficulty, ["resultCode"] = outcome.Success ? "P14_RAID_CLEARED" : "P14_RAID_FAILED", ["success"] = outcome.Success, ["durationMs"] = outcome.DurationTicks * 100L,
            ["targetPartId"] = command.TargetPartId, ["partyMercenaryInstanceIds"] = new JArray(command.PartyMercenaryInstanceIds), ["brokenPartIds"] = new JArray(outcome.BrokenPartIds), ["injuredMercenaryInstanceIds"] = new JArray(outcome.Members.Where(value => value.Injured).Select(value => value.Id)),
            ["potionConsumptions"] = new JArray(outcome.Members.Where(value => value.PotionsUsed > 0).Select(value => new JObject { ["mercenaryInstanceId"] = value.Id, ["potionId"] = "POT_HEAL_SMALL", ["quantity"] = value.PotionsUsed })),
            ["contributions"] = new JArray(outcome.Members.Select(value => new JObject { ["mercenaryInstanceId"] = value.Id, ["damage"] = value.Damage, ["healing"] = value.Healing, ["preventedDamage"] = value.PreventedDamage, ["total"] = value.Total })),
            ["rewards"] = rewards.DeepClone(), ["firstClear"] = firstClear, ["unlockedRegionIds"] = regions, ["progressionFlagIds"] = flags,
            ["trace"] = new JArray(outcome.Trace.Select(value => new JObject { ["tick"] = value.Tick, ["bossHp"] = value.BossHp, ["phase"] = value.Phase, ["activeParty"] = value.ActiveParty, ["eventCode"] = value.EventCode }))
        };
        private static void AppendHistory(JObject draft, JObject payload, string digest, DateTimeOffset now)
        {
            JObject regions = (JObject)draft["payload"]!["regions"]!; JArray values = (JArray)regions["raidHistory"]!; if (values.Count >= 50) values.RemoveAt(0); long sequence = regions.Value<long>("nextRaidHistorySequence"); var entry = (JObject)payload.DeepClone(); entry.Remove("trace"); entry["sequence"] = sequence; entry["resultDigest"] = digest; entry["settledAtUtc"] = Format(now); values.Add(entry); regions["nextRaidHistorySequence"] = sequence + 1;
            JObject progress = Progress(draft, payload.Value<string>("raidId"), payload.Value<string>("difficulty")); foreach (string id in payload["brokenPartIds"]!.Values<string>()) { JObject part = progress["partStates"]!.Children<JObject>().Single(value => value.Value<string>("partId") == id); part["breakCount"] = part.Value<long>("breakCount") + 1; part["exposedCount"] = part.Value<long>("exposedCount") + 1; long duration = payload.Value<long>("durationMs"); if (part["bestBreakTimeMs"]!.Type == JTokenType.Null || duration < part.Value<long>("bestBreakTimeMs")) part["bestBreakTimeMs"] = duration; part["lastRewardOperationId"] = payload.Value<string>("operationId"); }
        }
        private static void AppendJournal(JObject draft, ResolveRaidCommand command, JObject payload, string digest, DateTimeOffset now)
        { string timestamp = Format(now); ((JArray)draft["payload"]!["operationJournal"]!).Add(new JObject { ["operationId"] = command.OperationId.ToString("D"), ["operationType"] = "RAID_RESOLVE", ["facilityJobType"] = null, ["requestHash"] = command.RequestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = timestamp, ["updatedAtUtc"] = timestamp, ["completedAtUtc"] = timestamp, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest, ["failureResolution"] = null, ["resolvedAtUtc"] = null, ["resultPayload"] = payload.DeepClone() }); }
        private static RaidOperationResult FromPayload(Guid id, long revision, JObject payload, bool replayed) => new(id, revision, payload.Value<string>("resultCode"), payload.Value<bool>("success"), payload.Value<long>("durationMs"), payload["brokenPartIds"]!.Values<string>().ToArray(), payload["injuredMercenaryInstanceIds"]!.Values<string>().ToArray(), payload["rewards"]!.Children<JObject>().Select(value => value.Value<string>("rewardType") + ":" + value.Value<string>("rewardId") + " x" + value.Value<long>("quantity")).ToArray(), payload["trace"]?.Children<JObject>().Select(value => $"{value.Value<int>("tick") / 10.0:0.0}s {value.Value<string>("phase")} HP {value.Value<int>("bossHp")}").ToArray() ?? Array.Empty<string>(), replayed);
        private static JObject Progress(JObject document, string raid, string difficulty) => document["payload"]!["regions"]!["raids"]!.Children<JObject>().Single(value => value.Value<string>("raidId") == raid && value.Value<string>("difficulty") == difficulty);
        private static JObject FindJournal(JObject document, Guid id) => document["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == id.ToString("D"));
        private static void AddItem(JObject document, string id, long quantity) { JArray stacks = (JArray)document["payload"]!["inventory"]!["itemStacks"]!; JObject stack = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == id); if (stack == null) stacks.Add(new JObject { ["itemId"] = id, ["quantity"] = quantity }); else stack["quantity"] = checked(stack.Value<long>("quantity") + quantity); }
        private static void AddUnique(JArray values, string id) { if (!values.Values<string>().Contains(id, StringComparer.Ordinal)) values.Add(id); }
        private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static void VerifyHash(string left, string right) { Require(left != null && right != null && left.Length == right.Length && !left.Where((value, index) => value != right[index]).Any(), "P14_OPERATION_HASH_MISMATCH"); }
        private static void Require(bool condition, string code) { if (!condition) throw new RaidDomainException(code); }
        private void EnsureReady() { if (!IsBootstrapped) throw new RaidDomainException("P14_NOT_BOOTSTRAPPED"); }
        public void Shutdown() { Changed = null; IsBootstrapped = false; catalog = null; regions = null; game = null; content = null; save = null; gate.Dispose(); }
    }
}
