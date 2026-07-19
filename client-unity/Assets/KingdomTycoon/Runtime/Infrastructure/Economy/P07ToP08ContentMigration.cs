using System;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Economy
{
    public sealed class P07ToP08ContentMigration
    {
        public bool CanApply(JObject document) =>
            document?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P07ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P08_MIGRATION_VALIDATION_FAILED");
            var result = (JObject)source.DeepClone();
            result["gameVersion"] = "1.0.0-p08";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P08ContentVersion;
            result["payload"]!["economy"] = NewEconomy();
            return result;
        }

        public static JObject NewEconomy() => new()
        {
            ["economyVersion"] = 1,
            ["store"] = new JObject
            {
                ["stockVersion"] = 0,
                ["stackLines"] = new JArray(),
                ["equipment"] = new JArray(),
                ["supplyState"] = new JObject
                {
                    ["mode"] = "SYSTEM_SUPPLY", ["epoch"] = 0,
                    ["buyCountSinceRefresh"] = 0, ["lastRefreshOperationId"] = null
                },
                ["ledger"] = new JObject
                {
                    ["nextSequence"] = 1, ["prunedThroughSequence"] = 0,
                    ["prunedDigest"] = null, ["entries"] = new JArray()
                }
            }
        };
    }
}
