using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;

namespace KingdomTycoon.Infrastructure.Facilities
{
    public sealed class FacilityDefinition
    {
        public FacilityDefinition(string id, string nameKey, string operationMode, string requiredProfessionId)
        {
            Id = id;
            NameKey = nameKey;
            OperationMode = operationMode;
            RequiredProfessionId = requiredProfessionId;
        }
        public string Id { get; }
        public string NameKey { get; }
        public string OperationMode { get; }
        public string RequiredProfessionId { get; }
        public bool IsManaged => OperationMode == "MANAGED";
    }

    public sealed class FacilityLevelDefinition
    {
        public FacilityLevelDefinition(string facilityId, int level, string stageId, long gold, int durationSeconds, string effectKey, string effectTextKey, IReadOnlyDictionary<string, long> materials)
        {
            FacilityId = facilityId;
            Level = level;
            StageId = stageId;
            Gold = gold;
            DurationSeconds = durationSeconds;
            EffectKey = effectKey;
            EffectTextKey = effectTextKey;
            Materials = materials;
        }
        public string FacilityId { get; }
        public int Level { get; }
        public string StageId { get; }
        public long Gold { get; }
        public int DurationSeconds { get; }
        public string EffectKey { get; }
        public string EffectTextKey { get; }
        public IReadOnlyDictionary<string, long> Materials { get; }
    }

    public sealed class CanonicalFacilityCatalog
    {
        private readonly Dictionary<string, FacilityDefinition> facilities;
        private readonly Dictionary<string, FacilityLevelDefinition> levels;
        private readonly Dictionary<string, int> stageOrder;
        private readonly Dictionary<string, string> localizations;
        private readonly Dictionary<string, string> addresses;
        private readonly Dictionary<string, string> itemNameKeys;

        public CanonicalFacilityCatalog(ContentCatalog catalog, string locale = "ko-KR")
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            facilities = catalog.GetTable("facilities.csv").Rows.Where(Enabled).ToDictionary(
                row => row["facility_id"],
                row => new FacilityDefinition(row["facility_id"], row["name_text_key"], row["operation_mode"], EmptyToNull(row["required_profession_id"])),
                StringComparer.Ordinal);
            stageOrder = catalog.GetTable("kingdom_stages.csv").Rows.Where(Enabled).ToDictionary(
                row => row["stage_id"], row => int.Parse(row["order"], CultureInfo.InvariantCulture), StringComparer.Ordinal);
            var durations = catalog.GetTable("facility_construction_rules.csv").Rows.Where(Enabled).ToDictionary(
                Key, row => int.Parse(row["build_or_upgrade_duration_seconds"], CultureInfo.InvariantCulture), StringComparer.Ordinal);
            var materials = catalog.GetTable("facility_upgrade_materials.csv").Rows.Where(Enabled)
                .GroupBy(Key, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyDictionary<string, long>)group.ToDictionary(row => row["item_id"], row => long.Parse(row["quantity"], CultureInfo.InvariantCulture), StringComparer.Ordinal),
                    StringComparer.Ordinal);
            levels = catalog.GetTable("facility_levels.csv").Rows.Where(Enabled).ToDictionary(
                Key,
                row => new FacilityLevelDefinition(
                    row["facility_id"],
                    int.Parse(row["level"], CultureInfo.InvariantCulture),
                    row["required_kingdom_stage_id"],
                    long.Parse(row["build_or_upgrade_kingdom_gold"], CultureInfo.InvariantCulture),
                    durations[Key(row)],
                    row["effect_key"],
                    row["effect_text_key"],
                    materials.TryGetValue(Key(row), out IReadOnlyDictionary<string, long> value) ? value : new Dictionary<string, long>()),
                StringComparer.Ordinal);
            localizations = catalog.GetTable("localizations.csv").Rows.Where(row => Enabled(row) && row["locale"] == locale)
                .ToDictionary(row => row["text_key"], row => row["text_value"], StringComparer.Ordinal);
            addresses = catalog.GetTable("facility_world_assets.csv").Rows
                .Where(row => Enabled(row) && !string.IsNullOrEmpty(row["facility_id"]))
                .ToDictionary(row => row["facility_id"], row => row["address"], StringComparer.Ordinal);
            itemNameKeys = catalog.GetTable("items.csv").Rows.Where(Enabled)
                .ToDictionary(row => row["item_id"], row => row["name_text_key"], StringComparer.Ordinal);
            if (facilities.Count != 8 || levels.Count != 32)
            {
                throw new InvalidOperationException("CSV_FACILITY_CONSTRUCTION_COVERAGE_INVALID");
            }
        }

        public IReadOnlyList<FacilityDefinition> Facilities => facilities.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
        public FacilityDefinition GetFacility(string facilityId) => facilities.TryGetValue(facilityId, out FacilityDefinition value) ? value : throw new KeyNotFoundException(facilityId);
        public FacilityLevelDefinition GetLevel(string facilityId, int level) => levels.TryGetValue(facilityId + "\u001f" + level.ToString(CultureInfo.InvariantCulture), out FacilityLevelDefinition value) ? value : throw new KeyNotFoundException(facilityId + "/" + level);
        public bool IsStageAvailable(string currentStageId, string requiredStageId) => stageOrder[currentStageId] >= stageOrder[requiredStageId];
        public string Text(string key) => key != null && localizations.TryGetValue(key, out string value) ? value : key ?? string.Empty;
        public string Name(string facilityId) => Text(GetFacility(facilityId).NameKey);
        public string Address(string facilityId) => addresses[facilityId];
        public string ItemName(string itemId) => itemNameKeys.TryGetValue(itemId, out string key) ? Text(key) : itemId;

        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static string Key(IReadOnlyDictionary<string, string> row) => row["facility_id"] + "\u001f" + row["level"];
        private static string EmptyToNull(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
