using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Domain.Mercenaries
{
    public sealed class MercenaryDomainException : InvalidOperationException
    {
        public MercenaryDomainException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public interface IMercenaryCatalogRules
    {
        bool HasJob(string id);
        bool HasGrade(string id);
        bool HasRank(string id);
        bool HasPersonality(string id);
        bool HasTrait(string id);
        bool HasPotion(string id);
        bool IsTraitEligible(string traitId, string jobId);
        bool IsEquipmentEligible(string templateId, string jobId);
        int GradeOrder(string id);
        int RankOrder(string id);
        int JobOrder(string id);
        int GradeTraitSlots(string id);
        int RankTraitSlots(string id);
        int RankMaxLevel(string id);
        string Localize(string textKey);
    }

    public sealed class Mercenary
    {
        public Mercenary(
            string instanceId,
            string displayName,
            string jobId,
            string gradeId,
            string rankId,
            int level,
            long exp,
            string personalityId,
            IReadOnlyList<string> traitIds,
            long personalGold,
            long contribution,
            bool active,
            string autonomyState,
            string reasonCode,
            string currentRegionId,
            string promotionStatus,
            IReadOnlyDictionary<string, string> equipmentSlots,
            IReadOnlyDictionary<string, long> potionStacks,
            IReadOnlyDictionary<string, long> records)
        {
            InstanceId = instanceId;
            DisplayName = displayName;
            JobId = jobId;
            GradeId = gradeId;
            RankId = rankId;
            Level = level;
            Exp = exp;
            PersonalityId = personalityId;
            TraitIds = traitIds;
            PersonalGold = personalGold;
            Contribution = contribution;
            Active = active;
            AutonomyState = autonomyState;
            ReasonCode = reasonCode;
            CurrentRegionId = currentRegionId;
            PromotionStatus = promotionStatus;
            EquipmentSlots = equipmentSlots;
            PotionStacks = potionStacks;
            Records = records;
        }

        public string InstanceId { get; }
        public string DisplayName { get; }
        public string JobId { get; }
        public string GradeId { get; }
        public string RankId { get; }
        public int Level { get; }
        public long Exp { get; }
        public string PersonalityId { get; }
        public IReadOnlyList<string> TraitIds { get; }
        public long PersonalGold { get; }
        public long Contribution { get; }
        public bool Active { get; }
        public string AutonomyState { get; }
        public string ReasonCode { get; }
        public string CurrentRegionId { get; }
        public string PromotionStatus { get; }
        public IReadOnlyDictionary<string, string> EquipmentSlots { get; }
        public IReadOnlyDictionary<string, long> PotionStacks { get; }
        public IReadOnlyDictionary<string, long> Records { get; }
    }

    public sealed class MercenaryInvariantValidator
    {
        private static readonly HashSet<string> States = new(StringComparer.Ordinal)
        {
            "IDLE_TOWN", "PREPARE", "TRAVEL_TO_REGION", "FIND_TARGET", "COMBAT", "LOOT", "CONTINUE_DECISION",
            "RETURN_TOWN", "SELL_LOOT", "HEAL", "BUY_CONSUMABLES", "EVALUATE_EQUIPMENT", "BUY_EQUIPMENT",
            "PROMOTION_READY", "PROMOTION_PROCESS", "INJURED", "RAID_READY"
        };

        private static readonly HashSet<string> Reasons = new(StringComparer.Ordinal)
        {
            "NONE", "HP_LOW", "POTION_LOW", "INVENTORY_FULL", "SURVIVAL_LOW", "PLAYER_RECALL", "POLICY",
            "TARGET_FOUND", "LOOT_COMPLETE", "PROMOTION_AVAILABLE"
        };

        private static readonly string[] Slots = { "WEAPON", "ARMOR", "HELMET", "ACCESSORY" };
        private static readonly string[] LegacyRecordFields = { "huntCount", "killCount", "raidClearCount", "itemsCollected" };
        private static readonly string[] P11RecordFields = { "huntCount", "killCount", "raidClearCount", "itemsCollected", "region2BattleCount", "eliteKillCount", "bossContributionCount" };

        public void Validate(JObject document, IMercenaryCatalogRules catalog)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            JToken payload = document["payload"] ?? throw Error("SAVE_MERCENARY_GENERATION_INVALID");
            var mercenaries = payload["mercenaries"]!.Children<JObject>().ToArray();
            string[] ids = mercenaries.Select(value => value.Value<string>("instanceId")).ToArray();
            Require(ids.All(IsUuidV7) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "SAVE_MERCENARY_ID_INVALID");
            Require(ids.SequenceEqual(ids.OrderBy(value => value, StringComparer.Ordinal)), "SAVE_MERCENARY_ID_INVALID");

            int activeLimit = payload["kingdom"]!.Value<int>("activeMercenaryLimit");
            int ownedLimit = payload["kingdom"]!.Value<int>("ownedMercenaryLimit");
            Require(mercenaries.Length <= ownedLimit && mercenaries.Count(value => value.Value<bool>("active")) <= activeLimit, "SAVE_MERCENARY_SLOT_LIMIT_EXCEEDED");
            ValidateLodge(payload, activeLimit, ownedLimit);

            var equipment = payload["inventory"]!["equipment"]!.Children<JObject>()
                .ToDictionary(value => value.Value<string>("instanceId"), value => value, StringComparer.Ordinal);
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject mercenary in mercenaries)
            {
                ValidateOne(mercenary, catalog, equipment, occupied);
            }
            foreach (JObject item in equipment.Values)
            {
                if (item["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null)
                    Require(occupied.Contains(item.Value<string>("instanceId")), "SAVE_MERCENARY_EQUIPMENT_LINK_INVALID");
            }
        }

        private static void ValidateOne(JObject value, IMercenaryCatalogRules catalog, IReadOnlyDictionary<string, JObject> equipment, ISet<string> occupied)
        {
            foreach (string field in new[] { "generationProfileId", "nameSeed", "appearanceSeed", "growthSeed" })
                Require(!string.IsNullOrWhiteSpace(value.Value<string>(field)), "SAVE_MERCENARY_GENERATION_INVALID");
            foreach (string field in new[] { "nameSeed", "appearanceSeed", "growthSeed" })
            {
                string seed = value.Value<string>(field);
                Require(ulong.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed) && parsed.ToString(CultureInfo.InvariantCulture) == seed, "SAVE_MERCENARY_GENERATION_INVALID");
            }

            string job = value.Value<string>("jobId");
            string grade = value.Value<string>("gradeId");
            string rank = value.Value<string>("rankId");
            string personality = value.Value<string>("personalityId");
            string[] traits = value["traitIds"]!.Values<string>().ToArray();
            Require(catalog.HasJob(job) && catalog.HasGrade(grade) && catalog.HasRank(rank) && catalog.HasPersonality(personality) && traits.All(catalog.HasTrait), "SAVE_MERCENARY_CONTENT_REF_INVALID");
            Require(grade is "GRADE_C" or "GRADE_B" or "GRADE_A" or "GRADE_S" or "GRADE_SS", "MERCENARY_GRADE_IMMUTABLE");
            Require(value.Value<int>("level") >= 1 && value.Value<int>("level") <= catalog.RankMaxLevel(rank) && value.Value<long>("exp") >= 0, "SAVE_MERCENARY_LEVEL_INVALID");
            Require(traits.Distinct(StringComparer.Ordinal).Count() == traits.Length && traits.Length <= catalog.GradeTraitSlots(grade) + catalog.RankTraitSlots(rank) && traits.All(trait => catalog.IsTraitEligible(trait, job)), "SAVE_MERCENARY_TRAITS_INVALID");
            Require(value.Value<long>("personalGold") >= 0 && value.Value<long>("contribution") >= 0, "SAVE_MERCENARY_COUNTER_INVALID");
            ValidateName(value.Value<string>("displayName"));
            ValidateAutonomy(value, value["autonomy"] as JObject);
            ValidatePotions(value["potions"] as JArray, catalog);
            ValidateEquipment(value, value["equipmentSlots"] as JObject, job, catalog, equipment, occupied);
            ValidatePromotion(value["promotion"] as JObject);
            ValidateRecords(value["records"] as JObject);
        }

        private static void ValidateAutonomy(JObject mercenary, JObject autonomy)
        {
            Require(autonomy != null && States.Contains(autonomy.Value<string>("state")) && Reasons.Contains(autonomy.Value<string>("reasonCode")), "SAVE_MERCENARY_AUTONOMY_INVALID");
            Require(IsUtc(autonomy.Value<string>("stateStartedAtUtc")) && IsUtc(autonomy.Value<string>("nextDecisionAtUtc")), "SAVE_MERCENARY_AUTONOMY_INVALID");
            string state = autonomy.Value<string>("state");
            bool townSafe = state is "IDLE_TOWN" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED";
            if (!mercenary.Value<bool>("active"))
                Require(townSafe && IsNull(autonomy["currentRegionId"]) && IsNull(autonomy["targetInstanceId"]), "SAVE_MERCENARY_INACTIVE_STATE_INVALID");
            if (autonomy["targetInstanceId"]!.Type != JTokenType.Null)
                Require(IsUuidV7(autonomy.Value<string>("targetInstanceId")), "SAVE_MERCENARY_AUTONOMY_INVALID");
            if (state is "IDLE_TOWN" or "PROMOTION_READY" or "PROMOTION_PROCESS" or "INJURED")
                Require(IsNull(autonomy["currentRegionId"]), "SAVE_MERCENARY_AUTONOMY_INVALID");
        }

        private static void ValidatePotions(JArray potions, IMercenaryCatalogRules catalog)
        {
            Require(potions != null, "SAVE_MERCENARY_POTION_INVALID");
            string[] ids = potions.Children<JObject>().Select(value => value.Value<string>("potionId")).ToArray();
            Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length && potions.Children<JObject>().All(value => catalog.HasPotion(value.Value<string>("potionId")) && value.Value<long>("quantity") > 0), "SAVE_MERCENARY_POTION_INVALID");
        }

        private static void ValidateEquipment(JObject mercenary, JObject slots, string job, IMercenaryCatalogRules catalog, IReadOnlyDictionary<string, JObject> equipment, ISet<string> occupied)
        {
            Require(slots != null && slots.Properties().Select(property => property.Name).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(Slots.OrderBy(value => value, StringComparer.Ordinal)), "SAVE_MERCENARY_EQUIPMENT_LINK_INVALID");
            foreach (JProperty slot in slots.Properties())
            {
                if (slot.Value.Type == JTokenType.Null) continue;
                string id = slot.Value.Value<string>();
                Require(equipment.TryGetValue(id, out JObject item) && item.Value<string>("equippedByMercenaryInstanceId") == mercenary.Value<string>("instanceId") && occupied.Add(id) && catalog.IsEquipmentEligible(item.Value<string>("equipmentTemplateId"), job), "SAVE_MERCENARY_EQUIPMENT_LINK_INVALID");
            }
        }

        private static void ValidatePromotion(JObject promotion)
        {
            Require(promotion != null, "SAVE_MERCENARY_PROMOTION_INVALID");
            string status = promotion.Value<string>("status");
            bool target = !IsNull(promotion["targetRankId"]);
            bool operation = !IsNull(promotion["operationId"]);
            bool started = !IsNull(promotion["startedAtUtc"]);
            bool finishes = !IsNull(promotion["finishesAtUtc"]);
            bool cost = !IsNull(promotion["costSnapshot"]);
            bool valid = status switch
            {
                "NONE" => !target && !operation && !started && !finishes && !cost,
                "READY" => target && !operation && !started && !finishes && !cost,
                "IN_REVIEW" => target && operation && started && finishes && cost,
                "COMPLETED_PENDING_APPLY" => target && operation && started && finishes && cost,
                _ => false
            };
            Require(valid, "SAVE_MERCENARY_PROMOTION_INVALID");
        }

        private static void ValidateRecords(JObject records)
        {
            Require(records != null, "SAVE_MERCENARY_RECORD_INVALID");
            string[] actual = records.Properties().Select(value => value.Name).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] expected = actual.Length == P11RecordFields.Length ? P11RecordFields : LegacyRecordFields;
            Require(actual.SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)) && expected.All(field => records.Value<long>(field) >= 0), "SAVE_MERCENARY_RECORD_INVALID");
        }

        private static void ValidateName(string value)
        {
            Require(value != null && value == value.Trim() && ScalarCount(value) is >= 1 and <= 20 && !value.Any(char.IsControl), "SAVE_MERCENARY_NAME_INVALID");
        }

        private static int ScalarCount(string value)
        {
            int count = 0;
            for (int index = 0; index < value.Length; index++, count++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    Require(index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]), "SAVE_MERCENARY_NAME_INVALID");
                    index++;
                }
                else Require(!char.IsLowSurrogate(value[index]), "SAVE_MERCENARY_NAME_INVALID");
            }
            return count;
        }

        private static void ValidateLodge(JToken payload, int active, int owned)
        {
            JObject lodge = payload["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_LODGE");
            (int expectedActive, int expectedOwned) = lodge.Value<int>("level") switch { 1 => (4, 8), 2 => (8, 12), 3 => (12, 18), 4 => (16, 24), _ => (-1, -1) };
            Require(active == expectedActive && owned == expectedOwned, "SAVE_MERCENARY_LODGE_LIMIT_MISMATCH");
        }

        private static bool IsUuidV7(string value) => Guid.TryParseExact(value, "D", out _) && value.Length == 36 && value[14] == '7' && "89abAB".IndexOf(value[19]) >= 0;
        private static bool IsUtc(string value) => DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _);
        private static bool IsNull(JToken value) => value == null || value.Type == JTokenType.Null;
        private static MercenaryDomainException Error(string code) => new(code);
        private static void Require(bool condition, string code) { if (!condition) throw Error(code); }
    }

    public static class MercenaryActivityPolicy
    {
        public static void RequireAllowed(Mercenary mercenary, bool desiredActive, int activeCount, int activeLimit)
        {
            if (mercenary.Active == desiredActive) return;
            if (desiredActive)
            {
                if (mercenary.AutonomyState != "IDLE_TOWN" || mercenary.PromotionStatus == "IN_REVIEW") throw new MercenaryDomainException("MERCENARY_ACTIVE_CHANGE_STATE_FORBIDDEN");
                if (activeCount >= activeLimit) throw new MercenaryDomainException("MERCENARY_ACTIVE_LIMIT_REACHED");
                return;
            }
            if (mercenary.AutonomyState is not ("IDLE_TOWN" or "PROMOTION_READY" or "INJURED") || mercenary.CurrentRegionId != null || mercenary.PromotionStatus == "IN_REVIEW")
                throw new MercenaryDomainException("MERCENARY_ACTIVE_CHANGE_STATE_FORBIDDEN");
        }
    }

    public static class MercenaryRosterFilter
    {
        private static readonly CultureInfo KoreanCulture = CultureInfo.GetCultureInfo("ko-KR");

        public static bool Matches(Mercenary item, string search, ISet<string> jobs, ISet<string> grades, ISet<string> ranks, ISet<string> states, string active, bool promotionOnly, bool injuryOnly)
        {
            string needle = Normalize(search);
            return (needle.Length == 0 || Normalize(item.DisplayName).Contains(needle, StringComparison.Ordinal))
                && (jobs.Count == 0 || jobs.Contains(item.JobId))
                && (grades.Count == 0 || grades.Contains(item.GradeId))
                && (ranks.Count == 0 || ranks.Contains(item.RankId))
                && (states.Count == 0 || states.Contains(item.AutonomyState))
                && (active == "ALL" || (active == "ACTIVE") == item.Active)
                && (!promotionOnly || item.PromotionStatus == "READY" || item.AutonomyState == "PROMOTION_READY")
                && (!injuryOnly || item.AutonomyState == "INJURED");
        }

        public static string Normalize(string value) => (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormC).ToUpper(KoreanCulture);
    }

    public sealed class MercenaryRosterComparer : IComparer<Mercenary>
    {
        private readonly string sortId;
        private readonly IMercenaryCatalogRules catalog;
        public MercenaryRosterComparer(string sortId, IMercenaryCatalogRules catalog) { this.sortId = sortId ?? "DEFAULT"; this.catalog = catalog; }

        public int Compare(Mercenary x, Mercenary y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            int result = sortId switch
            {
                "NAME_ASC" => Text(x.DisplayName, y.DisplayName),
                "NAME_DESC" => Text(y.DisplayName, x.DisplayName),
                "LEVEL_ASC" => Chain(x.Level.CompareTo(y.Level), catalog.RankOrder(x.RankId).CompareTo(catalog.RankOrder(y.RankId))),
                "LEVEL_DESC" => Chain(y.Level.CompareTo(x.Level), catalog.RankOrder(y.RankId).CompareTo(catalog.RankOrder(x.RankId))),
                "GRADE_ASC" => Chain(catalog.GradeOrder(x.GradeId).CompareTo(catalog.GradeOrder(y.GradeId)), catalog.RankOrder(x.RankId).CompareTo(catalog.RankOrder(y.RankId)), x.Level.CompareTo(y.Level)),
                "GRADE_DESC" => Chain(catalog.GradeOrder(y.GradeId).CompareTo(catalog.GradeOrder(x.GradeId)), catalog.RankOrder(y.RankId).CompareTo(catalog.RankOrder(x.RankId)), y.Level.CompareTo(x.Level)),
                "RANK_ASC" => Chain(catalog.RankOrder(x.RankId).CompareTo(catalog.RankOrder(y.RankId)), x.Level.CompareTo(y.Level), catalog.GradeOrder(x.GradeId).CompareTo(catalog.GradeOrder(y.GradeId))),
                "RANK_DESC" => Chain(catalog.RankOrder(y.RankId).CompareTo(catalog.RankOrder(x.RankId)), y.Level.CompareTo(x.Level), catalog.GradeOrder(y.GradeId).CompareTo(catalog.GradeOrder(x.GradeId))),
                _ => Chain(y.Active.CompareTo(x.Active), catalog.JobOrder(x.JobId).CompareTo(catalog.JobOrder(y.JobId)), catalog.GradeOrder(y.GradeId).CompareTo(catalog.GradeOrder(x.GradeId)), catalog.RankOrder(y.RankId).CompareTo(catalog.RankOrder(x.RankId)), y.Level.CompareTo(x.Level), Text(x.DisplayName, y.DisplayName))
            };
            return result != 0 ? result : string.CompareOrdinal(x.InstanceId, y.InstanceId);
        }

        private static int Text(string x, string y) => string.CompareOrdinal(MercenaryRosterFilter.Normalize(x), MercenaryRosterFilter.Normalize(y));
        private static int Chain(params int[] values) => values.FirstOrDefault(value => value != 0);
    }
}
