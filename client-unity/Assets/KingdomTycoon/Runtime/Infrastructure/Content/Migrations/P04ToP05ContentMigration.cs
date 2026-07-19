using System;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Domain.Mercenaries;
using KingdomTycoon.Infrastructure.Mercenaries;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Content.Migrations
{
    public sealed class P04ToP05ContentMigration
    {
        private readonly JObject golden;
        private readonly IUuidV7Provider ids;
        private readonly ITrustedUtcClock clock;
        private readonly MercenaryInvariantValidator validator;
        private readonly CanonicalMercenaryCatalog catalog;

        public P04ToP05ContentMigration(string migrationGoldenJson, IUuidV7Provider ids, ITrustedUtcClock clock, MercenaryInvariantValidator validator, CanonicalMercenaryCatalog catalog)
        {
            golden = StrictJson.ParseObject(migrationGoldenJson ?? throw new ArgumentNullException(nameof(migrationGoldenJson)));
            this.ids = ids ?? throw new ArgumentNullException(nameof(ids));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public bool CanApply(JObject document) => document?.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P04ContentVersion;

        public JObject Apply(JObject document)
        {
            if (!CanApply(document)) throw new InvalidOperationException("P05_MIGRATION_VALIDATION_FAILED");
            var migrated = (JObject)document.DeepClone();
            JArray mercenaries = (JArray)migrated["payload"]!["mercenaries"]!;
            if (mercenaries.Count == 0)
            {
                string timestamp = clock.UtcNow.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
                try
                {
                    foreach (JObject source in golden["payload"]!["mercenaries"]!.Children<JObject>())
                    {
                        var starter = (JObject)source.DeepClone();
                        starter["instanceId"] = ids.NewId().ToString("D");
                        starter["autonomy"]!["stateStartedAtUtc"] = timestamp;
                        starter["autonomy"]!["nextDecisionAtUtc"] = timestamp;
                        mercenaries.Add(starter);
                    }
                    var ordered = new JArray(mercenaries.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal));
                    migrated["payload"]!["mercenaries"] = ordered;
                }
                catch (Exception exception) when (exception is not MercenaryDomainException)
                {
                    throw new InvalidOperationException("P05_MIGRATION_UUID_FAILED", exception);
                }
            }
            migrated["contentVersion"] = CompileTimeActiveContentVersionProvider.P05ContentVersion;
            try { validator.Validate(migrated, catalog); }
            catch (MercenaryDomainException exception) when (exception.ErrorCode == "SAVE_MERCENARY_SLOT_LIMIT_EXCEEDED") { throw new InvalidOperationException("P05_MIGRATION_SLOT_LIMIT_INVALID", exception); }
            catch (MercenaryDomainException exception) { throw new InvalidOperationException("P05_MIGRATION_VALIDATION_FAILED", exception); }
            return migrated;
        }
    }
}
