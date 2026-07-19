using System;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Progression
{
    public sealed class P10ToP11ContentMigration
    {
        public bool CanApply(JObject document) => document?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P10ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P11_MIGRATION_VALIDATION_FAILED");
            var result = (JObject)source.DeepClone();
            result["gameVersion"] = "1.0.0-p11";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P11ContentVersion;
            foreach (JObject mercenary in result["payload"]!["mercenaries"]!.Children<JObject>())
            {
                JObject records = (JObject)mercenary["records"]!;
                records["region2BattleCount"] = 0;
                records["eliteKillCount"] = 0;
                records["bossContributionCount"] = 0;
            }
            result["payload"]!["progression"] = NewProgressionState();
            return result;
        }

        public static JObject NewProgressionState() => new()
        {
            ["progressionVersion"] = 1, ["nextEventSequence"] = 1,
            ["issuedSupplyKeys"] = new JArray(), ["events"] = new JArray()
        };
    }
}
