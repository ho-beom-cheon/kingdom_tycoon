using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Infrastructure.Economy;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Combat
{
    public sealed class ContinuousHuntMemberDto
    {
        public ContinuousHuntMemberDto(string instanceId, string displayName, string jobId, string state, string assignedRegionId,
            int currentHpBps, int bagFill, int bagCapacity, long cyclesCompleted, long earnedGold, int totalSkillLevels,
            int bestEnhancementLevel, string lastGrowthAction, string lastGrowthResultCode)
        {
            InstanceId = instanceId; DisplayName = displayName; JobId = jobId; State = state; AssignedRegionId = assignedRegionId;
            CurrentHpBps = currentHpBps; BagFill = bagFill; BagCapacity = bagCapacity; CyclesCompleted = cyclesCompleted; EarnedGold = earnedGold;
            TotalSkillLevels = totalSkillLevels; BestEnhancementLevel = bestEnhancementLevel; LastGrowthAction = lastGrowthAction;
            LastGrowthResultCode = lastGrowthResultCode;
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
        public int TotalSkillLevels { get; }
        public int BestEnhancementLevel { get; }
        public string LastGrowthAction { get; }
        public string LastGrowthResultCode { get; }
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
        private readonly AutomaticGrowthRules growthRules;
        private SaveService save;
        private FacilityGameService game;
        private EconomyGameService economy;
        private ContentCatalogService content;
        private Dictionary<string, int> rankOrder;
        private Dictionary<string, SkillUnlock[]> skillsByJob;
        private Dictionary<int, EnhancementRule> enhancementRules;

        public ContinuousHuntGameService(ITrustedUtcClock clock, string automaticGrowthRulesJson)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            growthRules = AutomaticGrowthRules.Parse(automaticGrowthRulesJson);
        }
        public int InitializationOrder => 75;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<ContinuousHuntOverviewDto> Changed;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>();
            game = services.Get<FacilityGameService>();
            economy = services.Get<EconomyGameService>();
            content = services.Get<ContentCatalogService>();
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped) throw new InvalidOperationException("CONTINUOUS_HUNT_SAVE_NOT_READY");
            LoadGrowthCatalog();
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
            Changed = null; enhancementRules = null; skillsByJob = null; rankOrder = null; content = null; economy = null; game = null; save = null; IsBootstrapped = false;
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
                changed |= AddIfMissing(autonomy, "autoGrowthEnabled", true);
                changed |= AddIfMissing(autonomy, "autoSkillTraining", true);
                changed |= AddIfMissing(autonomy, "autoEquipmentEnhancement", true);
                changed |= AddIfMissing(autonomy, "growthPersonalGoldReserve", 50L);
                changed |= AddIfMissing(autonomy, "lastGrowthAction", "NONE");
                changed |= AddIfMissing(autonomy, "lastGrowthResultCode", "NONE");
                changed |= AddIfMissing(autonomy, "lastGrowthAtUtc", JValue.CreateNull());
                changed |= AddIfMissing(mercenary, "skillGrowth", new JObject
                {
                    ["growthVersion"] = 1, ["skills"] = new JArray(), ["totalSpentGold"] = 0L
                });
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

        private bool AdvanceMember(JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            string state = autonomy.Value<string>("state");
            string assigned = autonomy["assignedRegionId"]!.Type == JTokenType.Null ? null : autonomy.Value<string>("assignedRegionId");
            switch (state)
            {
                case "IDLE_TOWN":
                case "ENHANCE_EQUIPMENT":
                    if (state == "ENHANCE_EQUIPMENT") EnhanceEquipment(mercenary, autonomy, at);
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
                    damage = Math.Max(1, damage * (10000 - SkillBonusBps(mercenary, growthRules.SkillHuntDamageReductionBpsPerLevel)) / 10000);
                    autonomy["currentHpBps"] = Math.Max(0, autonomy.Value<int>("currentHpBps") - damage);
                    SetState(autonomy, "LOOT", "LOOT_COMPLETE", at);
                    return false;
                case "LOOT":
                    autonomy["bagFill"] = Math.Min(autonomy.Value<int>("bagCapacity"), checked(autonomy.Value<int>("bagFill") + 1));
                    int goldBonusBps = SkillBonusBps(mercenary, growthRules.SkillHuntGoldBonusBpsPerLevel);
                    autonomy["pendingSaleGold"] = checked(autonomy.Value<long>("pendingSaleGold") + SaleGoldPerCycle * (10000L + goldBonusBps) / 10000L);
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
                case "BUY_EQUIPMENT": SetState(autonomy, "TRAIN_SKILLS", "GROWTH_POLICY", at); return false;
                case "TRAIN_SKILLS":
                    TrainSkill(mercenary, autonomy, at);
                    SetState(autonomy, "ENHANCE_EQUIPMENT", "GROWTH_POLICY", at); return false;
                case "INJURED": SetState(autonomy, "HEAL", "HP_LOW", at); return true;
                default: SetState(autonomy, "IDLE_TOWN", "NONE", at); return false;
            }
        }

        private void LoadGrowthCatalog()
        {
            if (content.Catalog == null) throw new InvalidOperationException("CONTINUOUS_HUNT_CONTENT_NOT_READY");
            rankOrder = content.Catalog.GetTable("mercenary_ranks.csv").Rows.Where(Enabled)
                .ToDictionary(row => row["rank_id"], row => ParseInt(row, "order"), StringComparer.Ordinal);
            skillsByJob = content.Catalog.GetTable("job_skill_unlocks.csv").Rows.Where(Enabled)
                .GroupBy(row => row["job_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(row => new SkillUnlock(row["skill_id"], row["unlock_rank_id"], ParseInt(row, "slot_no")))
                    .OrderBy(value => value.Slot).ToArray(), StringComparer.Ordinal);
            enhancementRules = content.Catalog.GetTable("enhancement_rules.csv").Rows.Where(Enabled)
                .Select(row => new EnhancementRule(ParseInt(row, "target_level"), ParseBps(row, "success_chance"), row["stone_item_id"],
                    ParseLong(row, "stone_quantity"), ParseLong(row, "personal_gold_cost"), ParseBps(row, "fail_pity_increment")))
                .ToDictionary(value => value.TargetLevel);
        }

        private void TrainSkill(JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            if (!autonomy.Value<bool>("autoGrowthEnabled") || !autonomy.Value<bool>("autoSkillTraining"))
            { RecordGrowth(autonomy, "NONE", "GROWTH_SKILL_DISABLED", at); return; }
            if (!skillsByJob.TryGetValue(mercenary.Value<string>("jobId"), out SkillUnlock[] unlocks) ||
                !rankOrder.TryGetValue(mercenary.Value<string>("rankId"), out int actorRank))
            { RecordGrowth(autonomy, "NONE", "GROWTH_SKILL_NOT_AVAILABLE", at); return; }
            JObject growth = (JObject)mercenary["skillGrowth"]!;
            JArray levels = (JArray)growth["skills"]!;
            SkillUnlock target = unlocks.Where(value => rankOrder.TryGetValue(value.UnlockRankId, out int required) && required <= actorRank)
                .OrderBy(value => CurrentSkillLevel(levels, value.SkillId)).ThenBy(value => value.Slot)
                .FirstOrDefault(value => CurrentSkillLevel(levels, value.SkillId) < growthRules.MaxSkillLevel);
            if (target == null) { RecordGrowth(autonomy, "NONE", "GROWTH_SKILLS_MAXED", at); return; }
            int nextLevel = CurrentSkillLevel(levels, target.SkillId) + 1;
            long cost = growthRules.SkillCost(nextLevel);
            long reserve = Math.Max(growthRules.PersonalGoldReserve, autonomy.Value<long>("growthPersonalGoldReserve"));
            if (mercenary.Value<long>("personalGold") - cost < reserve)
            { RecordGrowth(autonomy, "NONE", "GROWTH_GOLD_RESERVED", at); return; }
            mercenary["personalGold"] = mercenary.Value<long>("personalGold") - cost;
            JObject line = levels.Children<JObject>().SingleOrDefault(value => value.Value<string>("skillId") == target.SkillId);
            if (line == null)
            {
                line = new JObject { ["skillId"] = target.SkillId, ["level"] = nextLevel, ["totalSpentGold"] = cost, ["trainedAtUtc"] = FormatUtc(at) };
                levels.Add(line);
            }
            else
            {
                line["level"] = nextLevel; line["totalSpentGold"] = checked(line.Value<long>("totalSpentGold") + cost); line["trainedAtUtc"] = FormatUtc(at);
            }
            growth["skills"] = new JArray(levels.Children<JObject>().OrderBy(value => value.Value<string>("skillId"), StringComparer.Ordinal));
            growth["totalSpentGold"] = checked(growth.Value<long>("totalSpentGold") + cost);
            RecordGrowth(autonomy, "SKILL_TRAINED", "GROWTH_SKILL_TRAINED", at);
        }

        private void EnhanceEquipment(JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            if (!autonomy.Value<bool>("autoGrowthEnabled") || !autonomy.Value<bool>("autoEquipmentEnhancement"))
            { RecordGrowth(autonomy, "NONE", "GROWTH_ENHANCEMENT_DISABLED", at); return; }
            JObject blacksmith = gameDocumentFacility(mercenary, "FAC_BLACKSMITH");
            if (blacksmith == null || blacksmith.Value<string>("state") != "ACTIVE")
            { RecordGrowth(autonomy, "NONE", "GROWTH_BLACKSMITH_INACTIVE", at); return; }
            JObject document = mercenary.Root as JObject;
            string actorId = mercenary.Value<string>("instanceId");
            JObject equipment = document!["payload"]!["inventory"]!["equipment"]!.Children<JObject>()
                .Where(value => !value.Value<bool>("locked") && value["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null && value.Value<string>("equippedByMercenaryInstanceId") == actorId)
                .Where(value => value.Value<int>("enhancementLevel") < growthRules.MaxAutomaticEnhancementLevel)
                .OrderBy(value => value.Value<int>("enhancementLevel")).ThenBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).FirstOrDefault();
            if (equipment == null) { RecordGrowth(autonomy, "NONE", "GROWTH_EQUIPMENT_NOT_AVAILABLE", at); return; }
            int before = equipment.Value<int>("enhancementLevel");
            if (!enhancementRules.TryGetValue(before + 1, out EnhancementRule rule) || (!growthRules.AllowRiskyEnhancement && rule.ChanceBps < 10000))
            { RecordGrowth(autonomy, "NONE", "GROWTH_ENHANCEMENT_POLICY_LIMIT", at); return; }
            long reserve = Math.Max(growthRules.PersonalGoldReserve, autonomy.Value<long>("growthPersonalGoldReserve"));
            if (mercenary.Value<long>("personalGold") - rule.Gold < reserve)
            { RecordGrowth(autonomy, "NONE", "GROWTH_GOLD_RESERVED", at); return; }
            JArray stacks = (JArray)document["payload"]!["inventory"]!["itemStacks"]!;
            JObject stack = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == rule.StoneId);
            if (stack == null || stack.Value<long>("quantity") < rule.StoneQuantity)
            { RecordGrowth(autonomy, "NONE", "GROWTH_MATERIAL_INSUFFICIENT", at); return; }
            mercenary["personalGold"] = mercenary.Value<long>("personalGold") - rule.Gold;
            long remaining = stack.Value<long>("quantity") - rule.StoneQuantity;
            if (remaining == 0) stack.Remove(); else stack["quantity"] = remaining;
            AddInvestment(equipment, rule.StoneId, rule.StoneQuantity);
            long attempt = checked(equipment.Value<long>("enhancementAttemptCount") + 1); equipment["enhancementAttemptCount"] = attempt;
            int chance = Math.Min(10000, rule.ChanceBps + equipment.Value<int>("enhancementPityBps"));
            bool success = chance >= 10000 || DeterministicRoll(actorId, equipment.Value<string>("instanceId"), attempt) < chance;
            if (success) { equipment["enhancementLevel"] = before + 1; equipment["enhancementPityBps"] = 0; }
            else equipment["enhancementPityBps"] = Math.Min(10000, checked(equipment.Value<int>("enhancementPityBps") + rule.PityIncrementBps));
            RecordGrowth(autonomy, "EQUIPMENT_ENHANCED", success ? "GROWTH_ENHANCEMENT_SUCCESS" : "GROWTH_ENHANCEMENT_FAILED", at);
        }

        private static JObject gameDocumentFacility(JObject mercenary, string facilityId) => (mercenary.Root as JObject)?["payload"]?["facilities"]?.Children<JObject>()
            .SingleOrDefault(value => value.Value<string>("facilityId") == facilityId);
        private int SkillBonusBps(JObject mercenary, int perLevel) => Math.Min(growthRules.MaximumSkillHuntBonusBps, checked(TotalSkillLevels(mercenary) * perLevel));
        private static int TotalSkillLevels(JObject mercenary) => mercenary["skillGrowth"]?["skills"]?.Children<JObject>().Sum(value => value.Value<int>("level")) ?? 0;
        private static int CurrentSkillLevel(JArray values, string skillId) => values.Children<JObject>().SingleOrDefault(value => value.Value<string>("skillId") == skillId)?.Value<int>("level") ?? 0;
        private static void RecordGrowth(JObject autonomy, string action, string result, DateTimeOffset at)
        { autonomy["lastGrowthAction"] = action; autonomy["lastGrowthResultCode"] = result; autonomy["lastGrowthAtUtc"] = FormatUtc(at); }
        private static void AddInvestment(JObject equipment, string itemId, long quantity)
        {
            JArray values = (JArray)equipment["enhancementMaterialInvested"]!;
            JObject line = values.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == itemId);
            if (line == null) values.Add(new JObject { ["itemId"] = itemId, ["quantity"] = quantity });
            else line["quantity"] = checked(line.Value<long>("quantity") + quantity);
            equipment["enhancementMaterialInvested"] = new JArray(values.Children<JObject>().OrderBy(value => value.Value<string>("itemId"), StringComparer.Ordinal));
        }
        private static int DeterministicRoll(string actorId, string equipmentId, long attempt)
        {
            unchecked { uint hash = 2166136261; foreach (char value in actorId + equipmentId + attempt.ToString(CultureInfo.InvariantCulture)) { hash ^= value; hash *= 16777619; } return (int)(hash % 10000); }
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
                    autonomy.Value<int>("currentHpBps"), autonomy.Value<int>("bagFill"), autonomy.Value<int>("bagCapacity"), autonomy.Value<long>("cyclesCompleted"), autonomy.Value<long>("earnedGold"),
                    TotalSkillLevels(value), document["payload"]!["inventory"]!["equipment"]!.Children<JObject>()
                        .Where(item => item["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null && item.Value<string>("equippedByMercenaryInstanceId") == value.Value<string>("instanceId"))
                        .Select(item => item.Value<int>("enhancementLevel")).DefaultIfEmpty(0).Max(), autonomy.Value<string>("lastGrowthAction"), autonomy.Value<string>("lastGrowthResultCode"));
            }).ToArray());

        private static JObject FindMercenary(JObject document, string id) => document["payload"]!["mercenaries"]!.Children<JObject>()
            .SingleOrDefault(value => value.Value<string>("instanceId") == id) ?? throw new InvalidOperationException("CONTINUOUS_HUNT_MERCENARY_NOT_FOUND");
        private static bool RegionUnlocked(JObject document, string regionId) => document["payload"]!["regions"]!["progress"]!.Children<JObject>()
            .Any(value => value.Value<string>("regionId") == regionId && value.Value<bool>("unlocked"));
        private static bool AddIfMissing(JObject target, string property, object value)
        { if (target[property] != null) return false; target[property] = value is JToken token ? token : JToken.FromObject(value); return true; }
        private static bool KnownState(string state) => state is "IDLE_TOWN" or "PREPARE" or "TRAVEL_TO_REGION" or "FIND_TARGET" or "COMBAT" or "LOOT" or "CONTINUE_DECISION" or "RETURN_TOWN" or "SELL_LOOT" or "HEAL" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" or "TRAIN_SKILLS" or "ENHANCE_EQUIPMENT" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED" or "RAID_READY";
        private static bool IsTownState(string state) => state is "IDLE_TOWN" or "SELL_LOOT" or "HEAL" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" or "TRAIN_SKILLS" or "ENHANCE_EQUIPMENT" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED" or "RAID_READY";
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
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int ParseInt(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long ParseLong(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static int ParseBps(IReadOnlyDictionary<string, string> row, string key) => checked((int)Math.Round(decimal.Parse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture) * 10000m, MidpointRounding.AwayFromZero));
        private void EnsureReady() { if (!IsBootstrapped || game == null || save == null) throw new InvalidOperationException("CONTINUOUS_HUNT_NOT_READY"); }

        private sealed class SkillUnlock
        {
            public SkillUnlock(string skillId, string unlockRankId, int slot) { SkillId = skillId; UnlockRankId = unlockRankId; Slot = slot; }
            public string SkillId { get; }
            public string UnlockRankId { get; }
            public int Slot { get; }
        }

        private sealed class EnhancementRule
        {
            public EnhancementRule(int targetLevel, int chanceBps, string stoneId, long stoneQuantity, long gold, int pityIncrementBps)
            { TargetLevel = targetLevel; ChanceBps = chanceBps; StoneId = stoneId; StoneQuantity = stoneQuantity; Gold = gold; PityIncrementBps = pityIncrementBps; }
            public int TargetLevel { get; }
            public int ChanceBps { get; }
            public string StoneId { get; }
            public long StoneQuantity { get; }
            public long Gold { get; }
            public int PityIncrementBps { get; }
        }

        private sealed class AutomaticGrowthRules
        {
            private readonly Dictionary<int, long> skillCosts;
            private AutomaticGrowthRules(JObject value)
            {
                int version = value.Value<int>("rulesVersion");
                if (version != 1) throw new InvalidOperationException("AUTOMATIC_GROWTH_RULE_VERSION_INVALID");
                PersonalGoldReserve = value.Value<long>("personalGoldReserve"); MaxSkillLevel = value.Value<int>("maxSkillLevel");
                MaxAutomaticEnhancementLevel = value.Value<int>("maxAutomaticEnhancementLevel"); AllowRiskyEnhancement = value.Value<bool>("allowRiskyEnhancement");
                SkillHuntDamageReductionBpsPerLevel = value.Value<int>("skillHuntDamageReductionBpsPerLevel"); SkillHuntGoldBonusBpsPerLevel = value.Value<int>("skillHuntGoldBonusBpsPerLevel");
                MaximumSkillHuntBonusBps = value.Value<int>("maximumSkillHuntBonusBps");
                skillCosts = value["skillCosts"]!.Children<JObject>().ToDictionary(item => item.Value<int>("targetLevel"), item => item.Value<long>("personalGoldCost"));
                if (PersonalGoldReserve < 0 || MaxSkillLevel is < 1 or > 3 || MaxAutomaticEnhancementLevel is < 0 or > 10 || skillCosts.Count != MaxSkillLevel)
                    throw new InvalidOperationException("AUTOMATIC_GROWTH_RULE_INVALID");
            }
            public long PersonalGoldReserve { get; }
            public int MaxSkillLevel { get; }
            public int MaxAutomaticEnhancementLevel { get; }
            public bool AllowRiskyEnhancement { get; }
            public int SkillHuntDamageReductionBpsPerLevel { get; }
            public int SkillHuntGoldBonusBpsPerLevel { get; }
            public int MaximumSkillHuntBonusBps { get; }
            public long SkillCost(int targetLevel) => skillCosts.TryGetValue(targetLevel, out long value) ? value : throw new InvalidOperationException("AUTOMATIC_GROWTH_SKILL_COST_MISSING");
            public static AutomaticGrowthRules Parse(string json)
            {
                if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Automatic growth rules are required.", nameof(json));
                return new AutomaticGrowthRules(JObject.Parse(json));
            }
        }
    }
}
