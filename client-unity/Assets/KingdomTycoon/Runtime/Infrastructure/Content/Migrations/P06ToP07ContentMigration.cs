using System;
using System.Linq;
using KingdomTycoon.Application.Abstractions;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Content.Migrations
{
    public sealed class P06ToP07ContentMigration
    {
        private readonly ITrustedUtcClock clock;

        public P06ToP07ContentMigration(ITrustedUtcClock clock) =>
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

        public bool CanApply(JObject document) =>
            document?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P06ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P07_MIGRATION_VALIDATION_FAILED");
            var result = (JObject)source.DeepClone();
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P07ContentVersion;
            JObject policies = (JObject)result["payload"]!["kingdom"]!["inventoryPolicies"]!;
            policies["autoEquipEnabled"] ??= true;
            policies["upgradeThresholdBps"] ??= 500;
            policies["autoSellEnabled"] ??= true;
            policies["protectBossEquipment"] ??= true;
            policies["protectFirstDiscovery"] ??= true;
            policies["discoveredEquipmentTemplateIds"] ??= new JArray();
            foreach (JObject mercenary in result["payload"]!["mercenaries"]!.Children<JObject>())
            {
                var potions = (JArray)mercenary["potions"]!;
                JObject healing = potions.Children<JObject>()
                    .SingleOrDefault(value => value.Value<string>("potionId") == "POT_HEAL_SMALL");
                if (healing == null)
                    potions.Add(new JObject { ["potionId"] = "POT_HEAL_SMALL", ["quantity"] = 2 });
                else
                    healing["quantity"] = checked(healing.Value<long>("quantity") + 2);
            }
            P05ToP06ContentMigration.NormalizeTransientAutonomy(result, clock.UtcNow);
            return result;
        }
    }
}
