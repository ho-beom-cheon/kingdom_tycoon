using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Infrastructure;

namespace KingdomTycoon.Infrastructure.Content
{
    internal static class ContentSemanticValidator
    {
        private static readonly HashSet<string> OfflineTypes = new(StringComparer.Ordinal)
        {
            "HUNT", "FACILITY", "NPC_PROFICIENCY", "POTION_CONSUMPTION", "INJURY_RECOVERY", "PROMOTION_REVIEW"
        };

        public static void Validate(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            ValidateEquipmentSource(tables, report);
            ValidateItemSource(tables, report);
            ValidateAutonomy(tables, report);
            ValidateRaidDifficulties(tables, report);
            ValidateOfflineRules(tables, report);
            ValidateTutorialDag(tables, report);
            ValidateConditionGroups(tables, report);
            ValidateRuntimeConfig(tables, report);
            ValidateRandomEquipment(tables, report);
            ValidateFacilityConstruction(tables, report);
            ValidateFacilityWorldAssets(tables, report);
        }

        private static void ValidateEquipmentSource(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("equipment_templates.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0 || !table.Rows[0].ContainsKey("source") || !table.Rows[0].ContainsKey("boss_id")) return;

            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyDictionary<string, string> row = table.Rows[index];
                bool valid = row["source"] == "BOSS" ? row["boss_id"].Length > 0 : row["boss_id"].Length == 0;
                if (!valid)
                {
                    Error(report, "CSV_EQUIPMENT_SOURCE_INVALID", table, index, "source/boss_id tagged union is invalid.");
                }
            }
        }

