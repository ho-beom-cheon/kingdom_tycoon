using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Combat
{
    internal sealed class WorldHuntRules
    {
        private readonly Dictionary<string, RegionLayout> layouts;

        private WorldHuntRules(JObject value)
        {
            if (value.Value<int>("rulesVersion") != 1) throw new InvalidOperationException("WORLD_HUNT_RULE_VERSION_INVALID");
            DecisionSeconds = value.Value<int>("decisionSeconds");
            MonsterSlotsPerRegion = value.Value<int>("monsterSlotsPerRegion");
            MonsterRespawnSeconds = value.Value<int>("monsterRespawnSeconds");
            ReturnHpBps = value.Value<int>("returnHpBps");
            DefaultBagCapacity = value.Value<int>("defaultBagCapacity");
            MaximumCatchUpSteps = value.Value<int>("maximumCatchUpSteps");
            SkillCastEveryCombatTicks = value.Value<int>("skillCastEveryCombatTicks");
            LevelAttackBpsPerLevel = value.Value<int>("levelAttackBpsPerLevel");
            LevelDefenseBpsPerLevel = value.Value<int>("levelDefenseBpsPerLevel");
            JObject feedback = value["feedback"] as JObject ?? throw new InvalidOperationException("WORLD_HUNT_FEEDBACK_RULE_MISSING");
            HitFlashSeconds = feedback.Value<float>("hitFlashSeconds");
            SkillPulseSeconds = feedback.Value<float>("skillPulseSeconds");
            DamageFloatSeconds = feedback.Value<float>("damageFloatSeconds");
            RewardFeedSeconds = feedback.Value<float>("rewardFeedSeconds");
            MaximumRewardFeedEntries = feedback.Value<int>("maximumRewardFeedEntries");
            MasterVolumeBps = feedback.Value<int>("masterVolumeBps");
            HitFrequencyHz = feedback.Value<int>("hitFrequencyHz");
            SkillFrequencyHz = feedback.Value<int>("skillFrequencyHz");
            RewardFrequencyHz = feedback.Value<int>("rewardFrequencyHz");
            GrowthFrequencyHz = feedback.Value<int>("growthFrequencyHz");
            layouts = value["regions"]!.Children<JObject>().Select(item => new RegionLayout(
                item.Value<string>("regionId"), item.Value<int>("worldX"), item.Value<string>("theme")))
                .ToDictionary(item => item.RegionId, StringComparer.Ordinal);
            if (DecisionSeconds is < 1 or > 10 || MonsterSlotsPerRegion is < 2 or > 8 || MonsterRespawnSeconds is < 1 or > 120 ||
                ReturnHpBps is < 500 or > 9000 || DefaultBagCapacity is < 4 or > 100 || MaximumCatchUpSteps is < 20 or > 1000 ||
                SkillCastEveryCombatTicks is < 1 or > 20 || HitFlashSeconds is < .05f or > 1f || SkillPulseSeconds is < .1f or > 2f ||
                DamageFloatSeconds is < .2f or > 3f || RewardFeedSeconds is < 1f or > 10f || MaximumRewardFeedEntries is < 1 or > 5 ||
                MasterVolumeBps is < 0 or > 5000 || HitFrequencyHz is < 80 or > 2000 || SkillFrequencyHz is < 80 or > 2000 ||
                RewardFrequencyHz is < 80 or > 2000 || GrowthFrequencyHz is < 80 or > 2000 || layouts.Count != 5)
                throw new InvalidOperationException("WORLD_HUNT_RULE_INVALID");
        }

        public int DecisionSeconds { get; }
        public int MonsterSlotsPerRegion { get; }
        public int MonsterRespawnSeconds { get; }
        public int ReturnHpBps { get; }
        public int DefaultBagCapacity { get; }
        public int MaximumCatchUpSteps { get; }
        public int SkillCastEveryCombatTicks { get; }
        public int LevelAttackBpsPerLevel { get; }
        public int LevelDefenseBpsPerLevel { get; }
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
        public RegionLayout Layout(string regionId) => layouts.TryGetValue(regionId, out RegionLayout value)
            ? value : throw new InvalidOperationException("WORLD_HUNT_REGION_LAYOUT_MISSING");

        public static WorldHuntRules Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("World hunt rules are required.", nameof(json));
            return new WorldHuntRules(JObject.Parse(json));
        }

        internal sealed class RegionLayout
        {
            public RegionLayout(string regionId, int worldX, string theme) { RegionId = regionId; WorldX = worldX; Theme = theme; }
            public string RegionId { get; }
            public int WorldX { get; }
            public string Theme { get; }
        }
    }

    internal sealed class WorldHuntCatalog
    {
        private readonly Dictionary<string, RegionDefinition> regions;
        private readonly Dictionary<string, MonsterDefinition> monsters;
        private readonly Dictionary<string, EncounterDefinition[]> encounters;
        private readonly Dictionary<string, LootDefinition[]> loot;
        private readonly Dictionary<string, CombatProfile> jobs;
        private readonly Dictionary<string, SkillDefinition> skills;
        private readonly Dictionary<string, SkillUnlock[]> skillUnlocks;
        private readonly Dictionary<string, EquipmentTemplate> equipment;
        private readonly HashSet<string> equipmentEligibility;
        private readonly Dictionary<string, int> qualityBps;
        private readonly Dictionary<string, string> names;
        private readonly Dictionary<string, string> itemNames;
        private readonly WorldHuntRules rules;

        public WorldHuntCatalog(ContentCatalog catalog, WorldHuntRules rules)
        {
            if (catalog == null) throw new InvalidOperationException("WORLD_HUNT_CONTENT_NOT_READY");
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            names = catalog.GetTable("localizations.csv").Rows.Where(row => Enabled(row) && row["locale"] == "ko-KR")
                .ToDictionary(row => row["text_key"], row => row["text_value"], StringComparer.Ordinal);
            itemNames = catalog.GetTable("items.csv").Rows.Where(Enabled)
                .ToDictionary(row => row["item_id"], row => Name(row["name_text_key"], "전리품"), StringComparer.Ordinal);
            regions = catalog.GetTable("regions.csv").Rows.Where(Enabled).Select(row => new RegionDefinition(
                    row["region_id"], Name(row["name_text_key"], row["region_id"]), Int(row, "order"), Int(row, "tier"),
                    Int(row, "recommended_power"), Int(row, "max_active"), rules.Layout(row["region_id"])))
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            monsters = catalog.GetTable("monsters.csv").Rows.Where(row => Enabled(row) && string.IsNullOrEmpty(row["raid_id"]))
                .Select(row => new MonsterDefinition(row["monster_id"], Name(row["name_text_key"], row["monster_id"]), row["region_id"],
                    row["type"], Int(row, "level"), Int(row, "hp"), Int(row, "attack"), Int(row, "defense"), Int(row, "xp"),
                    Long(row, "bounty_personal_gold"), row["loot_table_id"], Int(row, "spawn_weight")))
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            encounters = catalog.GetTable("region_encounter_profiles.csv").Rows.Where(Enabled)
                .Where(row => monsters.ContainsKey(row["monster_id"]))
                .GroupBy(row => row["region_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(row => new EncounterDefinition(row["monster_id"], Int(row, "weight")))
                    .OrderBy(value => value.MonsterId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            loot = catalog.GetTable("loot_entries.csv").Rows.Where(Enabled)
                .GroupBy(row => row["loot_table_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(row => new LootDefinition(Int(row, "entry_no"), row["reward_type"],
                    row["reward_id"], ProbabilityBps(row["probability"]), Int(row, "min_quantity"), Int(row, "max_quantity")))
                    .OrderBy(value => value.EntryNo).ToArray(), StringComparer.Ordinal);
            jobs = catalog.GetTable("combat_job_profiles.csv").Rows.Where(Enabled)
                .Select(row => new CombatProfile(row["job_id"], Int(row, "max_hp"), Int(row, "attack"), Int(row, "defense")))
                .ToDictionary(value => value.JobId, StringComparer.Ordinal);
            skills = catalog.GetTable("skill_runtime_rules.csv").Rows.Where(Enabled)
                .Select(row => new SkillDefinition(row["skill_id"], Int(row, "coefficient_bps"), row["effect_type"]))
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            skillUnlocks = catalog.GetTable("job_skill_unlocks.csv").Rows.Where(Enabled)
                .GroupBy(row => row["job_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(row => new SkillUnlock(row["skill_id"], Int(row, "slot_no")))
                    .OrderBy(value => value.Slot).ToArray(), StringComparer.Ordinal);
            equipment = catalog.GetTable("equipment_templates.csv").Rows.Where(Enabled)
                .Select(row => new EquipmentTemplate(row["equipment_template_id"], Name(row["name_text_key"], row["equipment_template_id"]),
                    Int(row, "tier"), row["slot"], row["profile"], Int(row, "base_power")))
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            equipmentEligibility = catalog.GetTable("equipment_job_eligibility.csv").Rows.Where(Enabled)
                .Select(row => row["equipment_template_id"] + "\0" + row["job_id"]).ToHashSet(StringComparer.Ordinal);
            qualityBps = catalog.GetTable("equipment_qualities.csv").Rows.Where(Enabled).ToDictionary(row => row["quality_id"],
                row => checked((int)Math.Round(decimal.Parse(row["stat_multiplier"], CultureInfo.InvariantCulture) * 10000m)), StringComparer.Ordinal);
            if (regions.Count != 5 || monsters.Count != 25 || jobs.Count != 5 || encounters.Count != 5)
                throw new InvalidOperationException("WORLD_HUNT_CONTENT_INCOMPLETE");
        }

        public IEnumerable<RegionDefinition> Regions => regions.Values.OrderBy(value => value.Order);
        public RegionDefinition Region(string id) => regions.TryGetValue(id, out RegionDefinition value) ? value : throw new InvalidOperationException("WORLD_HUNT_REGION_MISSING");
        public MonsterDefinition Monster(string id) => monsters.TryGetValue(id, out MonsterDefinition value) ? value : throw new InvalidOperationException("WORLD_HUNT_MONSTER_MISSING");
        public IReadOnlyList<LootDefinition> Loot(string id) => loot.TryGetValue(id, out LootDefinition[] value) ? value : Array.Empty<LootDefinition>();
        public CombatProfile Job(string id) => jobs.TryGetValue(id, out CombatProfile value) ? value : throw new InvalidOperationException("WORLD_HUNT_JOB_MISSING");
        public string RiskLabel(string regionId) => Region(regionId).Tier switch { 1 => "안정", 2 => "주의", 3 => "위험", 4 => "고위험", _ => "극한" };
        public string DropPreview(string regionId)
        {
            string[] values = encounters[regionId].Select(value => Monster(value.MonsterId).LootTableId)
                .SelectMany(value => Loot(value)).OrderByDescending(value => value.ProbabilityBps).ThenBy(value => value.EntryNo)
                .Select(value => value.RewardType == "RANDOM_EQUIPMENT_TIER" ? "장비" : itemNames.GetValueOrDefault(value.RewardId, "전리품"))
                .Distinct(StringComparer.Ordinal).Take(2).ToArray();
            return values.Length == 0 ? "전리품 정보 없음" : string.Join(" · ", values);
        }

        public MonsterDefinition SelectMonster(string regionId, long sequence)
        {
            EncounterDefinition[] values = encounters[regionId];
            int total = values.Sum(value => value.Weight);
            int roll = StableRoll(regionId + ":" + sequence.ToString(CultureInfo.InvariantCulture), total);
            foreach (EncounterDefinition encounter in values)
            {
                if (roll < encounter.Weight) return Monster(encounter.MonsterId);
                roll -= encounter.Weight;
            }
            return Monster(values[^1].MonsterId);
        }

        public SkillDefinition SelectActiveSkill(string jobId, IEnumerable<string> learnedSkillIds)
        {
            HashSet<string> learned = (learnedSkillIds ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
            if (!skillUnlocks.TryGetValue(jobId, out SkillUnlock[] values)) return null;
            return values.Where(value => learned.Contains(value.SkillId)).Select(value => skills.GetValueOrDefault(value.SkillId))
                .FirstOrDefault(value => value != null && value.CoefficientBps > 0 && value.EffectType.Contains("DAMAGE", StringComparison.Ordinal));
        }

        public EquipmentTemplate SelectEquipment(string jobId, int tier, string seed)
        {
            EquipmentTemplate[] candidates = equipment.Values.Where(value => value.Tier == tier && equipmentEligibility.Contains(value.Id + "\0" + jobId))
                .OrderBy(value => value.Slot == "WEAPON" ? 0 : 1).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
            return candidates.Length == 0 ? null : candidates[StableRoll(seed, candidates.Length)];
        }

        public (int MaxHp, int Attack, int Defense) EquipmentStats(JObject document, JObject mercenary)
        {
            var byId = document["payload"]!["inventory"]!["equipment"]!.Children<JObject>()
                .ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal);
            int hp = 0, attack = 0, defense = 0;
            foreach (JProperty slot in ((JObject)mercenary["equipmentSlots"]!).Properties())
            {
                if (slot.Value.Type == JTokenType.Null || !byId.TryGetValue(slot.Value.Value<string>(), out JObject stored)) continue;
                if (!equipment.TryGetValue(stored.Value<string>("equipmentTemplateId"), out EquipmentTemplate item)) continue;
                int quality = qualityBps.GetValueOrDefault(stored.Value<string>("qualityId"), 10000);
                int power = checked((int)((long)item.BasePower * quality * (10000L + stored.Value<int>("enhancementLevel") * 500L) / 100000000L));
                switch (item.Profile)
                {
                    case "SWORD": attack += power; break;
                    case "HAMMER_SHIELD": attack += power * 60 / 100; defense += power * 40 / 100; break;
                    case "BOW": attack += power * 95 / 100; break;
                    case "STAFF": attack += power * 90 / 100; break;
                    case "MACE": attack += power * 65 / 100; break;
                    case "HEAVY": hp += power * 4; defense += power * 50 / 100; break;
                    case "LIGHT": hp += power * 3; defense += power * 33 / 100; break;
                    case "CLOTH": hp += power * 2; defense += power * 25 / 100; break;
                    case "POWER": attack += power * 70 / 100; break;
                    case "GUARD": hp += power * 3; defense += power * 50 / 100; break;
                    case "WISDOM": attack += power * 30 / 100; break;
                }
            }
            return (hp, attack, defense);
        }

        private string Name(string textKey, string fallback) => names.GetValueOrDefault(textKey, fallback);
        private static int StableRoll(string seed, int upperExclusive)
        {
            if (upperExclusive <= 1) return 0;
            unchecked
            {
                uint hash = 2166136261;
                foreach (char value in seed) { hash ^= value; hash *= 16777619; }
                return (int)(hash % (uint)upperExclusive);
            }
        }
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static int ProbabilityBps(string value) => checked((int)Math.Round(decimal.Parse(value, CultureInfo.InvariantCulture) * 10000m));

        internal sealed class RegionDefinition
        {
            public RegionDefinition(string id, string name, int order, int tier, int recommendedPower, int maxActive, WorldHuntRules.RegionLayout layout)
            { Id = id; Name = name; Order = order; Tier = tier; RecommendedPower = recommendedPower; MaxActive = maxActive; Layout = layout; }
            public string Id { get; }
            public string Name { get; }
            public int Order { get; }
            public int Tier { get; }
            public int RecommendedPower { get; }
            public int MaxActive { get; }
            public WorldHuntRules.RegionLayout Layout { get; }
        }
        internal sealed class MonsterDefinition
        {
            public MonsterDefinition(string id, string name, string regionId, string type, int level, int hp, int attack, int defense, int xp, long bounty, string lootTableId, int spawnWeight)
            { Id = id; Name = name; RegionId = regionId; Type = type; Level = level; Hp = hp; Attack = attack; Defense = defense; Xp = xp; Bounty = bounty; LootTableId = lootTableId; SpawnWeight = spawnWeight; }
            public string Id { get; }
            public string Name { get; }
            public string RegionId { get; }
            public string Type { get; }
            public int Level { get; }
            public int Hp { get; }
            public int Attack { get; }
            public int Defense { get; }
            public int Xp { get; }
            public long Bounty { get; }
            public string LootTableId { get; }
            public int SpawnWeight { get; }
        }
        internal sealed class LootDefinition
        {
            public LootDefinition(int entryNo, string rewardType, string rewardId, int probabilityBps, int minQuantity, int maxQuantity)
            { EntryNo = entryNo; RewardType = rewardType; RewardId = rewardId; ProbabilityBps = probabilityBps; MinQuantity = minQuantity; MaxQuantity = maxQuantity; }
            public int EntryNo { get; }
            public string RewardType { get; }
            public string RewardId { get; }
            public int ProbabilityBps { get; }
            public int MinQuantity { get; }
            public int MaxQuantity { get; }
        }
        internal sealed class CombatProfile
        {
            public CombatProfile(string jobId, int maxHp, int attack, int defense) { JobId = jobId; MaxHp = maxHp; Attack = attack; Defense = defense; }
            public string JobId { get; }
            public int MaxHp { get; }
            public int Attack { get; }
            public int Defense { get; }
        }
        internal sealed class SkillDefinition
        {
            public SkillDefinition(string id, int coefficientBps, string effectType) { Id = id; CoefficientBps = coefficientBps; EffectType = effectType; }
            public string Id { get; }
            public int CoefficientBps { get; }
            public string EffectType { get; }
        }
        internal sealed class EquipmentTemplate
        {
            public EquipmentTemplate(string id, string name, int tier, string slot, string profile, int basePower)
            { Id = id; Name = name; Tier = tier; Slot = slot; Profile = profile; BasePower = basePower; }
            public string Id { get; }
            public string Name { get; }
            public int Tier { get; }
            public string Slot { get; }
            public string Profile { get; }
            public int BasePower { get; }
        }
        private sealed class EncounterDefinition
        {
            public EncounterDefinition(string monsterId, int weight) { MonsterId = monsterId; Weight = weight; }
            public string MonsterId { get; }
            public int Weight { get; }
        }
        private sealed class SkillUnlock
        {
            public SkillUnlock(string skillId, int slot) { SkillId = skillId; Slot = slot; }
            public string SkillId { get; }
            public int Slot { get; }
        }
    }
}
