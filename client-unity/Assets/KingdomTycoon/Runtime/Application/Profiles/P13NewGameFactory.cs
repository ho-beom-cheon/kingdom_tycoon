using System;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Save;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Profiles
{
    public sealed class P13NewGameFactory : INewGameFactory
    {
        private readonly JObject template;
        public P13NewGameFactory(string templateJson) => template = StrictJson.ParseObject(templateJson ?? throw new ArgumentNullException(nameof(templateJson)));

        public JObject CreateDraft(string saveId, string profileId, DateTimeOffset now)
        {
            string timestamp = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            var document = (JObject)template.DeepClone();
            document["saveId"] = saveId ?? throw new ArgumentNullException(nameof(saveId));
            document["profileId"] = profileId ?? throw new ArgumentNullException(nameof(profileId));
            document["revision"] = 0;
            document["createdAtUtc"] = timestamp;
            document["savedAtUtc"] = timestamp;
            document["gameVersion"] = "1.0.0-p13";
            document["contentVersion"] = CompileTimeActiveContentVersionProvider.P13ContentVersion;
            foreach (JObject npc in document["payload"]!["managementNpcs"]!.Children<JObject>()) npc["instanceId"] = UuidV7.NewString(now);
            foreach (JObject mercenary in document["payload"]!["mercenaries"]!.Children<JObject>())
            {
                mercenary["instanceId"] = UuidV7.NewString(now);
                mercenary["autonomy"]!["stateStartedAtUtc"] = timestamp;
                mercenary["autonomy"]!["nextDecisionAtUtc"] = timestamp;
            }
            document["payload"]!["mercenaries"] = new JArray(document["payload"]!["mercenaries"]!.Children<JObject>()
                .OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal));
            foreach (JObject region in document["payload"]!["regions"]!["progress"]!.Children<JObject>())
                if (region.Value<bool>("unlocked")) region["firstUnlockedAtUtc"] = timestamp;
            document["payload"]!["offline"]!["accrualCursorUtc"] = timestamp;
            document["payload"]!["offline"]!["lastTrustedUtc"] = timestamp;
            JObject integrity = (JObject)document["integrity"]!;
            integrity["payloadSha256"] = Rfc8785Canonicalizer.ComputeSha256(document["payload"]!);
            integrity["fileSha256"] = Rfc8785Canonicalizer.ComputeSha256(SaveDocumentValidator.BuildEnvelopeDigestInput(document));
            return document;
        }
    }
}
