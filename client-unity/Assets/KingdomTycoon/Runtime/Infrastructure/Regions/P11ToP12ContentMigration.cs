using System;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Regions
{
    public sealed class P11ToP12ContentMigration
    {
        public bool CanApply(JObject source) => source?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P11ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P12_MIGRATION_SOURCE_INVALID");
            JObject result = (JObject)source.DeepClone();
            JObject old = (JObject)result["payload"]!["regions"]!;
            result["payload"]!["regions"] = new JObject
            {
                ["regionVersion"] = 1, ["nextEventSequence"] = 1,
                ["progress"] = old["progress"]!.DeepClone(), ["raids"] = old["raids"]!.DeepClone(), ["events"] = new JArray()
            };
            JObject kingdom = (JObject)result["payload"]!["kingdom"]!;
            var existing = kingdom["regionAccessPolicies"]!.Children<JObject>()
                .ToDictionary(value => value.Value<string>("regionId"), value => value.Value<bool>("allowed"), StringComparer.Ordinal);
            kingdom["regionAccessPolicies"] = new JArray(Enumerable.Range(1, 5).Select(order =>
            {
                string id = $"REGION_R0{order}";
                return new JObject { ["regionId"] = id, ["allowed"] = existing.TryGetValue(id, out bool allowed) ? allowed : order == 1 };
            }));
            result["gameVersion"] = "1.0.0-p12";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P12ContentVersion;
            return result;
        }
    }
}
