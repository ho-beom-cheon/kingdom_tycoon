using System;
using System.Collections.Generic;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Raids
{
    public sealed class P13ToP14ContentMigration
    {
        private static readonly string[] Difficulties = { "NORMAL", "HARD", "CORRUPTED" };
        private static readonly IReadOnlyDictionary<string, int> StageOrder = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["KINGDOM_1"] = 1, ["KINGDOM_2"] = 2, ["KINGDOM_3"] = 3, ["KINGDOM_4"] = 4, ["KINGDOM_5"] = 5
        };

        public bool CanApply(JObject source) =>
            source?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P13ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P14_MIGRATION_SOURCE_INVALID");
            JObject result = (JObject)source.DeepClone();
            JObject regions = (JObject)result["payload"]!["regions"]!;
            regions["regionVersion"] = 2;
            regions["raidVersion"] = 1;
            regions["nextRaidHistorySequence"] = regions.Value<long?>("nextRaidHistorySequence") ?? 1;
            regions["raidHistory"] = regions["raidHistory"]?.DeepClone() ?? new JArray();
            regions["raids"] = BuildRaidProgress(result);
            result["gameVersion"] = "1.0.0-p14";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P14ContentVersion;
            return result;
        }

        private static JArray BuildRaidProgress(JObject document)
        {
            JObject regions = (JObject)document["payload"]!["regions"]!;
            var existing = regions["raids"]!.Children<JObject>().ToDictionary(
                value => Key(value.Value<string>("raidId"), value.Value<string>("difficulty")),
                value => value,
                StringComparer.Ordinal);
            var progress = regions["progress"]!.Children<JObject>().ToDictionary(value => value.Value<string>("regionId"), StringComparer.Ordinal);
            var flags = new HashSet<string>(document["payload"]!["kingdom"]!["progressionFlagIds"]!.Values<string>(), StringComparer.Ordinal);
            int stage = StageOrder.GetValueOrDefault(document["payload"]!["kingdom"]!.Value<string>("kingdomStageId"));
            var result = new JArray();
            AddRaid(result, existing, progress, flags, stage, "RAID_HYDRA", "REGION_R04", 4, new[] { "HYDRA_HEAD", "HYDRA_BODY", "HYDRA_HEART" });
            AddRaid(result, existing, progress, flags, stage, "RAID_DRAGON", "REGION_R05", 5, new[] { "DRAGON_HORN", "DRAGON_WING", "DRAGON_BODY", "DRAGON_HEART" });
            return result;
        }

        private static void AddRaid(JArray target, IReadOnlyDictionary<string, JObject> existing, IReadOnlyDictionary<string, JObject> progress,
            ISet<string> flags, int stage, string raidId, string regionId, int requiredStage, IReadOnlyList<string> partIds)
        {
            long previousClear = 0;
            foreach (string difficulty in Difficulties)
            {
                existing.TryGetValue(Key(raidId, difficulty), out JObject old);
                bool unlocked = stage >= requiredStage && progress[regionId].Value<int>("progressPercent") >= 100;
                if (difficulty == "HARD") unlocked &= previousClear >= 1;
                if (difficulty == "CORRUPTED") unlocked &= previousClear >= 3 && flags.Contains("FLAG_RAID_DRAGON_VARIANTS");
                var oldParts = (old?["partStates"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                    .ToDictionary(value => value.Value<string>("partId"), StringComparer.Ordinal);
                var parts = new JArray();
                foreach (string partId in partIds)
                {
                    oldParts.TryGetValue(partId, out JObject part);
                    parts.Add(new JObject
                    {
                        ["partId"] = partId,
                        ["breakCount"] = part?.Value<long?>("breakCount") ?? 0,
                        ["exposedCount"] = part?.Value<long?>("exposedCount") ?? 0,
                        ["bestBreakTimeMs"] = part?["bestBreakTimeMs"]?.DeepClone() ?? JValue.CreateNull(),
                        ["lastRewardOperationId"] = part?["lastRewardOperationId"]?.DeepClone() ?? JValue.CreateNull()
                    });
                }
                var row = new JObject
                {
                    ["raidId"] = raidId,
                    ["difficulty"] = difficulty,
                    ["unlocked"] = (old?.Value<bool?>("unlocked") ?? false) || unlocked,
                    ["attemptCount"] = old?.Value<long?>("attemptCount") ?? old?.Value<long?>("clearCount") ?? 0,
                    ["clearCount"] = old?.Value<long?>("clearCount") ?? 0,
                    ["bestClearTimeMs"] = old?["bestClearTimeMs"]?.DeepClone() ?? JValue.CreateNull(),
                    ["firstClearedAtUtc"] = old?["firstClearedAtUtc"]?.DeepClone() ?? JValue.CreateNull(),
                    ["lastClearedAtUtc"] = old?["lastClearedAtUtc"]?.DeepClone() ?? JValue.CreateNull(),
                    ["firstClearRewardOperationId"] = old?["firstClearRewardOperationId"]?.DeepClone() ?? JValue.CreateNull(),
                    ["lastResultCode"] = old?["lastResultCode"]?.DeepClone() ?? JValue.CreateNull(),
                    ["partStates"] = parts
                };
                target.Add(row);
                previousClear = row.Value<long>("clearCount");
            }
        }

        private static string Key(string raidId, string difficulty) => raidId + "|" + difficulty;
    }
}
