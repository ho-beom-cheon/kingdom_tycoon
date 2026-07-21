using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Infrastructure.Content;
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
            string targetMonsterInstanceId, int currentHpBps, int bagFill, int bagCapacity, long cyclesCompleted, long earnedGold,
            long pendingBountyGold, int totalSkillLevels, int bestEnhancementLevel, int lastCombatDamage, string lastSkillId,
            string lastGrowthAction, string lastGrowthResultCode)
        {
            InstanceId = instanceId; DisplayName = displayName; JobId = jobId; State = state; AssignedRegionId = assignedRegionId;
            TargetMonsterInstanceId = targetMonsterInstanceId; CurrentHpBps = currentHpBps; BagFill = bagFill; BagCapacity = bagCapacity;
            CyclesCompleted = cyclesCompleted; EarnedGold = earnedGold; PendingBountyGold = pendingBountyGold; TotalSkillLevels = totalSkillLevels;
            BestEnhancementLevel = bestEnhancementLevel; LastCombatDamage = lastCombatDamage; LastSkillId = lastSkillId;
            LastGrowthAction = lastGrowthAction; LastGrowthResultCode = lastGrowthResultCode;
        }

        public string InstanceId { get; }
        public string DisplayName { get; }
        public string JobId { get; }
        public string State { get; }
        public string AssignedRegionId { get; }
        public string TargetMonsterInstanceId { get; }
        public int CurrentHpBps { get; }
        public int BagFill { get; }
        public int BagCapacity { get; }
        public long CyclesCompleted { get; }
        public long EarnedGold { get; }
        public long PendingBountyGold { get; }
        public int TotalSkillLevels { get; }
        public int BestEnhancementLevel { get; }
        public int LastCombatDamage { get; }
        public string LastSkillId { get; }
        public string LastGrowthAction { get; }
        public string LastGrowthResultCode { get; }
        public bool IsAssigned => AssignedRegionId != null;
    }

    public sealed class WorldHuntRegionDto
    {
        public WorldHuntRegionDto(string id, string displayName, int order, int tier, int recommendedPower, int maxActive,
            int worldX, string theme, bool unlocked, int assignedCount, string riskLabel, string dropPreview)
        { Id = id; DisplayName = displayName; Order = order; Tier = tier; RecommendedPower = recommendedPower; MaxActive = maxActive; WorldX = worldX; Theme = theme; Unlocked = unlocked; AssignedCount = assignedCount; RiskLabel = riskLabel; DropPreview = dropPreview; }
        public string Id { get; }
        public string DisplayName { get; }
        public int Order { get; }
        public int Tier { get; }
        public int RecommendedPower { get; }
        public int MaxActive { get; }
        public int WorldX { get; }
        public string Theme { get; }
        public bool Unlocked { get; }
        public int AssignedCount { get; }
        public string RiskLabel { get; }
        public string DropPreview { get; }
    }

    public sealed class WorldHuntFeedbackConfigDto
    {
        public WorldHuntFeedbackConfigDto(float hitFlashSeconds, float skillPulseSeconds, float damageFloatSeconds, float rewardFeedSeconds,
            int maximumRewardFeedEntries, int masterVolumeBps, int hitFrequencyHz, int skillFrequencyHz, int rewardFrequencyHz, int growthFrequencyHz)
        {
            HitFlashSeconds = hitFlashSeconds; SkillPulseSeconds = skillPulseSeconds; DamageFloatSeconds = damageFloatSeconds; RewardFeedSeconds = rewardFeedSeconds;
            MaximumRewardFeedEntries = maximumRewardFeedEntries; MasterVolumeBps = masterVolumeBps; HitFrequencyHz = hitFrequencyHz;
            SkillFrequencyHz = skillFrequencyHz; RewardFrequencyHz = rewardFrequencyHz; GrowthFrequencyHz = growthFrequencyHz;
        }
        public float HitFlashSeconds { get; }
        public float SkillPulseSeconds { get; }
        public float DamageFloatSeconds { get; }
        public float RewardFeedSeconds { get; }
        public int MaximumRewardFeedEntries { get; }
        public int MasterVolumeBps { get; }
        public int HitFrequencyHz { get; }
        public int SkillFrequencyHz { get; }
        public int RewardFrequencyHz { get; }
        public int GrowthFrequencyHz { get; }
    }

    public sealed class WorldHuntMonsterDto
    {
        public WorldHuntMonsterDto(string instanceId, string monsterId, string displayName, string regionId, string type, int level,
            int currentHp, int maxHp, string state, int spawnSlot, string targetMercenaryInstanceId)
        { InstanceId = instanceId; MonsterId = monsterId; DisplayName = displayName; RegionId = regionId; Type = type; Level = level; CurrentHp = currentHp; MaxHp = maxHp; State = state; SpawnSlot = spawnSlot; TargetMercenaryInstanceId = targetMercenaryInstanceId; }
        public string InstanceId { get; }
        public string MonsterId { get; }
        public string DisplayName { get; }
        public string RegionId { get; }
        public string Type { get; }
        public int Level { get; }
        public int CurrentHp { get; }
        public int MaxHp { get; }
        public string State { get; }
        public int SpawnSlot { get; }
        public string TargetMercenaryInstanceId { get; }
        public int HpBps => MaxHp <= 0 ? 0 : Math.Clamp((int)((long)CurrentHp * 10000L / MaxHp), 0, 10000);
    }

    public sealed class ContinuousHuntOverviewDto
    {
        public ContinuousHuntOverviewDto(long revision, IReadOnlyList<ContinuousHuntMemberDto> members,
            IReadOnlyList<WorldHuntRegionDto> regions, IReadOnlyList<WorldHuntMonsterDto> monsters, WorldHuntFeedbackConfigDto feedback)
        { Revision = revision; Members = members; Regions = regions; Monsters = monsters; Feedback = feedback; }
        public long Revision { get; }
        public IReadOnlyList<ContinuousHuntMemberDto> Members { get; }
        public IReadOnlyList<WorldHuntRegionDto> Regions { get; }
        public IReadOnlyList<WorldHuntMonsterDto> Monsters { get; }
        public WorldHuntFeedbackConfigDto Feedback { get; }
        public int AssignedCount => Members.Count(value => value.IsAssigned);
        public long EarnedGold => Members.Sum(value => value.EarnedGold);
    }

    /// <summary>
    /// Owns ordinary hunting-ground assignment, persistent field monsters, combat, loot bags and the handoff to the store economy.
    /// Raid combat remains isolated in the P14 raid service.
    /// </summary>
    public sealed class ContinuousHuntGameService : IAppService
    {
        internal const int ExperiencePerFallbackKill = 12;
        internal const int ContributionPerKill = 8;
        public const int RuntimeStepBudget = 24;
        public const int PersistenceIntervalSeconds = 20;
        private readonly ITrustedUtcClock clock;
        private readonly AutomaticGrowthRules growthRules;
        private readonly WorldHuntRules worldRules;
        private SaveService save;
        private FacilityGameService game;
        private EconomyGameService economy;
        private ContentCatalogService content;
        private WorldHuntCatalog worldCatalog;
        private Dictionary<string, int> rankOrder;
        private Dictionary<string, SkillUnlock[]> skillsByJob;
        private Dictionary<int, EnhancementRule> enhancementRules;
        private DateTimeOffset nextDueUtc = DateTimeOffset.MinValue;
        private long observedRevision = -1;
        private bool overviewDirty;

        public ContinuousHuntGameService(ITrustedUtcClock clock, string automaticGrowthRulesJson, string worldHuntRulesJson)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            growthRules = AutomaticGrowthRules.Parse(automaticGrowthRulesJson);
            worldRules = WorldHuntRules.Parse(worldHuntRulesJson);
        }

        public int InitializationOrder => 75;
        public bool IsBootstrapped { get; private set; }
        public bool HasPendingPersistence { get; private set; }
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
            LoadCatalogs();
            JObject draft = game.Snapshot();
            bool changed = Normalize(draft, clock.UtcNow);
            changed |= EnsureWorldState(draft, clock.UtcNow);
            if (changed) Commit(draft, game.Revision, clock.UtcNow);
            IsBootstrapped = true;
            observedRevision = game.Revision;
            nextDueUtc = EarliestWorldDue(game.CurrentDocument);
            AdvanceTo(clock.UtcNow);
        }

        public ContinuousHuntOverviewDto GetOverview()
        {
            EnsureReady();
            return BuildOverview(game.CurrentDocument);
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
            int assigned = draft["payload"]!["mercenaries"]!.Children<JObject>().Count(value => value["autonomy"]!.Value<string>("assignedRegionId") == regionId && value.Value<string>("instanceId") != mercenaryInstanceId);
            if (assigned >= worldCatalog.Region(regionId).MaxActive) throw new InvalidOperationException("CONTINUOUS_HUNT_REGION_CAPACITY");
            JObject autonomy = (JObject)mercenary["autonomy"]!;
            ReleaseTarget(draft, autonomy.Value<string>("targetInstanceId"), mercenaryInstanceId);
            autonomy["assignedRegionId"] = regionId; autonomy["autoResume"] = true; autonomy["currentRegionId"] = regionId; autonomy["targetInstanceId"] = null;
            Transition(autonomy, "TRAVEL_TO_REGION", "POLICY", clock.UtcNow);
            ApplyTransient(draft); PublishPending();
        }

        public void Unassign(string mercenaryInstanceId)
        {
            EnsureReady(); JObject draft = game.Snapshot(); JObject mercenary = FindMercenary(draft, mercenaryInstanceId); JObject autonomy = (JObject)mercenary["autonomy"]!;
            ReleaseTarget(draft, autonomy.Value<string>("targetInstanceId"), mercenaryInstanceId);
            autonomy["targetInstanceId"] = null; autonomy["assignedRegionId"] = null; autonomy["autoResume"] = false; autonomy["reasonCode"] = "PLAYER_RECALL";
            if (IsTownState(autonomy.Value<string>("state"))) { autonomy["currentRegionId"] = null; Transition(autonomy, "IDLE_TOWN", "PLAYER_RECALL", clock.UtcNow); }
            ApplyTransient(draft); PublishPending();
        }

        public int AdvanceTo(DateTimeOffset now)
        {
            int steps = AdvanceInternal(now, int.MaxValue);
            PublishPending();
            return steps;
        }

        public int AdvanceBudgetedTo(DateTimeOffset now, int maximumSteps)
        {
            if (maximumSteps < 1) throw new ArgumentOutOfRangeException(nameof(maximumSteps));
            return AdvanceInternal(now, maximumSteps);
        }

        public bool HasDueWork(DateTimeOffset now)
        {
            EnsureReady();
            return nextDueUtc <= now;
        }

        public bool PublishPending()
        {
            EnsureReady();
            if (!overviewDirty) return false;
            overviewDirty = false;
            Publish();
            return true;
        }

        public bool FlushPending(DateTimeOffset now)
        {
            EnsureReady();
            if (!HasPendingPersistence) return false;
            JObject draft = game.Snapshot();
            SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, now);
            if (!result.Success)
                throw new InvalidOperationException((result.ErrorCode ?? "CONTINUOUS_HUNT_SAVE_FAILED") + ": " + string.Join(" | ", result.Report.Issues.Select(value => value.ToString())));
            game.SynchronizeCommittedDocument(result.Document);
            observedRevision = game.Revision;
            HasPendingPersistence = false;
            return true;
        }

        private int AdvanceInternal(DateTimeOffset now, int maximumSteps)
        {
            EnsureReady();
            if (observedRevision != game.Revision)
            {
                observedRevision = game.Revision;
                nextDueUtc = EarliestWorldDue(game.CurrentDocument);
            }
            if (nextDueUtc != DateTimeOffset.MinValue && nextDueUtc > now) return 0;
            JObject draft = game.Snapshot(); bool changed = Normalize(draft, now) | EnsureWorldState(draft, now); int steps = 0;
            var settled = new List<string>();
            foreach (JObject mercenary in draft["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal))
            {
                JObject autonomy = (JObject)mercenary["autonomy"]!; int memberSteps = 0;
                while (steps < maximumSteps && memberSteps < worldRules.MaximumCatchUpSteps && ShouldAdvance(autonomy) && Due(autonomy, now))
                {
                    DateTimeOffset at = NextDecision(autonomy); changed |= RespawnDue(draft, at);
                    bool reachedStore = AdvanceMember(draft, mercenary, autonomy, at);
                    memberSteps++; steps++; changed = true;
                    if (reachedStore) { settled.Add(mercenary.Value<string>("instanceId")); break; }
                }
                if (steps >= maximumSteps) break;
            }
            changed |= RespawnDue(draft, now);
            nextDueUtc = EarliestWorldDue(draft);
            if (!changed) return 0;
            ApplyTransient(draft);
            if (settled.Count > 0)
            {
                long revisionBeforeStore = game.Revision;
                try
                {
                    economy.RunStoreAutonomyCycle(DeterministicCycleId(game.ActiveProfileId, now, game.Revision), settled);
                    if (game.Revision > revisionBeforeStore) HasPendingPersistence = false;
                    observedRevision = game.Revision;
                    overviewDirty = true;
                    nextDueUtc = EarliestWorldDue(game.CurrentDocument);
                }
                catch (Exception exception) { UnityEngine.Debug.LogWarning("상점 자동 순환을 다음 귀환으로 미룹니다: " + exception.Message); }
            }
            return steps;
        }

        public void Shutdown()
        {
            if (IsBootstrapped && HasPendingPersistence)
            {
                try { FlushPending(clock.UtcNow); }
                catch (Exception exception) { UnityEngine.Debug.LogWarning("자동 사냥 종료 저장을 완료하지 못했습니다: " + exception.Message); }
            }
            Changed = null; enhancementRules = null; skillsByJob = null; rankOrder = null; worldCatalog = null; content = null; economy = null; game = null; save = null; IsBootstrapped = false;
            HasPendingPersistence = false; overviewDirty = false; nextDueUtc = DateTimeOffset.MinValue; observedRevision = -1;
        }

        internal static bool Normalize(JObject document, DateTimeOffset now)
        {
            bool changed = false; JObject payload = (JObject)document["payload"]!;
            changed |= AddIfMissing(payload, "worldHunt", new JObject { ["worldVersion"] = 1, ["nextSpawnSequence"] = 1L, ["regions"] = new JArray() });
            JObject world = (JObject)payload["worldHunt"]!;
            changed |= AddIfMissing(world, "worldVersion", 1); changed |= AddIfMissing(world, "nextSpawnSequence", 1L); changed |= AddIfMissing(world, "regions", new JArray());
            foreach (JObject mercenary in payload["mercenaries"]!.Children<JObject>())
            {
                JObject autonomy = (JObject)mercenary["autonomy"]!;
                bool oldHunting = autonomy.Value<string>("state") is "TRAVEL_TO_REGION" or "FIND_TARGET" or "COMBAT" or "LOOT" or "CONTINUE_DECISION" or "RETURN_TOWN";
                changed |= AddIfMissing(autonomy, "assignedRegionId", oldHunting ? autonomy["currentRegionId"]?.DeepClone() ?? JValue.CreateNull() : JValue.CreateNull());
                changed |= AddIfMissing(autonomy, "autoResume", true); changed |= AddIfMissing(autonomy, "currentHpBps", 10000);
                changed |= AddIfMissing(autonomy, "bagFill", 0); changed |= AddIfMissing(autonomy, "bagCapacity", 12); changed |= AddIfMissing(autonomy, "pendingSaleGold", 0L);
                changed |= AddIfMissing(autonomy, "cyclesCompleted", 0L); changed |= AddIfMissing(autonomy, "earnedGold", 0L);
                changed |= AddIfMissing(autonomy, "autoGrowthEnabled", true); changed |= AddIfMissing(autonomy, "autoSkillTraining", true);
                changed |= AddIfMissing(autonomy, "autoEquipmentEnhancement", true); changed |= AddIfMissing(autonomy, "growthPersonalGoldReserve", 50L);
                changed |= AddIfMissing(autonomy, "lastGrowthAction", "NONE"); changed |= AddIfMissing(autonomy, "lastGrowthResultCode", "NONE");
                changed |= AddIfMissing(autonomy, "lastGrowthAtUtc", JValue.CreateNull());
                changed |= AddIfMissing(autonomy, "huntBag", new JObject { ["itemStacks"] = new JArray(), ["equipment"] = new JArray() });
                changed |= AddIfMissing(autonomy, "pendingBountyGold", 0L); changed |= AddIfMissing(autonomy, "pendingLootTableId", JValue.CreateNull());
                changed |= AddIfMissing(autonomy, "pendingMonsterId", JValue.CreateNull()); changed |= AddIfMissing(autonomy, "pendingKillSequence", 0L);
                changed |= AddIfMissing(autonomy, "lastCombatDamage", 0L); changed |= AddIfMissing(autonomy, "lastSkillId", JValue.CreateNull());
                changed |= AddIfMissing(autonomy, "combatTickCount", 0L);
                changed |= AddIfMissing(mercenary, "skillGrowth", new JObject { ["growthVersion"] = 1, ["skills"] = new JArray(), ["totalSpentGold"] = 0L });
                JObject bag = (JObject)autonomy["huntBag"]!; changed |= AddIfMissing(bag, "itemStacks", new JArray()); changed |= AddIfMissing(bag, "equipment", new JArray());
                int fill = BagFill(bag), capacity = Math.Max(1, autonomy.Value<int>("bagCapacity"));
                if (autonomy.Value<int>("bagFill") != Math.Min(fill, capacity)) { autonomy["bagFill"] = Math.Min(fill, capacity); changed = true; }
                if (autonomy.Value<int>("currentHpBps") is < 0 or > 10000) { autonomy["currentHpBps"] = 10000; changed = true; }
                if (!KnownState(autonomy.Value<string>("state")))
                {
                    autonomy["assignedRegionId"] = null; autonomy["currentRegionId"] = null; autonomy["targetInstanceId"] = null;
                    SetState(autonomy, "IDLE_TOWN", "NONE", now, 2); changed = true;
                }
            }
            return changed;
        }

        private bool EnsureWorldState(JObject document, DateTimeOffset now)
        {
            bool changed = false; JObject world = (JObject)document["payload"]!["worldHunt"]!; JArray regionValues = (JArray)world["regions"]!;
            foreach (WorldHuntCatalog.RegionDefinition definition in worldCatalog.Regions)
            {
                JObject region = regionValues.Children<JObject>().SingleOrDefault(value => value.Value<string>("regionId") == definition.Id);
                if (region == null) { region = new JObject { ["regionId"] = definition.Id, ["monsters"] = new JArray() }; regionValues.Add(region); changed = true; }
                JArray values = (JArray)region["monsters"]!;
                foreach (JObject duplicate in values.Children<JObject>().GroupBy(value => value.Value<int>("spawnSlot")).SelectMany(group => group.Skip(1)).ToArray()) { duplicate.Remove(); changed = true; }
                for (int slot = 0; slot < worldRules.MonsterSlotsPerRegion; slot++)
                {
                    if (values.Children<JObject>().Any(value => value.Value<int>("spawnSlot") == slot)) continue;
                    values.Add(CreateMonster(document, definition.Id, slot, now)); changed = true;
                }
                foreach (JObject extra in values.Children<JObject>().Where(value => value.Value<int>("spawnSlot") >= worldRules.MonsterSlotsPerRegion).ToArray()) { extra.Remove(); changed = true; }
                region["monsters"] = new JArray(values.Children<JObject>().OrderBy(value => value.Value<int>("spawnSlot")));
            }
            world["regions"] = new JArray(regionValues.Children<JObject>().Where(value => worldCatalog.Regions.Any(region => region.Id == value.Value<string>("regionId")))
                .OrderBy(value => worldCatalog.Region(value.Value<string>("regionId")).Order));
            HashSet<string> monsterIds = world["regions"]!.Children<JObject>().SelectMany(value => value["monsters"]!.Children<JObject>()).Select(value => value.Value<string>("instanceId")).ToHashSet(StringComparer.Ordinal);
            foreach (JObject mercenary in document["payload"]!["mercenaries"]!.Children<JObject>())
            {
                JObject autonomy = (JObject)mercenary["autonomy"]!; string target = NullableString(autonomy["targetInstanceId"]);
                if (target != null && !monsterIds.Contains(target)) { autonomy["targetInstanceId"] = null; if (autonomy.Value<string>("state") == "COMBAT") Transition(autonomy, "FIND_TARGET", "TARGET_LOST", now); changed = true; }
            }
            return changed;
        }

        private JObject CreateMonster(JObject document, string regionId, int slot, DateTimeOffset now)
        {
            JObject world = (JObject)document["payload"]!["worldHunt"]!; long sequence = Math.Max(1, world.Value<long>("nextSpawnSequence")); world["nextSpawnSequence"] = checked(sequence + 1);
            WorldHuntCatalog.MonsterDefinition definition = worldCatalog.SelectMonster(regionId, sequence);
            return new JObject
            {
                ["instanceId"] = DeterministicUuid($"WORLD_MONSTER:{document.Value<string>("profileId")}:{regionId}:{slot}:{sequence}"),
                ["monsterId"] = definition.Id, ["currentHp"] = definition.Hp, ["maxHp"] = definition.Hp, ["state"] = "ACTIVE", ["spawnSlot"] = slot,
                ["targetMercenaryInstanceId"] = null, ["defeatedAtUtc"] = null, ["respawnAtUtc"] = null, ["killSequence"] = 0L
            };
        }

        private bool RespawnDue(JObject document, DateTimeOffset at)
        {
            bool changed = false;
            foreach (JObject region in document["payload"]!["worldHunt"]!["regions"]!.Children<JObject>().ToArray())
            foreach (JObject monster in region["monsters"]!.Children<JObject>().ToArray())
            {
                if (monster.Value<string>("state") != "RESPAWNING" || ParseNullableUtc(monster["respawnAtUtc"]) is not DateTimeOffset due || due > at) continue;
                JObject replacement = CreateMonster(document, region.Value<string>("regionId"), monster.Value<int>("spawnSlot"), at);
                monster.Replace(replacement); changed = true;
            }
            return changed;
        }

        private bool AdvanceMember(JObject document, JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            string state = autonomy.Value<string>("state"); string assigned = NullableString(autonomy["assignedRegionId"]);
            switch (state)
            {
                case "IDLE_TOWN":
                case "ENHANCE_EQUIPMENT":
                    if (state == "ENHANCE_EQUIPMENT") EnhanceEquipment(mercenary, autonomy, at);
                    if (assigned != null && autonomy.Value<bool>("autoResume")) { autonomy["currentRegionId"] = assigned; Transition(autonomy, "TRAVEL_TO_REGION", "POLICY", at); }
                    else { autonomy["currentRegionId"] = null; autonomy["targetInstanceId"] = null; Transition(autonomy, "IDLE_TOWN", "NONE", at); }
                    return false;
                case "TRAVEL_TO_REGION": Transition(autonomy, "FIND_TARGET", "ARRIVED_REGION", at); return false;
                case "FIND_TARGET":
                    JObject target = SelectTarget(document, assigned, mercenary.Value<string>("instanceId"));
                    if (target == null) { autonomy["targetInstanceId"] = null; Transition(autonomy, "FIND_TARGET", "WAITING_RESPAWN", at); }
                    else { autonomy["targetInstanceId"] = target.Value<string>("instanceId"); if (target["targetMercenaryInstanceId"]!.Type == JTokenType.Null) target["targetMercenaryInstanceId"] = mercenary.Value<string>("instanceId"); Transition(autonomy, "COMBAT", "TARGET_FOUND", at); }
                    return false;
                case "COMBAT": return AdvanceCombat(document, mercenary, autonomy, at);
                case "LOOT": ResolveLoot(document, mercenary, autonomy, at); Transition(autonomy, "CONTINUE_DECISION", "LOOT_COMPLETE", at); return false;
                case "CONTINUE_DECISION":
                    if (assigned == null || autonomy.Value<int>("currentHpBps") <= worldRules.ReturnHpBps || autonomy.Value<int>("bagFill") >= autonomy.Value<int>("bagCapacity"))
                        Transition(autonomy, "RETURN_TOWN", assigned == null ? "PLAYER_RECALL" : autonomy.Value<int>("currentHpBps") <= worldRules.ReturnHpBps ? "HP_LOW" : "INVENTORY_FULL", at);
                    else Transition(autonomy, "FIND_TARGET", "POLICY", at);
                    return false;
                case "RETURN_TOWN": ReleaseTarget(document, NullableString(autonomy["targetInstanceId"]), mercenary.Value<string>("instanceId")); autonomy["targetInstanceId"] = null; autonomy["currentRegionId"] = null; Transition(autonomy, "SELL_LOOT", "RETURNED_TO_STORE", at); return false;
                case "SELL_LOOT":
                    SettleBag(document, mercenary, autonomy); ConsumePotion(mercenary); autonomy["currentHpBps"] = 10000;
                    Transition(autonomy, "BUY_CONSUMABLES", "AUTO_SELL_ELIGIBLE", at); return true;
                case "HEAL": ConsumePotion(mercenary); autonomy["currentHpBps"] = 10000; Transition(autonomy, "BUY_CONSUMABLES", "POTION_TARGET_LOW", at); return true;
                case "BUY_CONSUMABLES": Transition(autonomy, "EVALUATE_EQUIPMENT", "POLICY", at); return false;
                case "EVALUATE_EQUIPMENT": Transition(autonomy, "BUY_EQUIPMENT", "EQUIPMENT_UPGRADE", at); return false;
                case "BUY_EQUIPMENT": Transition(autonomy, "TRAIN_SKILLS", "GROWTH_POLICY", at); return false;
                case "TRAIN_SKILLS": TrainSkill(mercenary, autonomy, at); Transition(autonomy, "ENHANCE_EQUIPMENT", "GROWTH_POLICY", at); return false;
                case "INJURED": ConsumePotion(mercenary); autonomy["currentHpBps"] = 10000; Transition(autonomy, "BUY_CONSUMABLES", "HP_LOW", at); return true;
                default: Transition(autonomy, "IDLE_TOWN", "NONE", at); return false;
            }
        }

        private bool AdvanceCombat(JObject document, JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            string actorId = mercenary.Value<string>("instanceId"); JObject monster = FindWorldMonster(document, NullableString(autonomy["targetInstanceId"]));
            if (monster == null || monster.Value<string>("state") != "ACTIVE" || monster.Value<int>("currentHp") <= 0)
            { autonomy["targetInstanceId"] = null; Transition(autonomy, "FIND_TARGET", "TARGET_LOST", at); return false; }
            WorldHuntCatalog.MonsterDefinition monsterDefinition = worldCatalog.Monster(monster.Value<string>("monsterId"));
            WorldHuntCatalog.CombatProfile job = worldCatalog.Job(mercenary.Value<string>("jobId"));
            (int equipmentHp, int equipmentAttack, int equipmentDefense) = worldCatalog.EquipmentStats(document, mercenary);
            int level = Math.Max(1, mercenary.Value<int>("level"));
            int attack = Math.Max(1, checked((int)((long)(job.Attack + equipmentAttack) * (10000L + (level - 1L) * worldRules.LevelAttackBpsPerLevel) / 10000L)));
            int defense = Math.Max(0, checked((int)((long)(job.Defense + equipmentDefense) * (10000L + (level - 1L) * worldRules.LevelDefenseBpsPerLevel) / 10000L)));
            int maximumHp = Math.Max(1, job.MaxHp + equipmentHp);
            long combatTick = checked(autonomy.Value<long>("combatTickCount") + 1); autonomy["combatTickCount"] = combatTick;
            WorldHuntCatalog.SkillDefinition skill = combatTick % worldRules.SkillCastEveryCombatTicks == 0
                ? worldCatalog.SelectActiveSkill(mercenary.Value<string>("jobId"), mercenary["skillGrowth"]!["skills"]!.Children<JObject>().Select(value => value.Value<string>("skillId"))) : null;
            int coefficient = skill?.CoefficientBps ?? 10000;
            int damage = Math.Max(1, checked((int)((long)attack * coefficient / 10000L)) - monsterDefinition.Defense);
            monster["currentHp"] = Math.Max(0, monster.Value<int>("currentHp") - damage); autonomy["lastCombatDamage"] = damage; autonomy["lastSkillId"] = skill?.Id == null ? JValue.CreateNull() : skill.Id;
            if (monster.Value<int>("currentHp") == 0)
            {
                long killSequence = checked(monster.Value<long>("killSequence") + 1); monster["killSequence"] = killSequence; monster["state"] = "RESPAWNING"; monster["targetMercenaryInstanceId"] = null;
                monster["defeatedAtUtc"] = FormatUtc(at); monster["respawnAtUtc"] = FormatUtc(at.AddSeconds(worldRules.MonsterRespawnSeconds));
                autonomy["targetInstanceId"] = null; autonomy["pendingLootTableId"] = monsterDefinition.LootTableId; autonomy["pendingMonsterId"] = monsterDefinition.Id;
                autonomy["pendingKillSequence"] = killSequence; autonomy["pendingBountyGold"] = checked(autonomy.Value<long>("pendingBountyGold") + monsterDefinition.Bounty);
                Transition(autonomy, "LOOT", "MONSTER_DEFEATED", at); return false;
            }
            int incoming = Math.Max(1, monsterDefinition.Attack - defense);
            int incomingBps = Math.Max(1, checked((int)Math.Ceiling(incoming * 10000m / maximumHp)));
            incomingBps = Math.Max(1, incomingBps * (10000 - SkillBonusBps(mercenary, growthRules.SkillHuntDamageReductionBpsPerLevel)) / 10000);
            autonomy["currentHpBps"] = Math.Max(0, autonomy.Value<int>("currentHpBps") - incomingBps);
            if (autonomy.Value<int>("currentHpBps") <= worldRules.ReturnHpBps)
            { ReleaseTarget(document, monster.Value<string>("instanceId"), actorId); autonomy["targetInstanceId"] = null; Transition(autonomy, "RETURN_TOWN", "HP_LOW", at); }
            else Transition(autonomy, "COMBAT", skill == null ? "BASIC_ATTACK" : "SKILL_CAST", at);
            return false;
        }

        private void ResolveLoot(JObject document, JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            string lootTableId = NullableString(autonomy["pendingLootTableId"]), monsterId = NullableString(autonomy["pendingMonsterId"]);
            long killSequence = autonomy.Value<long>("pendingKillSequence"); JObject bag = (JObject)autonomy["huntBag"]!; int capacity = autonomy.Value<int>("bagCapacity");
            int collected = 0; string seedRoot = $"{document.Value<string>("profileId")}:{mercenary.Value<string>("instanceId")}:{monsterId}:{killSequence}:{autonomy.Value<long>("cyclesCompleted")}";
            foreach (WorldHuntCatalog.LootDefinition entry in worldCatalog.Loot(lootTableId))
            {
                int available = capacity - BagFill(bag); if (available <= 0) break;
                string seed = seedRoot + ":" + entry.EntryNo.ToString(CultureInfo.InvariantCulture);
                if (StableRoll(seed, 10000) >= entry.ProbabilityBps) continue;
                if (entry.RewardType == "ITEM")
                {
                    int quantity = entry.MinQuantity + StableRoll(seed + ":QTY", entry.MaxQuantity - entry.MinQuantity + 1); quantity = Math.Min(quantity, available);
                    AddItemStack((JArray)bag["itemStacks"]!, entry.RewardId, quantity); collected += quantity;
                }
                else if (entry.RewardType == "RANDOM_EQUIPMENT_TIER")
                {
                    int tier = worldCatalog.Region(worldCatalog.Monster(monsterId).RegionId).Tier;
                    WorldHuntCatalog.EquipmentTemplate item = worldCatalog.SelectEquipment(mercenary.Value<string>("jobId"), tier, seed);
                    if (item != null) { ((JArray)bag["equipment"]!).Add(CreateEquipment(document, mercenary, item, seed)); collected++; }
                }
            }
            WorldHuntCatalog.MonsterDefinition defeated = monsterId == null ? null : worldCatalog.Monster(monsterId);
            autonomy["bagFill"] = Math.Min(capacity, BagFill(bag)); autonomy["cyclesCompleted"] = checked(autonomy.Value<long>("cyclesCompleted") + 1);
            mercenary["exp"] = checked(mercenary.Value<long>("exp") + (defeated?.Xp ?? ExperiencePerFallbackKill)); mercenary["contribution"] = checked(mercenary.Value<long>("contribution") + ContributionPerKill);
            mercenary["records"]!["killCount"] = checked(mercenary["records"]!.Value<long>("killCount") + 1); mercenary["records"]!["huntCount"] = checked(mercenary["records"]!.Value<long>("huntCount") + 1);
            mercenary["records"]!["itemsCollected"] = checked(mercenary["records"]!.Value<long>("itemsCollected") + collected);
            if (defeated?.Type == "ELITE") mercenary["records"]!["eliteKillCount"] = checked(mercenary["records"]!.Value<long>("eliteKillCount") + 1);
            autonomy["pendingLootTableId"] = null; autonomy["pendingMonsterId"] = null; autonomy["pendingKillSequence"] = 0L;
        }

        private JObject CreateEquipment(JObject document, JObject mercenary, WorldHuntCatalog.EquipmentTemplate item, string seed)
        {
            return new JObject
            {
                ["instanceId"] = DeterministicUuid("HUNT_EQUIPMENT:" + seed), ["equipmentTemplateId"] = item.Id, ["tier"] = item.Tier,
                ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = 0, ["refineOption"] = null, ["locked"] = false,
                ["equippedByMercenaryInstanceId"] = null, ["sourceContentVersion"] = document.Value<string>("contentVersion"),
                ["generationOperationId"] = DeterministicUuid("HUNT_LOOT_OPERATION:" + mercenary.Value<string>("instanceId") + ":" + seed),
                ["enhancementPityBps"] = 0, ["enhancementAttemptCount"] = 0L, ["enhancementMaterialInvested"] = new JArray(),
                ["pendingRefineOption"] = null, ["refineRollCount"] = 0L
            };
        }

        private static void SettleBag(JObject document, JObject mercenary, JObject autonomy)
        {
            JObject bag = (JObject)autonomy["huntBag"]!; JArray inventoryItems = (JArray)document["payload"]!["inventory"]!["itemStacks"]!;
            foreach (JObject stack in bag["itemStacks"]!.Children<JObject>()) AddItemStack(inventoryItems, stack.Value<string>("itemId"), stack.Value<long>("quantity"));
            JArray inventoryEquipment = (JArray)document["payload"]!["inventory"]!["equipment"]!;
            foreach (JObject equipment in bag["equipment"]!.Children<JObject>()) inventoryEquipment.Add(equipment.DeepClone());
            long bounty = checked(autonomy.Value<long>("pendingBountyGold") + autonomy.Value<long>("pendingSaleGold"));
            mercenary["personalGold"] = checked(mercenary.Value<long>("personalGold") + bounty); autonomy["earnedGold"] = checked(autonomy.Value<long>("earnedGold") + bounty);
            autonomy["pendingBountyGold"] = 0L; autonomy["pendingSaleGold"] = 0L; bag["itemStacks"] = new JArray(); bag["equipment"] = new JArray(); autonomy["bagFill"] = 0;
        }

        private JObject SelectTarget(JObject document, string regionId, string actorId)
        {
            if (regionId == null) return null;
            JObject[] active = document["payload"]!["worldHunt"]!["regions"]!.Children<JObject>().Single(value => value.Value<string>("regionId") == regionId)["monsters"]!
                .Children<JObject>().Where(value => value.Value<string>("state") == "ACTIVE" && value.Value<int>("currentHp") > 0)
                .OrderBy(value => value["targetMercenaryInstanceId"]!.Type == JTokenType.Null ? 0 : value.Value<string>("targetMercenaryInstanceId") == actorId ? 1 : 2)
                .ThenBy(value => value.Value<int>("spawnSlot")).ToArray();
            return active.FirstOrDefault();
        }

        private static JObject FindWorldMonster(JObject document, string instanceId) => instanceId == null ? null : document["payload"]!["worldHunt"]!["regions"]!.Children<JObject>()
            .SelectMany(value => value["monsters"]!.Children<JObject>()).SingleOrDefault(value => value.Value<string>("instanceId") == instanceId);
        private static void ReleaseTarget(JObject document, string monsterInstanceId, string actorId)
        {
            JObject monster = FindWorldMonster(document, monsterInstanceId);
            if (monster != null && NullableString(monster["targetMercenaryInstanceId"]) == actorId) monster["targetMercenaryInstanceId"] = null;
        }

        private void LoadCatalogs()
        {
            if (content.Catalog == null) throw new InvalidOperationException("CONTINUOUS_HUNT_CONTENT_NOT_READY");
            worldCatalog = new WorldHuntCatalog(content.Catalog, worldRules);
            rankOrder = content.Catalog.GetTable("mercenary_ranks.csv").Rows.Where(Enabled).ToDictionary(row => row["rank_id"], row => ParseInt(row, "order"), StringComparer.Ordinal);
            skillsByJob = content.Catalog.GetTable("job_skill_unlocks.csv").Rows.Where(Enabled).GroupBy(row => row["job_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(row => new SkillUnlock(row["skill_id"], row["unlock_rank_id"], ParseInt(row, "slot_no"))).OrderBy(value => value.Slot).ToArray(), StringComparer.Ordinal);
            enhancementRules = content.Catalog.GetTable("enhancement_rules.csv").Rows.Where(Enabled)
                .Select(row => new EnhancementRule(ParseInt(row, "target_level"), ParseBps(row, "success_chance"), row["stone_item_id"], ParseLong(row, "stone_quantity"), ParseLong(row, "personal_gold_cost"), ParseBps(row, "fail_pity_increment")))
                .ToDictionary(value => value.TargetLevel);
        }

        private void TrainSkill(JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            if (!autonomy.Value<bool>("autoGrowthEnabled") || !autonomy.Value<bool>("autoSkillTraining")) { RecordGrowth(autonomy, "NONE", "GROWTH_SKILL_DISABLED", at); return; }
            if (!skillsByJob.TryGetValue(mercenary.Value<string>("jobId"), out SkillUnlock[] unlocks) || !rankOrder.TryGetValue(mercenary.Value<string>("rankId"), out int actorRank)) { RecordGrowth(autonomy, "NONE", "GROWTH_SKILL_NOT_AVAILABLE", at); return; }
            JObject growth = (JObject)mercenary["skillGrowth"]!; JArray levels = (JArray)growth["skills"]!;
            SkillUnlock target = unlocks.Where(value => rankOrder.TryGetValue(value.UnlockRankId, out int required) && required <= actorRank)
                .OrderBy(value => CurrentSkillLevel(levels, value.SkillId)).ThenBy(value => value.Slot).FirstOrDefault(value => CurrentSkillLevel(levels, value.SkillId) < growthRules.MaxSkillLevel);
            if (target == null) { RecordGrowth(autonomy, "NONE", "GROWTH_SKILLS_MAXED", at); return; }
            int nextLevel = CurrentSkillLevel(levels, target.SkillId) + 1; long cost = growthRules.SkillCost(nextLevel); long reserve = Math.Max(growthRules.PersonalGoldReserve, autonomy.Value<long>("growthPersonalGoldReserve"));
            if (mercenary.Value<long>("personalGold") - cost < reserve) { RecordGrowth(autonomy, "NONE", "GROWTH_GOLD_RESERVED", at); return; }
            mercenary["personalGold"] = mercenary.Value<long>("personalGold") - cost; JObject line = levels.Children<JObject>().SingleOrDefault(value => value.Value<string>("skillId") == target.SkillId);
            if (line == null) { line = new JObject { ["skillId"] = target.SkillId, ["level"] = nextLevel, ["totalSpentGold"] = cost, ["trainedAtUtc"] = FormatUtc(at) }; levels.Add(line); }
            else { line["level"] = nextLevel; line["totalSpentGold"] = checked(line.Value<long>("totalSpentGold") + cost); line["trainedAtUtc"] = FormatUtc(at); }
            growth["skills"] = new JArray(levels.Children<JObject>().OrderBy(value => value.Value<string>("skillId"), StringComparer.Ordinal)); growth["totalSpentGold"] = checked(growth.Value<long>("totalSpentGold") + cost);
            RecordGrowth(autonomy, "SKILL_TRAINED", "GROWTH_SKILL_TRAINED", at);
        }

        private void EnhanceEquipment(JObject mercenary, JObject autonomy, DateTimeOffset at)
        {
            if (!autonomy.Value<bool>("autoGrowthEnabled") || !autonomy.Value<bool>("autoEquipmentEnhancement")) { RecordGrowth(autonomy, "NONE", "GROWTH_ENHANCEMENT_DISABLED", at); return; }
            JObject blacksmith = FindFacility(mercenary, "FAC_BLACKSMITH"); if (blacksmith == null || blacksmith.Value<string>("state") != "ACTIVE") { RecordGrowth(autonomy, "NONE", "GROWTH_BLACKSMITH_INACTIVE", at); return; }
            JObject document = mercenary.Root as JObject; string actorId = mercenary.Value<string>("instanceId");
            JObject equipment = document!["payload"]!["inventory"]!["equipment"]!.Children<JObject>()
                .Where(value => !value.Value<bool>("locked") && value["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null && value.Value<string>("equippedByMercenaryInstanceId") == actorId)
                .Where(value => value.Value<int>("enhancementLevel") < growthRules.MaxAutomaticEnhancementLevel)
                .OrderBy(value => value.Value<int>("enhancementLevel")).ThenBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).FirstOrDefault();
            if (equipment == null) { RecordGrowth(autonomy, "NONE", "GROWTH_EQUIPMENT_NOT_AVAILABLE", at); return; }
            int before = equipment.Value<int>("enhancementLevel");
            if (!enhancementRules.TryGetValue(before + 1, out EnhancementRule rule) || (!growthRules.AllowRiskyEnhancement && rule.ChanceBps < 10000)) { RecordGrowth(autonomy, "NONE", "GROWTH_ENHANCEMENT_POLICY_LIMIT", at); return; }
            long reserve = Math.Max(growthRules.PersonalGoldReserve, autonomy.Value<long>("growthPersonalGoldReserve")); if (mercenary.Value<long>("personalGold") - rule.Gold < reserve) { RecordGrowth(autonomy, "NONE", "GROWTH_GOLD_RESERVED", at); return; }
            JArray stacks = (JArray)document["payload"]!["inventory"]!["itemStacks"]!; JObject stack = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == rule.StoneId);
            if (stack == null || stack.Value<long>("quantity") < rule.StoneQuantity) { RecordGrowth(autonomy, "NONE", "GROWTH_MATERIAL_INSUFFICIENT", at); return; }
            mercenary["personalGold"] = mercenary.Value<long>("personalGold") - rule.Gold; long remaining = stack.Value<long>("quantity") - rule.StoneQuantity; if (remaining == 0) stack.Remove(); else stack["quantity"] = remaining;
            AddInvestment(equipment, rule.StoneId, rule.StoneQuantity); long attempt = checked(equipment.Value<long>("enhancementAttemptCount") + 1); equipment["enhancementAttemptCount"] = attempt;
            int chance = Math.Min(10000, rule.ChanceBps + equipment.Value<int>("enhancementPityBps")); bool success = chance >= 10000 || StableRoll(actorId + equipment.Value<string>("instanceId") + attempt.ToString(CultureInfo.InvariantCulture), 10000) < chance;
            if (success) { equipment["enhancementLevel"] = before + 1; equipment["enhancementPityBps"] = 0; } else equipment["enhancementPityBps"] = Math.Min(10000, checked(equipment.Value<int>("enhancementPityBps") + rule.PityIncrementBps));
            RecordGrowth(autonomy, "EQUIPMENT_ENHANCED", success ? "GROWTH_ENHANCEMENT_SUCCESS" : "GROWTH_ENHANCEMENT_FAILED", at);
        }

        private ContinuousHuntOverviewDto BuildOverview(JObject document)
        {
            ContinuousHuntMemberDto[] members = document["payload"]!["mercenaries"]!.Children<JObject>().Where(value => value.Value<bool>("active")).OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).Select(value =>
            {
                JObject autonomy = (JObject)value["autonomy"]!;
                return new ContinuousHuntMemberDto(value.Value<string>("instanceId"), value.Value<string>("displayName"), value.Value<string>("jobId"), autonomy.Value<string>("state"), NullableString(autonomy["assignedRegionId"]), NullableString(autonomy["targetInstanceId"]),
                    autonomy.Value<int>("currentHpBps"), autonomy.Value<int>("bagFill"), autonomy.Value<int>("bagCapacity"), autonomy.Value<long>("cyclesCompleted"), autonomy.Value<long>("earnedGold"), autonomy.Value<long>("pendingBountyGold"), TotalSkillLevels(value),
                    document["payload"]!["inventory"]!["equipment"]!.Children<JObject>().Where(item => NullableString(item["equippedByMercenaryInstanceId"]) == value.Value<string>("instanceId")).Select(item => item.Value<int>("enhancementLevel")).DefaultIfEmpty(0).Max(),
                    checked((int)Math.Min(int.MaxValue, autonomy.Value<long>("lastCombatDamage"))), NullableString(autonomy["lastSkillId"]), autonomy.Value<string>("lastGrowthAction"), autonomy.Value<string>("lastGrowthResultCode"));
            }).ToArray();
            var monsterDtos = new List<WorldHuntMonsterDto>();
            foreach (JObject region in document["payload"]!["worldHunt"]!["regions"]!.Children<JObject>())
            foreach (JObject monster in region["monsters"]!.Children<JObject>())
            {
                WorldHuntCatalog.MonsterDefinition definition = worldCatalog.Monster(monster.Value<string>("monsterId"));
                monsterDtos.Add(new WorldHuntMonsterDto(monster.Value<string>("instanceId"), definition.Id, definition.Name, definition.RegionId, definition.Type, definition.Level,
                    monster.Value<int>("currentHp"), monster.Value<int>("maxHp"), monster.Value<string>("state"), monster.Value<int>("spawnSlot"), NullableString(monster["targetMercenaryInstanceId"])));
            }
            WorldHuntRegionDto[] regions = worldCatalog.Regions.Select(value => new WorldHuntRegionDto(value.Id, value.Name, value.Order, value.Tier, value.RecommendedPower, value.MaxActive,
                value.Layout.WorldX, value.Layout.Theme, RegionUnlocked(document, value.Id), members.Count(member => member.AssignedRegionId == value.Id),
                worldCatalog.RiskLabel(value.Id), worldCatalog.DropPreview(value.Id))).ToArray();
            var feedback = new WorldHuntFeedbackConfigDto(worldRules.HitFlashSeconds, worldRules.SkillPulseSeconds, worldRules.DamageFloatSeconds, worldRules.RewardFeedSeconds,
                worldRules.MaximumRewardFeedEntries, worldRules.MasterVolumeBps, worldRules.HitFrequencyHz, worldRules.SkillFrequencyHz, worldRules.RewardFrequencyHz, worldRules.GrowthFrequencyHz);
            return new ContinuousHuntOverviewDto(document.Value<long>("revision"), members, regions,
                monsterDtos.OrderBy(value => worldCatalog.Region(value.RegionId).Order).ThenBy(value => value.SpawnSlot).ToArray(), feedback);
        }

        private void Commit(JObject draft, long expectedRevision, DateTimeOffset now)
        {
            SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, expectedRevision, now);
            if (!result.Success) throw new InvalidOperationException((result.ErrorCode ?? "CONTINUOUS_HUNT_SAVE_FAILED") + ": " + string.Join(" | ", result.Report.Issues.Select(value => value.ToString())));
            game.SynchronizeCommittedDocument(result.Document);
            observedRevision = game.Revision;
        }

        private void ApplyTransient(JObject draft)
        {
            game.SynchronizeTransientDocument(draft);
            observedRevision = game.Revision;
            HasPendingPersistence = true;
            overviewDirty = true;
            nextDueUtc = EarliestWorldDue(game.CurrentDocument);
        }

        private void Publish() => Changed?.Invoke(this, BuildOverview(game.CurrentDocument));
        private void Transition(JObject autonomy, string state, string reason, DateTimeOffset at) => SetState(autonomy, state, reason, at, worldRules.DecisionSeconds);
        private static void SetState(JObject autonomy, string state, string reason, DateTimeOffset at, int decisionSeconds)
        { autonomy["state"] = state; autonomy["reasonCode"] = reason; autonomy["stateStartedAtUtc"] = FormatUtc(at); autonomy["nextDecisionAtUtc"] = FormatUtc(at.AddSeconds(decisionSeconds)); }
        private static JObject FindMercenary(JObject document, string id) => document["payload"]!["mercenaries"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id) ?? throw new InvalidOperationException("CONTINUOUS_HUNT_MERCENARY_NOT_FOUND");
        private static JObject FindFacility(JObject mercenary, string facilityId) => (mercenary.Root as JObject)?["payload"]?["facilities"]?.Children<JObject>().SingleOrDefault(value => value.Value<string>("facilityId") == facilityId);
        private static bool RegionUnlocked(JObject document, string regionId) => document["payload"]!["regions"]!["progress"]!.Children<JObject>().Any(value => value.Value<string>("regionId") == regionId && value.Value<bool>("unlocked"));
        private static bool AddIfMissing(JObject target, string property, object value) { if (target[property] != null) return false; target[property] = value is JToken token ? token : JToken.FromObject(value); return true; }
        private static bool KnownState(string state) => state is "IDLE_TOWN" or "PREPARE" or "TRAVEL_TO_REGION" or "FIND_TARGET" or "COMBAT" or "LOOT" or "CONTINUE_DECISION" or "RETURN_TOWN" or "SELL_LOOT" or "HEAL" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" or "TRAIN_SKILLS" or "ENHANCE_EQUIPMENT" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED" or "RAID_READY";
        private static bool IsTownState(string state) => state is "IDLE_TOWN" or "SELL_LOOT" or "HEAL" or "BUY_CONSUMABLES" or "EVALUATE_EQUIPMENT" or "BUY_EQUIPMENT" or "TRAIN_SKILLS" or "ENHANCE_EQUIPMENT" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED" or "RAID_READY";
        private static bool ShouldAdvance(JObject autonomy) => autonomy["assignedRegionId"]!.Type != JTokenType.Null || autonomy.Value<string>("state") is not "IDLE_TOWN";
        private static DateTimeOffset NextDecision(JObject autonomy) => DateTimeOffset.TryParse(autonomy.Value<string>("nextDecisionAtUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset value) ? value : DateTimeOffset.MinValue;
        private static bool Due(JObject autonomy, DateTimeOffset now) => NextDecision(autonomy) <= now;
        private static DateTimeOffset EarliestWorldDue(JObject document)
        {
            if (document == null) return DateTimeOffset.MaxValue;
            IEnumerable<DateTimeOffset> memberDue = document["payload"]!["mercenaries"]!.Children<JObject>()
                .Select(value => (JObject)value["autonomy"]!)
                .Where(ShouldAdvance)
                .Select(NextDecision);
            IEnumerable<DateTimeOffset> monsterDue = document["payload"]!["worldHunt"]!["regions"]!.Children<JObject>()
                .SelectMany(value => value["monsters"]!.Children<JObject>())
                .Where(value => value.Value<string>("state") == "RESPAWNING")
                .Select(value => ParseNullableUtc(value["respawnAtUtc"]))
                .Where(value => value.HasValue)
                .Select(value => value.Value);
            return memberDue.Concat(monsterDue).DefaultIfEmpty(DateTimeOffset.MaxValue).Min();
        }
        private static DateTimeOffset? ParseNullableUtc(JToken value) => value == null || value.Type == JTokenType.Null ? null : DateTimeOffset.TryParse(value.Value<string>(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed) ? parsed : null;
        private static string NullableString(JToken token) => token == null || token.Type == JTokenType.Null ? null : token.Value<string>();
        private int SkillBonusBps(JObject mercenary, int perLevel) => Math.Min(growthRules.MaximumSkillHuntBonusBps, checked(TotalSkillLevels(mercenary) * perLevel));
        private static int TotalSkillLevels(JObject mercenary) => mercenary["skillGrowth"]?["skills"]?.Children<JObject>().Sum(value => value.Value<int>("level")) ?? 0;
        private static int CurrentSkillLevel(JArray values, string skillId) => values.Children<JObject>().SingleOrDefault(value => value.Value<string>("skillId") == skillId)?.Value<int>("level") ?? 0;
        private static int BagFill(JObject bag) => checked((int)Math.Min(100, bag["itemStacks"]!.Children<JObject>().Sum(value => value.Value<long>("quantity")) + bag["equipment"]!.Count()));
        private static void AddItemStack(JArray values, string itemId, long quantity) { if (quantity <= 0) return; JObject line = values.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == itemId); if (line == null) values.Add(new JObject { ["itemId"] = itemId, ["quantity"] = quantity }); else line["quantity"] = checked(line.Value<long>("quantity") + quantity); }
        private static void ConsumePotion(JObject mercenary) { JObject line = mercenary["potions"]!.Children<JObject>().FirstOrDefault(value => value.Value<string>("potionId") == "POT_HEAL_SMALL" && value.Value<long>("quantity") > 0); if (line == null) return; long remaining = line.Value<long>("quantity") - 1; if (remaining == 0) line.Remove(); else line["quantity"] = remaining; }
        private static void RecordGrowth(JObject autonomy, string action, string result, DateTimeOffset at) { autonomy["lastGrowthAction"] = action; autonomy["lastGrowthResultCode"] = result; autonomy["lastGrowthAtUtc"] = FormatUtc(at); }
        private static void AddInvestment(JObject equipment, string itemId, long quantity) { JArray values = (JArray)equipment["enhancementMaterialInvested"]!; JObject line = values.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == itemId); if (line == null) values.Add(new JObject { ["itemId"] = itemId, ["quantity"] = quantity }); else line["quantity"] = checked(line.Value<long>("quantity") + quantity); equipment["enhancementMaterialInvested"] = new JArray(values.Children<JObject>().OrderBy(value => value.Value<string>("itemId"), StringComparer.Ordinal)); }
        private static int StableRoll(string seed, int upperExclusive) { if (upperExclusive <= 1) return 0; unchecked { uint hash = 2166136261; foreach (char value in seed ?? string.Empty) { hash ^= value; hash *= 16777619; } return (int)(hash % (uint)upperExclusive); } }
        private static Guid DeterministicCycleId(string profileId, DateTimeOffset at, long revision) => Guid.Parse(DeterministicUuid($"WORLD_HUNT_CYCLE:{profileId}:{at.ToUniversalTime():O}:{revision}"));
        private static string DeterministicUuid(string seed)
        {
            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(seed)).Take(16).ToArray(); bytes[6] = (byte)((bytes[6] & 0x0f) | 0x70); bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            var value = new StringBuilder(36); for (int index = 0; index < bytes.Length; index++) { if (index is 4 or 6 or 8 or 10) value.Append('-'); value.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture)); } return value.ToString();
        }
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int ParseInt(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long ParseLong(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static int ParseBps(IReadOnlyDictionary<string, string> row, string key) => checked((int)Math.Round(decimal.Parse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture) * 10000m, MidpointRounding.AwayFromZero));
        private void EnsureReady() { if (!IsBootstrapped || game == null || save == null || worldCatalog == null) throw new InvalidOperationException("CONTINUOUS_HUNT_NOT_READY"); }

        private sealed class SkillUnlock
        {
            public SkillUnlock(string skillId, string unlockRankId, int slot) { SkillId = skillId; UnlockRankId = unlockRankId; Slot = slot; }
            public string SkillId { get; } public string UnlockRankId { get; } public int Slot { get; }
        }
        private sealed class EnhancementRule
        {
            public EnhancementRule(int targetLevel, int chanceBps, string stoneId, long stoneQuantity, long gold, int pityIncrementBps) { TargetLevel = targetLevel; ChanceBps = chanceBps; StoneId = stoneId; StoneQuantity = stoneQuantity; Gold = gold; PityIncrementBps = pityIncrementBps; }
            public int TargetLevel { get; } public int ChanceBps { get; } public string StoneId { get; } public long StoneQuantity { get; } public long Gold { get; } public int PityIncrementBps { get; }
        }
        private sealed class AutomaticGrowthRules
        {
            private readonly Dictionary<int, long> skillCosts;
            private AutomaticGrowthRules(JObject value)
            {
                if (value.Value<int>("rulesVersion") != 1) throw new InvalidOperationException("AUTOMATIC_GROWTH_RULE_VERSION_INVALID");
                PersonalGoldReserve = value.Value<long>("personalGoldReserve"); MaxSkillLevel = value.Value<int>("maxSkillLevel"); MaxAutomaticEnhancementLevel = value.Value<int>("maxAutomaticEnhancementLevel"); AllowRiskyEnhancement = value.Value<bool>("allowRiskyEnhancement");
                SkillHuntDamageReductionBpsPerLevel = value.Value<int>("skillHuntDamageReductionBpsPerLevel"); SkillHuntGoldBonusBpsPerLevel = value.Value<int>("skillHuntGoldBonusBpsPerLevel"); MaximumSkillHuntBonusBps = value.Value<int>("maximumSkillHuntBonusBps");
                skillCosts = value["skillCosts"]!.Children<JObject>().ToDictionary(item => item.Value<int>("targetLevel"), item => item.Value<long>("personalGoldCost"));
                if (PersonalGoldReserve < 0 || MaxSkillLevel is < 1 or > 3 || MaxAutomaticEnhancementLevel is < 0 or > 10 || skillCosts.Count != MaxSkillLevel) throw new InvalidOperationException("AUTOMATIC_GROWTH_RULE_INVALID");
            }
            public long PersonalGoldReserve { get; } public int MaxSkillLevel { get; } public int MaxAutomaticEnhancementLevel { get; } public bool AllowRiskyEnhancement { get; }
            public int SkillHuntDamageReductionBpsPerLevel { get; } public int SkillHuntGoldBonusBpsPerLevel { get; } public int MaximumSkillHuntBonusBps { get; }
            public long SkillCost(int targetLevel) => skillCosts.TryGetValue(targetLevel, out long value) ? value : throw new InvalidOperationException("AUTOMATIC_GROWTH_SKILL_COST_MISSING");
            public static AutomaticGrowthRules Parse(string json) { if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Automatic growth rules are required.", nameof(json)); return new AutomaticGrowthRules(JObject.Parse(json)); }
        }
    }
}
