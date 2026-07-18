using System;
using System.Linq;
using KingdomTycoon.Infrastructure;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Content.Migrations
{
    public sealed class P03ToP04ContentMigration
    {
        private readonly JObject p04Template;

        public P03ToP04ContentMigration(string p04TemplateJson)
        {
            p04Template = StrictJson.ParseObject(p04TemplateJson ?? throw new ArgumentNullException(nameof(p04TemplateJson)));
        }

        public bool CanApply(JObject document)
        {
            if (document == null || document.Value<string>("contentVersion") != "1.0.0-content.1" || document.Value<long>("revision") < 1) return false;
            JToken payload = document["payload"];
            if (payload["kingdom"].Value<long>("kingdomGold") != 0 || payload["managementNpcs"].Any() || payload["operationJournal"].Any()) return false;
            JObject[] facilities = payload["facilities"].Children<JObject>().ToArray();
            return facilities.Length == 8 && facilities.All(facility =>
                facility.Value<int>("level") == 1 && facility.Value<string>("state") == "LOCKED" &&
                facility["assignedNpcInstanceId"].Type == JTokenType.Null && facility["job"].Type == JTokenType.Null && !facility["storage"].Any());
        }

        public JObject Apply(JObject document)
        {
            if (!CanApply(document)) throw new InvalidOperationException("P04_BOOTSTRAP_SIGNATURE_MISMATCH");
            var migrated = (JObject)document.DeepClone();
            migrated["contentVersion"] = CompileTimeActiveContentVersionProvider.P04ContentVersion;
            migrated["payload"]["kingdom"]["kingdomGold"] = 5000;
            migrated["payload"]["managementNpcs"] = p04Template["payload"]["managementNpcs"].DeepClone();
            migrated["payload"]["facilities"] = p04Template["payload"]["facilities"].DeepClone();
            return migrated;
        }
    }
}
