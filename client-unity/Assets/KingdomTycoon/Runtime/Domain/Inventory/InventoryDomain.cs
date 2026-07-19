using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KingdomTycoon.Domain.Combat;

namespace KingdomTycoon.Domain.Inventory
{
    public sealed class InventoryDomainException : InvalidOperationException
    {
        public InventoryDomainException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public sealed class LootEntry
    {
        public LootEntry(int entryNo, string rewardType, string rewardId, int probabilityMillionths, int minimum, int maximum, bool enabled = true)
        {
            if (entryNo <= 0 || probabilityMillionths is < 0 or > 1_000_000 || minimum <= 0 || maximum < minimum)
                throw new InventoryDomainException("P07_REWARD_UNION_INVALID");
            EntryNo = entryNo; RewardType = rewardType; RewardId = rewardId;
            ProbabilityMillionths = probabilityMillionths; Minimum = minimum; Maximum = maximum; Enabled = enabled;
        }
        public int EntryNo { get; }
        public string RewardType { get; }
        public string RewardId { get; }
        public int ProbabilityMillionths { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public bool Enabled { get; }
    }

    public sealed class LootLine
    {
        public LootLine(int entryNo, string rewardType, string rewardId, int quantity, int ordinal)
        { EntryNo = entryNo; RewardType = rewardType; RewardId = rewardId; Quantity = quantity; Ordinal = ordinal; }
        public int EntryNo { get; }
        public string RewardType { get; }
        public string RewardId { get; }
        public int Quantity { get; }
        public int Ordinal { get; }
    }

    public static class LootDeterminism
    {
        public static ulong Seed(string contentVersion, Guid huntOperationId, int encounterIndex)
        {
            if (encounterIndex < 0) throw new ArgumentOutOfRangeException(nameof(encounterIndex));
            string source = $"KT|P07_LOOT_V1|{contentVersion}|{huntOperationId:D}|{encounterIndex.ToString(CultureInfo.InvariantCulture)}";
            using SHA256 sha = SHA256.Create();
            return BinaryPrimitives.ReadUInt64BigEndian(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(source)).AsSpan(0, 8));
        }

        public static IReadOnlyList<LootLine> Resolve(IReadOnlyList<LootEntry> entries, SplitMix64 random)
        {
            if (entries == null || random == null) throw new ArgumentNullException();
            var result = new List<LootLine>();
            int ordinal = 0;
            foreach (LootEntry entry in entries.Where(value => value.Enabled).OrderBy(value => value.EntryNo))
            {
                ulong probability = random.NextBounded(1_000_000);
                if (probability >= (ulong)entry.ProbabilityMillionths) continue;
                int quantity = entry.Minimum;
                if (entry.Minimum != entry.Maximum)
                    quantity += (int)random.NextBounded((ulong)(entry.Maximum - entry.Minimum + 1));
                result.Add(new LootLine(entry.EntryNo, entry.RewardType, entry.RewardId, quantity, ordinal++));
            }
            return result;
        }
    }

    public sealed class InventoryCapacity
    {
        public InventoryCapacity(int itemStackSlots, int equipmentSlots, int potionStackSlots, int huntBufferSlots)
        {
            if (itemStackSlots < 0 || equipmentSlots < 0 || potionStackSlots < 0 || huntBufferSlots < 0)
                throw new InventoryDomainException("P07_CAPACITY_EXCEEDED");
            ItemStackSlots = itemStackSlots; EquipmentSlots = equipmentSlots;
            PotionStackSlots = potionStackSlots; HuntBufferSlots = huntBufferSlots;
        }
        public int ItemStackSlots { get; }
        public int EquipmentSlots { get; }
        public int PotionStackSlots { get; }
        public int HuntBufferSlots { get; }
        public void Require(int itemStacks, int equipment, int potionStacks, int bufferedLines)
        {
            if (itemStacks > ItemStackSlots || equipment > EquipmentSlots || potionStacks > PotionStackSlots || bufferedLines > HuntBufferSlots)
                throw new InventoryDomainException(bufferedLines > HuntBufferSlots ? "P07_HUNT_BUFFER_FULL" : "P07_CAPACITY_EXCEEDED");
        }
    }

    public sealed class EquipmentDefinition
    {
        public EquipmentDefinition(string id, int tier, string slot, string profile, int basePower, string source)
        { Id = id; Tier = tier; Slot = slot; Profile = profile; BasePower = basePower; Source = source; }
        public string Id { get; }
        public int Tier { get; }
        public string Slot { get; }
        public string Profile { get; }
        public int BasePower { get; }
        public string Source { get; }
    }

