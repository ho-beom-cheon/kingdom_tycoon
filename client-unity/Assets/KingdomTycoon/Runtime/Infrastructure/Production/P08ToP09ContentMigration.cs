using System;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Production
{
    public sealed class P08ToP09ContentMigration
    {
        public bool CanApply(JObject document) => document?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P08ContentVersion;

        public JObject Apply(JObject source, JArray stockTargets)
        {
            if (!CanApply(source) || stockTargets == null) throw new InvalidOperationException("P09_MIGRATION_VALIDATION_FAILED");
            var result = (JObject)source.DeepClone();
            result["gameVersion"] = "1.0.0-p09";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P09ContentVersion;
            result["payload"]!["production"] = NewProduction(stockTargets);
            result["payload"]!["economy"]!["store"]!["supplyState"]!["mode"] = "PRODUCTION_OWNED";
            return result;
        }

        public static JObject NewProduction(JArray stockTargets) => new()
        {
            ["productionVersion"] = 1,
            ["currentTick"] = 0,
            ["nextQueueNo"] = 1,
            ["nextEventSequence"] = 1,
            ["stockTargets"] = stockTargets.DeepClone(),
            ["facilityQueues"] = new JArray(new[] { "FAC_BLACKSMITH", "FAC_ALCHEMY", "FAC_INFIRMARY" }.Select(id => new JObject
            {
                ["facilityId"] = id, ["stoppedReason"] = "P09_FACILITY_LOCKED", ["jobs"] = new JArray(), ["lastCompletion"] = null
            })),
            ["events"] = new JArray()
        };
    }
}
