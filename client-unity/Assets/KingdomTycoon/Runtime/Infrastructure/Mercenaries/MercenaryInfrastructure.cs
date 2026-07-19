using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Application.Mercenaries;
using KingdomTycoon.Application.Mercenaries.Commands;
using KingdomTycoon.Domain.Mercenaries;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Mercenaries
{
    public sealed class CanonicalMercenaryCatalog : IMercenaryCatalogRules
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> jobs;
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> grades;
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> ranks;
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> personalities;
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> traits;
        private readonly HashSet<string> potions;
        private readonly HashSet<string> traitEligibility;
        private readonly HashSet<string> equipmentEligibility;
        private readonly Dictionary<string, string> contentNameKeys = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> localizations;

        public CanonicalMercenaryCatalog(ContentCatalog source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            jobs = Index(source, "jobs.csv", "job_id", contentNameKeys);
            grades = Index(source, "mercenary_grades.csv", "grade_id", contentNameKeys);
            ranks = Index(source, "mercenary_ranks.csv", "rank_id", contentNameKeys);
            personalities = Index(source, "personalities.csv", "personality_id", contentNameKeys);
            traits = Index(source, "traits.csv", "trait_id", contentNameKeys);
            potions = source.GetTable("potions.csv").Rows.Where(Enabled).Select(row => row["potion_id"]).ToHashSet(StringComparer.Ordinal);
            foreach (IReadOnlyDictionary<string, string> row in source.GetTable("potions.csv").Rows.Where(Enabled)) contentNameKeys[row["potion_id"]] = row["name_text_key"];
            foreach (IReadOnlyDictionary<string, string> row in source.GetTable("equipment_templates.csv").Rows.Where(Enabled)) contentNameKeys[row["equipment_template_id"]] = row["name_text_key"];
            traitEligibility = source.GetTable("trait_job_eligibility.csv").Rows.Where(Enabled).Select(row => row["trait_id"] + "\0" + row["job_id"]).ToHashSet(StringComparer.Ordinal);
            equipmentEligibility = source.GetTable("equipment_job_eligibility.csv").Rows.Where(Enabled).Select(row => row["equipment_template_id"] + "\0" + row["job_id"]).ToHashSet(StringComparer.Ordinal);
            localizations = source.GetTable("localizations.csv").Rows.Where(row => Enabled(row) && row["locale"] == "ko-KR").ToDictionary(row => row["text_key"], row => row["text_value"], StringComparer.Ordinal);
        }

        public bool HasJob(string id) => jobs.ContainsKey(id ?? string.Empty);
        public bool HasGrade(string id) => grades.ContainsKey(id ?? string.Empty);
        public bool HasRank(string id) => ranks.ContainsKey(id ?? string.Empty);
        public bool HasPersonality(string id) => personalities.ContainsKey(id ?? string.Empty);
        public bool HasTrait(string id) => traits.ContainsKey(id ?? string.Empty);
        public bool HasPotion(string id) => potions.Contains(id ?? string.Empty);
        public bool IsTraitEligible(string traitId, string jobId) => traitEligibility.Contains(traitId + "\0" + jobId);
        public bool IsEquipmentEligible(string templateId, string jobId) => equipmentEligibility.Contains(templateId + "\0" + jobId);
        public int GradeOrder(string id) => Parse(grades, id, "order");
        public int RankOrder(string id) => Parse(ranks, id, "order");
        public int JobOrder(string id) => Array.IndexOf(new[] { "JOB_WARRIOR", "JOB_GUARDIAN", "JOB_ARCHER", "JOB_MAGE", "JOB_CLERIC" }, id);
        public int GradeTraitSlots(string id) => Parse(grades, id, "initial_trait_count");
        public int RankTraitSlots(string id) => Parse(ranks, id, "trait_slot_bonus");
        public int RankMaxLevel(string id) => Parse(ranks, id, "max_level");
        public string Localize(string idOrKey)
        {
            string key = contentNameKeys.TryGetValue(idOrKey ?? string.Empty, out string mapped) ? mapped : idOrKey;
            return key != null && localizations.TryGetValue(key, out string value) ? value : idOrKey ?? string.Empty;
        }

        private static Dictionary<string, IReadOnlyDictionary<string, string>> Index(ContentCatalog source, string table, string key, IDictionary<string, string> names)
        {
            var result = source.GetTable(table).Rows.Where(Enabled).ToDictionary(row => row[key], row => row, StringComparer.Ordinal);
            foreach (KeyValuePair<string, IReadOnlyDictionary<string, string>> row in result)
                if (row.Value.TryGetValue("name_text_key", out string textKey)) names[row.Key] = textKey;
            return result;
        }
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int Parse(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> values, string id, string field) =>
            int.Parse(values.TryGetValue(id ?? string.Empty, out IReadOnlyDictionary<string, string> row) ? row[field] : throw new MercenaryDomainException("SAVE_MERCENARY_CONTENT_REF_INVALID"), CultureInfo.InvariantCulture);
    }

    public sealed class SaveV1MercenaryMapper
    {
        public IReadOnlyList<Mercenary> Read(JObject document)
        {
            return document["payload"]!["mercenaries"]!.Children<JObject>().Select(ReadOne).ToArray();
        }

        public void SetActive(JObject document, string instanceId, bool active)
        {
            JObject value = document["payload"]!["mercenaries"]!.Children<JObject>().SingleOrDefault(item => item.Value<string>("instanceId") == instanceId)
                ?? throw new MercenaryDomainException("MERCENARY_NOT_FOUND");
            value["active"] = active;
        }

        private static Mercenary ReadOne(JObject value)
        {
            JObject slots = (JObject)value["equipmentSlots"]!;
            JObject records = (JObject)value["records"]!;
            return new Mercenary(
                value.Value<string>("instanceId"), value.Value<string>("displayName"), value.Value<string>("jobId"), value.Value<string>("gradeId"), value.Value<string>("rankId"),
                value.Value<int>("level"), value.Value<long>("exp"), value.Value<string>("personalityId"), value["traitIds"]!.Values<string>().ToArray(),
                value.Value<long>("personalGold"), value.Value<long>("contribution"), value.Value<bool>("active"), value["autonomy"]!.Value<string>("state"), value["autonomy"]!.Value<string>("reasonCode"),
                value["autonomy"]!["currentRegionId"]!.Type == JTokenType.Null ? null : value["autonomy"]!.Value<string>("currentRegionId"), value["promotion"]!.Value<string>("status"),
                slots.Properties().ToDictionary(property => property.Name, property => property.Value.Type == JTokenType.Null ? null : property.Value.Value<string>(), StringComparer.Ordinal),
                value["potions"]!.Children<JObject>().ToDictionary(item => item.Value<string>("potionId"), item => item.Value<long>("quantity"), StringComparer.Ordinal),
                records.Properties().ToDictionary(property => property.Name, property => property.Value.Value<long>(), StringComparer.Ordinal));
        }
    }

    public sealed class MercenaryRequestHasher : IMercenaryRequestHasher
    {
        public string ComputeHash(SetMercenaryActiveCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class MercenaryActivatedEventArgs : EventArgs
    {
        public MercenaryActivatedEventArgs(string id, bool active, int count, int limit, long revision) { InstanceId = id; Active = active; ActiveCount = count; Limit = limit; Revision = revision; }
        public string InstanceId { get; }
        public bool Active { get; }
        public int ActiveCount { get; }
        public int Limit { get; }
        public long Revision { get; }
    }

    public sealed class MercenaryRosterMigratedEventArgs : EventArgs
    {
        public MercenaryRosterMigratedEventArgs(int addedCount, string contentVersion, long revision)
        { AddedCount = addedCount; ContentVersion = contentVersion; Revision = revision; }
        public int AddedCount { get; }
        public string ContentVersion { get; }
        public long Revision { get; }
    }

    public sealed class MercenaryRosterService : IAppService, IMercenaryUnitOfWork
    {
        private readonly ITrustedUtcClock clock;
        private readonly string migrationGoldenJson;
        private readonly IUuidV7Provider ids;
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly SaveV1MercenaryMapper mapper = new();
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private CanonicalMercenaryCatalog catalog;
        private MercenaryInvariantValidator validator;

        public MercenaryRosterService(ITrustedUtcClock clock, string migrationGoldenJson, IUuidV7Provider ids = null)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.migrationGoldenJson = migrationGoldenJson ?? throw new ArgumentNullException(nameof(migrationGoldenJson));
            this.ids = ids ?? new SystemUuidV7Provider();
        }

        public int InitializationOrder => 60;
        public bool IsBootstrapped { get; private set; }
        public long Revision => game?.Revision ?? 0;
        public CanonicalMercenaryCatalog Catalog => catalog;
        public event EventHandler<MercenaryActivatedEventArgs> ActivityChanged;
        public event EventHandler<MercenaryRosterMigratedEventArgs> RosterMigrated;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>();
            content = services.Get<ContentCatalogService>();
            game = services.Get<FacilityGameService>();
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || content.Catalog == null) throw new InvalidOperationException("P05_GAME_NOT_BOOTSTRAPPED");
            catalog = new CanonicalMercenaryCatalog(content.Catalog);
            validator = new MercenaryInvariantValidator();
            JObject current = game.Snapshot();
            if (current.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P04ContentVersion)
            {
                int beforeCount = current["payload"]!["mercenaries"]!.Count();
                var migration = new KingdomTycoon.Infrastructure.Content.Migrations.P04ToP05ContentMigration(migrationGoldenJson, ids, clock, validator, catalog);
                JObject draft = migration.Apply(current);
                SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow);
                if (!written.Success) throw new InvalidOperationException(written.ErrorCode ?? "MERCENARY_SAVE_WRITE_FAILED");
                game.SynchronizeCommittedDocument(written.Document);
                int afterCount = written.Document["payload"]!["mercenaries"]!.Count();
                RosterMigrated?.Invoke(this, new MercenaryRosterMigratedEventArgs(afterCount - beforeCount,
                    written.Document.Value<string>("contentVersion"), written.Document.Value<long>("revision")));
            }
            else if (current.Value<string>("contentVersion") is not (CompileTimeActiveContentVersionProvider.P05ContentVersion or CompileTimeActiveContentVersionProvider.P06ContentVersion or CompileTimeActiveContentVersionProvider.P07ContentVersion or CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion or CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion))
            {
                throw new InvalidOperationException("SAVE_CONTENT_VERSION_UNSUPPORTED");
            }
            validator.Validate(game.Snapshot(), catalog);
            IsBootstrapped = true;
        }

        public MercenaryRosterResultDto GetRoster(MercenaryRosterQueryDto query = null)
        {
            EnsureReady();
            JObject document = game.Snapshot();
            IReadOnlyList<Mercenary> values = mapper.Read(document);
            return new GetMercenaryRosterQuery().Execute(values, query, catalog, document["payload"]!["kingdom"]!.Value<int>("ownedMercenaryLimit"), document["payload"]!["kingdom"]!.Value<int>("activeMercenaryLimit"));
        }

        public MercenaryDetailDto GetDetail(string instanceId)
        {
            EnsureReady();
            return new GetMercenaryDetailQuery().Execute(mapper.Read(game.Snapshot()), instanceId, catalog);
        }

        public void Reload()
        {
            EnsureReady();
            SaveLoadResult loaded = save.Repository.Load(game.ActiveProfileId);
            if (!loaded.Success) throw new MercenaryDomainException(loaded.ErrorCode ?? "MERCENARY_SAVE_WRITE_FAILED");
            validator.Validate(loaded.Document, catalog);
            game.SynchronizeCommittedDocument(loaded.Document);
        }

        public MercenaryOperationResult SetActive(SetMercenaryActiveCommand command)
        {
            EnsureReady();
            if (command == null) throw new ArgumentNullException(nameof(command));
            gate.Wait();
            try
            {
                string actualHash = new MercenaryRequestHasher().ComputeHash(command);
                Require(FixedTimeEquals(actualHash, command.RequestHash), "MERCENARY_OPERATION_HASH_MISMATCH");
                JObject current = game.Snapshot();
                IReadOnlyList<Mercenary> values = mapper.Read(current);
                Mercenary value = values.SingleOrDefault(item => item.InstanceId == command.MercenaryInstanceId.ToString("D")) ?? throw new MercenaryDomainException("MERCENARY_NOT_FOUND");
                if (value.Active == command.DesiredActive)
                {
                    Require(command.ExpectedRevision <= game.Revision, "MERCENARY_SAVE_REVISION_CONFLICT");
                    return new MercenaryOperationResult(command.OperationId, game.Revision, value.InstanceId, value.Active, true);
                }
                Require(command.ExpectedRevision == game.Revision, "MERCENARY_SAVE_REVISION_CONFLICT");
                int activeCount = values.Count(item => item.Active);
                int activeLimit = current["payload"]!["kingdom"]!.Value<int>("activeMercenaryLimit");
                MercenaryActivityPolicy.RequireAllowed(value, command.DesiredActive, activeCount, activeLimit);
                var draft = (JObject)current.DeepClone();
                mapper.SetActive(draft, value.InstanceId, command.DesiredActive);
                validator.Validate(draft, catalog);
                SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, command.ExpectedRevision, clock.UtcNow);
                if (!written.Success) throw new MercenaryDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "MERCENARY_SAVE_REVISION_CONFLICT" : "MERCENARY_SAVE_WRITE_FAILED");
                game.SynchronizeCommittedDocument(written.Document);
                int afterCount = mapper.Read(written.Document).Count(item => item.Active);
                ActivityChanged?.Invoke(this, new MercenaryActivatedEventArgs(value.InstanceId, command.DesiredActive, afterCount, activeLimit, written.Document.Value<long>("revision")));
                return new MercenaryOperationResult(command.OperationId, game.Revision, value.InstanceId, command.DesiredActive, false);
            }
            finally { gate.Release(); }
        }

        public void Shutdown()
        {
            ActivityChanged = null;
            RosterMigrated = null;
            IsBootstrapped = false;
            catalog = null;
            validator = null;
            game = null;
            content = null;
            save = null;
            gate.Dispose();
        }

        private void EnsureReady() { if (!IsBootstrapped) throw new InvalidOperationException("P05_GAME_NOT_BOOTSTRAPPED"); }
        private static void Require(bool condition, string code) { if (!condition) throw new MercenaryDomainException(code); }
        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0; for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index]; return difference == 0;
        }
    }
}