    public sealed class EquipmentInstance
    {
        public EquipmentInstance(string instanceId, EquipmentDefinition definition, string qualityId, int qualityBps, int enhancementLevel, string refineOption, bool locked)
            : this(instanceId, definition, qualityId, qualityBps, enhancementLevel, refineOption, string.IsNullOrEmpty(refineOption) ? 0 : 1000, locked)
        {
        }

        public EquipmentInstance(string instanceId, EquipmentDefinition definition, string qualityId, int qualityBps, int enhancementLevel, string refineOptionId, int refineValueBps, bool locked)
        {
            if (enhancementLevel is < 0 or > 10) throw new InventoryDomainException("P07_ENHANCEMENT_LEVEL_INVALID");
            if (refineValueBps is < 0 or > 10_000 || (string.IsNullOrEmpty(refineOptionId) != (refineValueBps == 0))) throw new InventoryDomainException("P07_REFINE_INVALID");
            InstanceId = instanceId; Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            QualityId = qualityId; QualityBps = qualityBps; EnhancementLevel = enhancementLevel;
            RefineOptionId = refineOptionId; RefineValueBps = refineValueBps; Locked = locked;
        }
        public string InstanceId { get; }
        public EquipmentDefinition Definition { get; }
        public string QualityId { get; }
        public int QualityBps { get; }
        public int EnhancementLevel { get; }
        public string RefineOption => RefineOptionId;
        public string RefineOptionId { get; }
        public int RefineValueBps { get; }
        public bool Locked { get; }
    }

    public sealed class EquipmentStatBlock
    {
        public int MaxHp { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public int HealPower { get; set; }
        public int CritChance { get; set; }
        public int MoveSpeed { get; set; }
        public int StatusPower { get; set; }
        public int StatusResist { get; set; }
        public EquipmentStatBlock Add(EquipmentStatBlock other)
        {
            MaxHp = checked(MaxHp + other.MaxHp); Attack = checked(Attack + other.Attack);
            Defense = checked(Defense + other.Defense); HealPower = checked(HealPower + other.HealPower);
            CritChance = checked(CritChance + other.CritChance); MoveSpeed = checked(MoveSpeed + other.MoveSpeed);
            StatusPower = checked(StatusPower + other.StatusPower); StatusResist = checked(StatusResist + other.StatusResist);
            return this;
        }
    }

    public static class EquipmentMath
    {
        private static readonly IReadOnlyDictionary<string, HashSet<string>> Eligible = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["JOB_WARRIOR"] = new(StringComparer.Ordinal) { "SWORD", "HEAVY", "POWER" },
            ["JOB_GUARDIAN"] = new(StringComparer.Ordinal) { "HAMMER_SHIELD", "HEAVY", "GUARD" },
            ["JOB_ARCHER"] = new(StringComparer.Ordinal) { "BOW", "LIGHT", "POWER" },
            ["JOB_MAGE"] = new(StringComparer.Ordinal) { "STAFF", "CLOTH", "WISDOM" },
            ["JOB_CLERIC"] = new(StringComparer.Ordinal) { "MACE", "CLOTH", "WISDOM" }
        };

        public static int EffectivePower(EquipmentInstance item)
        {
            long qualityPower = (long)item.Definition.BasePower * item.QualityBps / 10_000;
            return checked((int)(qualityPower * (10_000 + item.EnhancementLevel * 500L) / 10_000));
        }

