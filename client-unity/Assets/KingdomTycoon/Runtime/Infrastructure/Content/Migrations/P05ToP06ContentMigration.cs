using System;
using System.Globalization;
using KingdomTycoon.Application.Abstractions;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Content.Migrations
{
    public sealed class P05ToP06ContentMigration
    {
        private readonly ITrustedUtcClock clock;

        public P05ToP06ContentMigration(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));

        public bool CanApply(JObject document) => document?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P05ContentVersion;

        public JObject Apply(JObject source)
        {
            if (!CanApply(source)) throw new InvalidOperationException("P06_MIGRATION_VALIDATION_FAILED");
            var result = (JObject)source.DeepClone();
            result["contentVersion"] = CompileTimeActiveContentVersionProvider.P06ContentVersion;
            NormalizeTransientAutonomy(result, clock.UtcNow);
            return result;
        }

        public static bool NormalizeTransientAutonomy(JObject document, DateTimeOffset now)
        {
            bool changed = false;
            string timestamp = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            foreach (JObject mercenary in document["payload"]!["mercenaries"]!.Children<JObject>())
            {
                JObject autonomy = (JObject)mercenary["autonomy"]!;
                string state = autonomy.Value<string>("state");
                if (state is "IDLE_TOWN" or "PROMOTION_READY" or "INJURED") continue;
                autonomy["state"] = "IDLE_TOWN";
                autonomy["reasonCode"] = "NONE";
                autonomy["currentRegionId"] = null;
                autonomy["targetInstanceId"] = null;
                autonomy["stateStartedAtUtc"] = timestamp;
                autonomy["nextDecisionAtUtc"] = timestamp;
                changed = true;
            }
            return changed;
        }
    }
}
