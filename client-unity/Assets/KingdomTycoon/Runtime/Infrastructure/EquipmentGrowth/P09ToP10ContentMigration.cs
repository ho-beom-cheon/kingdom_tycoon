using System;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.EquipmentGrowth
{
    public sealed class P09ToP10ContentMigration
    {
        public bool CanApply(JObject document) => document?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P09ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P10_MIGRATION_VALIDATION_FAILED");
            var result = (JObject)source.DeepClone();
            result["gameVersion"] = "1.0.0-p10";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P10ContentVersion;
            NormalizeEquipment((JArray)result["payload"]!["inventory"]!["equipment"]!);
            NormalizeEquipment((JArray)result["payload"]!["economy"]!["store"]!["equipment"]!);
            result["payload"]!["equipmentGrowth"] = NewGrowthState();
            return result;
        }

        public static JObject NewGrowthState() => new()
        {
            ["growthVersion"] = 1,
            ["nextEventSequence"] = 1,
            ["events"] = new JArray()
        };

        public static void NormalizeEquipment(JArray equipment)
        {
            foreach (JObject value in equipment.Children<JObject>())
            {
                value["enhancementPityBps"] = 0;
                value["enhancementAttemptCount"] = 0;
                value["enhancementMaterialInvested"] = new JArray();
                value["pendingRefineOption"] = null;
                value["refineRollCount"] = 0;
            }
        }
    }
}