        private static void ValidateItemSource(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("items.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0 || !table.Rows[0].ContainsKey("source_type") || !table.Rows[0].ContainsKey("source_id")) return;

            foreach ((IReadOnlyDictionary<string, string> row, int index) in table.Rows.Select((row, index) => (row, index)))
            {
                string sourceType = row["source_type"];
                string sourceId = row["source_id"];
                bool valid = sourceType switch
                {
                    "REGION" => HasEnabledId(tables, "regions.csv", "region_id", sourceId),
                    "RAID" => HasEnabledId(tables, "raids.csv", "raid_id", sourceId),
                    "DISMANTLE" or "ELITE_AND_RAID" or "PROMOTION_CONTENT" => sourceId.Length == 0,
                    _ => false
                };
                if (!valid)
                {
                    Error(report, "CSV_ITEM_SOURCE_INVALID", table, index, "source_type/source_id tagged union is invalid.");
                }
            }
        }

        private static void ValidateAutonomy(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("autonomy_rules.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0) return;

            List<IReadOnlyDictionary<string, string>> rows = Enabled(table).ToList();
            HashSet<string> states = rows.Select(row => row["state"]).ToHashSet(StringComparer.Ordinal);
            bool valid = rows.Count == 37 && states.Count == 17;
            foreach (IGrouping<string, IReadOnlyDictionary<string, string>> group in rows.GroupBy(row => row["state"], StringComparer.Ordinal))
            {
                valid &= group.Select(row => row["priority"]).Distinct(StringComparer.Ordinal).Count() == group.Count();
                valid &= group.Count(row => row["priority"] == "0") == 1;
                valid &= group.All(row => states.Contains(row["next_state"]));
            }

            if (!valid)
            {
                report.AddError("CSV_AUTONOMY_RULE_INVALID", table.FileName, "/", "Enabled autonomy catalog must contain 17 states, 37 rules, unique priorities, and one fallback per state.");
            }
        }

        private static void ValidateRaidDifficulties(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("raid_difficulties.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0) return;

            string[] expected = { "NORMAL", "HARD", "CORRUPTED" };
            bool valid = Enabled(table).Count() == 6;
            foreach (IGrouping<string, IReadOnlyDictionary<string, string>> group in Enabled(table).GroupBy(row => row["raid_id"], StringComparer.Ordinal))
            {
                IReadOnlyList<IReadOnlyDictionary<string, string>> ordered = expected
                    .Select(value => group.SingleOrDefault(row => row["difficulty"] == value))
                    .ToArray();
                valid &= ordered.All(row => row != null);
                if (ordered.All(row => row != null))
                {
                    long[] powers = ordered.Select(row => long.Parse(row["recommended_power"], CultureInfo.InvariantCulture)).ToArray();
                    valid &= powers[0] < powers[1] && powers[1] < powers[2];
                }
            }

            if (!valid)
            {
                report.AddError("CSV_RAID_DIFFICULTY_INVALID", table.FileName, "/", "Each raid requires NORMAL/HARD/CORRUPTED rows with increasing power.");
            }
        }

        private static void ValidateOfflineRules(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("offline_reward_rules.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0) return;

            List<IReadOnlyDictionary<string, string>> rows = Enabled(table).ToList();
            bool valid = rows.Count == 6 && rows.Select(row => row["settlement_type"]).ToHashSet(StringComparer.Ordinal).SetEquals(OfflineTypes);
            valid &= rows.All(row => long.TryParse(row["max_seconds"], NumberStyles.None, CultureInfo.InvariantCulture, out long seconds) && seconds == 28_800);
            valid &= rows.All(row => decimal.TryParse(row["efficiency"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal efficiency) && efficiency >= 0m && efficiency <= 1m);
            if (!valid)
            {
                report.AddError("CSV_OFFLINE_RULE_SET_INVALID", table.FileName, "/", "Offline rules must contain the six canonical types with an eight-hour cap.");
            }
        }

        private static void ValidateTutorialDag(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("tutorial_steps.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0) return;

            List<IReadOnlyDictionary<string, string>> rows = Enabled(table).ToList();
            var byId = rows.ToDictionary(row => row["tutorial_step_id"], StringComparer.Ordinal);
            bool valid = rows.Select(row => row["order"]).Distinct(StringComparer.Ordinal).Count() == rows.Count;
            foreach (IReadOnlyDictionary<string, string> row in rows)
            {
                string prerequisite = row["prerequisite_step_id"];
                if (prerequisite.Length == 0)
                {
                    continue;
                }

                valid &= byId.TryGetValue(prerequisite, out IReadOnlyDictionary<string, string> parent) &&
                    int.Parse(parent["order"], CultureInfo.InvariantCulture) < int.Parse(row["order"], CultureInfo.InvariantCulture);
            }

            if (!valid)
            {
                report.AddError("CSV_TUTORIAL_DAG_INVALID", table.FileName, "/", "Tutorial prerequisites must form a strictly ordered DAG.");
            }
        }

        private static void ValidateConditionGroups(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("condition_group_members.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0) return;

            var edges = Enabled(table)
                .Where(row => row["member_type"] == "GROUP")
                .GroupBy(row => row["condition_group_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(row => row["child_group_id"]).ToArray(), StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            bool Cycle(string node, int depth)
            {
                if (depth > 16 || !visiting.Add(node)) return true;
                if (edges.TryGetValue(node, out string[] children) && children.Any(child => !visited.Contains(child) && Cycle(child, depth + 1))) return true;
                visiting.Remove(node);
                visited.Add(node);
                return false;
            }

            if (edges.Keys.Any(node => !visited.Contains(node) && Cycle(node, 1)))
            {
                report.AddError("CSV_CONDITION_GROUP_INVALID", table.FileName, "/", "Condition group graph contains a cycle or exceeds depth 16.");
            }
        }

        private static void ValidateRuntimeConfig(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("runtime_config.csv", out ContentTable table))
            {
                return;
            }
            if (table.Rows.Count == 0) return;

            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyDictionary<string, string> row = table.Rows[index];
                string valueType = row["value_type"];
                bool boolean = valueType == "BOOLEAN";
                bool valid = valueType switch
                {
                    "INTEGER" => long.TryParse(row["value"], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer) &&
                        Bound(row["min_value"], row["max_value"], integer),
                    "DECIMAL" => decimal.TryParse(row["value"], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal number) &&
                        Bound(row["min_value"], row["max_value"], number),
                    "BOOLEAN" => row["value"] is "TRUE" or "FALSE" && row["min_value"].Length == 0 && row["max_value"].Length == 0 && row["unit"] == "BOOL",
                    "STRING" => row["value"].Length > 0 && row["min_value"].Length == 0 && row["max_value"].Length == 0,
                    _ => false
                };
                if (!valid)
                {
                    Error(
                        report,
                        boolean ? "CSV_RUNTIME_BOOLEAN_LEXICAL_INVALID" : "CSV_RUNTIME_VALUE_TYPE_MISMATCH",
                        table,
                        index,
                        "runtime_config value/min/max does not match value_type.");
                }
            }
        }

        private static bool Bound(string minimum, string maximum, long value) =>
            (minimum.Length == 0 || long.TryParse(minimum, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long min) && min <= value) &&
            (maximum.Length == 0 || long.TryParse(maximum, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long max) && value <= max);

        private static bool Bound(string minimum, string maximum, decimal value) =>
            (minimum.Length == 0 || decimal.TryParse(minimum, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal min) && min <= value) &&
            (maximum.Length == 0 || decimal.TryParse(maximum, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal max) && value <= max);

        private static void ValidateFacilityConstruction(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("facility_construction_rules.csv", out ContentTable table))
            {
                return;
            }

            List<IReadOnlyDictionary<string, string>> rows = Enabled(table).ToList();
            string[] facilityIds = rows.Select(row => row["facility_id"]).Distinct(StringComparer.Ordinal).ToArray();
            bool valid = rows.Count == 32 && facilityIds.Length == 8;
            foreach (string facilityId in facilityIds)
            {
                valid &= rows.Where(row => row["facility_id"] == facilityId)
                    .Select(row => row["level"])
                    .SequenceEqual(new[] { "1", "2", "3", "4" });
            }

            if (!valid)
            {
                report.AddError("CSV_FACILITY_CONSTRUCTION_COVERAGE_INVALID", table.FileName, "/", "Enabled construction rules require exact 8x4 coverage.");
            }

            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyDictionary<string, string> row = table.Rows[index];
                if (!int.TryParse(row["build_or_upgrade_duration_seconds"], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds is < 1 or > 86_400)
                {
                    Error(report, "CSV_FACILITY_DURATION_INVALID", table, index, "Facility duration must be in 1..86400 seconds.");
                }
                if (row["cancel_refund_ratio"] != "0")
                {
                    Error(report, "CSV_FACILITY_CANCEL_RATIO_INVALID", table, index, "P04 facility cancellation ratio must be zero.");
                }
            }
        }

        private static void ValidateFacilityWorldAssets(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("facility_world_assets.csv", out ContentTable table))
            {
                return;
            }

            List<IReadOnlyDictionary<string, string>> rows = Enabled(table).ToList();
            bool unique = rows.Select(row => row["address"]).Distinct(StringComparer.Ordinal).Count() == rows.Count;
            if (!unique)
            {
                report.AddError("CSV_FACILITY_WORLD_ASSET_DUPLICATE", table.FileName, "/address", "World asset addresses must be globally unique.");
            }

            int bases = rows.Count(row => row["state_variant"] == "BASE" && row["asset_type"] == "PREFAB" && row["facility_id"].Length > 0);
            if (bases != 8 || rows.Where(row => row["state_variant"] == "BASE").Select(row => row["facility_id"]).Distinct(StringComparer.Ordinal).Count() != 8)
            {
                report.AddError("CSV_FACILITY_WORLD_ASSET_COVERAGE_INVALID", table.FileName, "/", "Every facility requires one PREFAB/BASE asset.");
            }

            var roles = new HashSet<string>(rows.Where(row => row["facility_id"].Length == 0).Select(row => row["state_variant"]), StringComparer.Ordinal);
            if (!roles.SetEquals(new[] { "BACKGROUND", "PLOT", "LOCKED", "CONSTRUCTION", "STOPPED" }))
            {
                report.AddError("CSV_FACILITY_WORLD_ROLE_COVERAGE_INVALID", table.FileName, "/", "Scene/state role coverage is incomplete.");
            }

            for (int index = 0; index < table.Rows.Count; index++)
            {
                IReadOnlyDictionary<string, string> row = table.Rows[index];
                bool dimensions = int.TryParse(row["width_px"], out int width) && width is >= 1 and <= 4096 &&
                    int.TryParse(row["height_px"], out int height) && height is >= 1 and <= 4096;
                bool pivot = decimal.TryParse(row["pivot_x"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal pivotX) && pivotX is >= 0 and <= 1 &&
                    decimal.TryParse(row["pivot_y"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal pivotY) && pivotY is >= 0 and <= 1;
                if (!dimensions) Error(report, "CSV_FACILITY_WORLD_DIMENSION_INVALID", table, index, "World asset dimensions must be in 1..4096.");
                if (!pivot) Error(report, "CSV_FACILITY_WORLD_PIVOT_INVALID", table, index, "World asset pivot must be in 0..1.");
                if (row["pixels_per_unit"] != "100") Error(report, "CSV_FACILITY_WORLD_PPU_INVALID", table, index, "World asset PPU must be 100.");
            }
        }

        private static void ValidateRandomEquipment(IReadOnlyDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (!tables.TryGetValue("equipment_quality_weights.csv", out ContentTable weights) ||
                !tables.TryGetValue("random_equipment_tier_specs.csv", out ContentTable specs) ||
                !tables.TryGetValue("equipment_templates.csv", out ContentTable templates))
            {
                return;
            }

            bool valid = Enabled(weights).GroupBy(row => row["quality_profile_id"], StringComparer.Ordinal)
                .All(group => group.Sum(row => long.Parse(row["weight"], CultureInfo.InvariantCulture)) == 100);
            foreach (IReadOnlyDictionary<string, string> spec in Enabled(specs))
            {
                valid &= Enabled(templates).Any(template => template["tier"] == spec["tier"] &&
                    (spec["slot_policy"] == "ANY" || template["slot"] == spec["fixed_slot"]));
            }

            if (!valid)
            {
                report.AddError("CSV_RANDOM_EQUIPMENT_SPEC_INVALID", specs.FileName, "/", "Quality weights must sum to 100 and every spec needs an eligible template.");
            }
        }

        private static IEnumerable<IReadOnlyDictionary<string, string>> Enabled(ContentTable table) =>
            table.Rows.Where(row => row["enabled"] == "TRUE");

        private static bool HasEnabledId(IReadOnlyDictionary<string, ContentTable> tables, string file, string key, string value) =>
            value.Length > 0 && tables.TryGetValue(file, out ContentTable table) && Enabled(table).Any(row => row[key] == value);

        private static void Error(ValidationReport report, string code, ContentTable table, int index, string message) =>
            report.AddError(code, table.FileName, (index + 2).ToString(CultureInfo.InvariantCulture), message);
    }
}
