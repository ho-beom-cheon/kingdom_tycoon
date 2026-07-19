using System;
using System.Linq;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Recruitment
{
    public sealed class P12ToP13ContentMigration
    {
        public bool CanApply(JObject source) => source?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P12ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P13_MIGRATION_SOURCE_INVALID");
            JObject result = (JObject)source.DeepClone();
            JObject old = (JObject)result["payload"]!["recruitmentMockState"]!;
            JObject wallet = (JObject)old["premiumWalletCache"]!.DeepClone();
            if (old.Value<string>("authority") == "MOCK_ONLY" && wallet.Properties().Sum(value => value.Value.Value<long>()) == 0)
            {
                wallet["freePremium"] = 600;
                wallet["paidPremium"] = 0;
                wallet["specialRecruitTickets"] = 3;
            }
            result["payload"]!["recruitmentMockState"] = new JObject
            {
                ["stateVersion"] = 1,
                ["authority"] = old["authority"]!.DeepClone(),
                ["serverRevision"] = old["serverRevision"]!.DeepClone(),
                ["nextHistorySequence"] = 1,
                ["nextEventSequence"] = 1,
                ["premiumWalletCache"] = wallet,
                ["tavern"] = new JObject
                {
                    ["refreshSequence"] = 0,
                    ["lastRefreshAtUtc"] = JValue.CreateNull(),
                    ["nextFreeRefreshAtUtc"] = JValue.CreateNull(),
                    ["candidates"] = new JArray()
                },
                ["pityCounters"] = old["pityCounters"]!.DeepClone(),
                ["featuredGuarantees"] = old["featuredGuarantees"]!.DeepClone(),
                ["pendingRequests"] = old["pendingRequests"]!.DeepClone(),
                ["history"] = new JArray(),
                ["events"] = new JArray()
            };
            result["gameVersion"] = "1.0.0-p13";
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P13ContentVersion;
            return result;
        }
    }
}
