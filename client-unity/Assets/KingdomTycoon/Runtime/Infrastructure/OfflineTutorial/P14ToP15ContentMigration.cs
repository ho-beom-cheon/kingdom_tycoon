using System;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.OfflineTutorial
{
    public sealed class P14ToP15ContentMigration
    {
        public bool CanApply(JObject source) => source?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P14ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P15_MIGRATION_SOURCE_INVALID");
            JObject result = (JObject)source.DeepClone();
            JObject payload = (JObject)result["payload"]!;
            JObject oldTutorial = (JObject)payload["tutorial"]!;
            string current = oldTutorial.Value<string>("currentStepId");
            bool completed = oldTutorial["completedAtUtc"]!.Type != JTokenType.Null;
            payload["tutorial"] = new JObject
            {
                ["tutorialVersion"] = 1,
                ["currentStepId"] = completed ? JValue.CreateNull() : current != null && current.StartsWith("TUT_", StringComparison.Ordinal) ? current : "TUT_01_KINGDOM_OVERVIEW",
                ["completedStepIds"] = SortedUnique(oldTutorial["completedStepIds"] as JArray),
                ["grantedRewardIds"] = SortedUnique(oldTutorial["grantedRewardIds"] as JArray),
                ["actionReceipts"] = new JArray(), ["skipped"] = oldTutorial.Value<bool>("skipped"),
                ["startedAtUtc"] = JValue.CreateNull(), ["lastAdvancedAtUtc"] = JValue.CreateNull(),
                ["completedAtUtc"] = oldTutorial["completedAtUtc"]!.DeepClone(), ["lastOperationId"] = JValue.CreateNull()
            };
            JObject oldOffline = (JObject)payload["offline"]!;
            payload["offline"] = new JObject
            {
                ["offlineVersion"] = 1, ["accrualCursorUtc"] = oldOffline["accrualCursorUtc"]!.DeepClone(),
                ["lastTrustedUtc"] = oldOffline["lastTrustedUtc"]!.DeepClone(), ["lastSettlementId"] = JValue.CreateNull(),
                ["lastStatus"] = "NONE", ["lastElapsedSeconds"] = 0, ["lastEligibleSeconds"] = 0,
                ["pendingSettlement"] = oldOffline["pendingSettlement"]!.DeepClone(), ["history"] = new JArray()
            };
            result["gameVersion"] = "1.0.0-p15";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P15ContentVersion;
            return result;
        }

        private static JArray SortedUnique(JArray source) => new((source?.Values<string>() ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal));
    }
}