        public static EquipmentStatBlock FlatModifier(string jobId, EquipmentInstance item)
        {
            if (!Eligible.TryGetValue(jobId, out HashSet<string> profiles) || !profiles.Contains(item.Definition.Profile))
                throw new InventoryDomainException("P07_EQUIPMENT_JOB_INELIGIBLE");
            if (item.Definition.Source is not ("BOSS" or "CRAFT"))
                throw new InventoryDomainException("P07_EQUIPMENT_PROFILE_INVALID");
            int power = EffectivePower(item);
            var stats = new EquipmentStatBlock();
            switch (item.Definition.Profile)
            {
                case "SWORD": stats.Attack = power; break;
                case "HAMMER_SHIELD": stats.Attack = power * 60 / 100; stats.Defense = power * 40 / 100; break;
                case "BOW": stats.Attack = power * 95 / 100; stats.CritChance = Math.Min(1000, power * 5); break;
                case "STAFF": stats.Attack = power * 90 / 100; stats.StatusPower = Math.Min(1500, power * 6); break;
                case "MACE": stats.Attack = power * 65 / 100; stats.HealPower = power * 55 / 100; break;
                case "HEAVY": stats.MaxHp = power * 4; stats.Defense = power * 50 / 100; break;
                case "LIGHT": stats.MaxHp = power * 3; stats.Defense = power * 33 / 100; stats.MoveSpeed = Math.Min(500, power); break;
                case "CLOTH": stats.MaxHp = power * 2; stats.Defense = power * 25 / 100; stats.StatusPower = Math.Min(1000, power * 3); break;
                case "POWER": stats.Attack = power * 70 / 100; stats.CritChance = Math.Min(800, power * 4); break;
                case "GUARD": stats.MaxHp = power * 3; stats.Defense = power * 50 / 100; stats.StatusResist = Math.Min(1000, power * 3); break;
                case "WISDOM": stats.HealPower = power * 80 / 100; stats.StatusPower = Math.Min(1200, power * 4); break;
                default: throw new InventoryDomainException("P07_EQUIPMENT_PROFILE_INVALID");
            }
            ApplyRefine(stats, item.RefineOptionId, item.RefineValueBps);
            return stats;
        }

        public static long Score(string jobId, EquipmentInstance item, IReadOnlyDictionary<string, int> weights)
        {
            EquipmentStatBlock stats = FlatModifier(jobId, item);
            long primary = stats.Attack + stats.HealPower + stats.StatusPower;
            long secondary = stats.CritChance + stats.MoveSpeed;
            long survival = stats.MaxHp + stats.Defense * 10L + stats.StatusResist;
            long refine = string.IsNullOrEmpty(item.RefineOption) ? 0 : EffectivePower(item);
            long boss = item.Definition.Source == "BOSS" ? EffectivePower(item) : 0;
            return primary * Weight(weights, "primary_weight_bps") + secondary * Weight(weights, "secondary_weight_bps") +
                   survival * Weight(weights, "survival_weight_bps") + refine * Weight(weights, "refine_weight_bps") +
                   boss * Weight(weights, "boss_weight_bps");
        }

        private static int Weight(IReadOnlyDictionary<string, int> weights, string key) =>
            weights.TryGetValue(key, out int value) ? value : throw new InventoryDomainException("P07_CONTENT_MISSING");

        private static void ApplyRefine(EquipmentStatBlock stats, string refine, int valueBps)
        {
            if (string.IsNullOrEmpty(refine)) return;
            switch (refine)
            {
                case "ATTACK": case "REF_ATK_POWER": stats.Attack += Math.Max(1, checked(stats.Attack * valueBps / 10_000)); break;
                case "DEFENSE": case "REF_DEF": stats.Defense += Math.Max(1, checked(stats.Defense * valueBps / 10_000)); break;
                case "HP": case "REF_HP": stats.MaxHp += Math.Max(1, checked(stats.MaxHp * valueBps / 10_000)); break;
                case "CRIT": case "REF_CRIT": stats.CritChance = checked(stats.CritChance + valueBps); break;
                case "FIRE": case "FROST": case "BOSS": case "PART": case "POISON": case "MATERIAL": case "RARE":
                case "REF_FIRE": case "REF_FROST": case "REF_BOSS": case "REF_PART": case "REF_POISON": case "REF_MATERIAL": case "REF_RARE_FIND": break;
                default: throw new InventoryDomainException("P07_REFINE_INVALID");
            }
        }
    }

    public sealed class InventoryPolicy
    {
        public int AutoSellMaxTier { get; set; }
        public bool AutoEquipEnabled { get; set; } = true;
        public bool AutoSellEnabled { get; set; } = true;
        public int UpgradeThresholdBps { get; set; } = 500;
        public bool ProtectBossEquipment { get; set; } = true;
        public bool ProtectFirstDiscovery { get; set; } = true;
        public ISet<string> ProtectQualityIds { get; } = new SortedSet<string>(StringComparer.Ordinal);
        public ISet<string> DiscoveredEquipmentTemplateIds { get; } = new SortedSet<string>(StringComparer.Ordinal);
        public void Validate()
        {
            if (AutoSellMaxTier is < 0 or > 5 || UpgradeThresholdBps is < 0 or > 10_000)
                throw new InventoryDomainException("P07_POLICY_INVALID");
        }
    }
}
