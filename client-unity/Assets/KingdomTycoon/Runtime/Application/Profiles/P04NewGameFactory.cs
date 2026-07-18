using System;
using System.Globalization;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Save;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Profiles
{
    public sealed class P04NewGameFactory
    {
        private readonly JObject template;

        public P04NewGameFactory(string templateJson)
        {
            template = StrictJson.ParseObject(templateJson ?? throw new ArgumentNullException(nameof(templateJson)));
        }

        public JObject CreateDraft(string profileId, DateTimeOffset now)
        {
            string timestamp = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            var document = (JObject)template.DeepClone();
            document["saveId"] = UuidV7.NewString(now);
            document["profileId"] = profileId;
            document["revision"] = 0;
            document["createdAtUtc"] = timestamp;
            document["savedAtUtc"] = timestamp;
            document["contentVersion"] = "1.0.0-content.2";
            foreach (JObject npc in document["payload"]!["managementNpcs"]!.Children<JObject>())
            {
                npc["instanceId"] = UuidV7.NewString(now);
            }
            foreach (JObject region in document["payload"]!["regions"]!["progress"]!.Children<JObject>())
            {
                if (region.Value<bool>("unlocked"))
                {
                    region["firstUnlockedAtUtc"] = timestamp;
                }
            }

            JObject integrity = (JObject)document["integrity"]!;
            integrity["payloadSha256"] = Rfc8785Canonicalizer.ComputeSha256(document["payload"]!);
            integrity["fileSha256"] = Rfc8785Canonicalizer.ComputeSha256(SaveDocumentValidator.BuildEnvelopeDigestInput(document));
            return document;
        }
    }
}
