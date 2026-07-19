using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Application.Mercenaries;
using KingdomTycoon.Application.Mercenaries.Commands;
using KingdomTycoon.Domain.Mercenaries;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Content.Migrations;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P05MercenaryTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 7, 20, 0, 0, 0, TimeSpan.Zero);
        private string temporaryRoot;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private MercenaryRosterService roster;
        private CanonicalMercenaryCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p05-tests");
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
            Directory.CreateDirectory(temporaryRoot);
            clock = new DeterministicClock(FixedNow);
            string assets = UnityEngine.Application.dataPath;
            string schema = ReadAsset("KingdomTycoon/Resources/Contracts/save.schema.json");
            string p04 = ReadAsset("KingdomTycoon/Resources/Contracts/p04-new-game.template.json");
            string p05 = ReadAsset("KingdomTycoon/Resources/Contracts/p05-new-game.template.json");
            string migration = ReadAsset("KingdomTycoon/Resources/Contracts/p05-migration-after.golden.json");
            services = new ServiceRegistry();
            services.Register(new SaveService(temporaryRoot, schema));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(assets, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, p05, p04);
            roster = new MercenaryRosterService(clock, migration, new SequenceUuidV7Provider(MigrationIds));
            services.Register(game);
            services.Register(roster);
            services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
            game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult();
            roster.Bootstrap();
            catalog = roster.Catalog;
        }

        [TearDown]
        public void TearDown()
        {
            services?.ShutdownAll();
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void P05ContentGolden_HasExactManifestAndLocalizationDigests()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.3");
            JObject manifest = StrictJson.ParseObject(File.ReadAllText(Path.Combine(root, "content_manifest.json")));
            Assert.That(Rfc8785Canonicalizer.Canonicalize(manifest).Length, Is.EqualTo(67270));
            Assert.That(Rfc8785Canonicalizer.ComputeSha256(manifest), Is.EqualTo("352a947980b09b0dc5d5699a9367bd6e35cfb1296241784d4c52bc80df0a98e1"));
            byte[] localization = File.ReadAllBytes(Path.Combine(root, "localizations.csv"));
            Assert.That(Sha256(localization), Is.EqualTo("60feb5d4c780aaa52fc7808d206d690950883432e2608f95034050c88f5f1e18"));
            ContentImportResult imported = new CsvContentImporter().Import(manifest.ToString(), file => File.ReadAllText(Path.Combine(root, file)));
            Assert.That(imported.IsValid, Is.True, string.Join("\n", imported.Report.Issues));
            Assert.That(imported.Catalog.Tables.Count, Is.EqualTo(62));
            Assert.That(imported.Catalog.GetTable("localizations.csv").Rows.Count, Is.EqualTo(806));
        }

        [Test]
        public void RequestHash_MatchesP05Golden()
        {
            var command = new SetMercenaryActiveCommand(Guid.Parse("019f7cd2-8800-7002-8000-000000000001"), null, 2,
                Guid.Parse("019f7cd2-8800-7002-8000-000000000001"), false);
            Assert.That(new MercenaryRequestHasher().ComputeHash(command), Is.EqualTo("8b19c3a58c3803be9fa801b09a281cb975e76b2fa900e48df1c6314475d9a37c"));
        }

        [Test]
        public void NewGame_BootstrapsFourCanonicalStartersAndLodgeLimits()
        {
            MercenaryRosterResultDto result = roster.GetRoster();
            Assert.That(game.CurrentDocument.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.3"));
            Assert.That(result.TotalOwned, Is.EqualTo(4));
            Assert.That(result.TotalActive, Is.EqualTo(4));
            Assert.That(result.OwnedLimit, Is.EqualTo(8));
            Assert.That(result.ActiveLimit, Is.EqualTo(4));
            Assert.That(result.Cards.Select(value => value.DisplayName), Is.EquivalentTo(new[] { "레온", "미라", "아린", "세라" }));
        }

        [Test]
        public void Migration_EmptyAddsFour_ExistingPreserves_AndP05RejectsSecondApply()
        {
            JObject before = StrictJson.ParseObject(ReadAsset("KingdomTycoon/Resources/Contracts/p04-new-game.template.json"));
            var migration = NewMigration(new SequenceUuidV7Provider(MigrationIds));
            JObject after = migration.Apply(before);
            Assert.That(after.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.3"));
            Assert.That(after["payload"]["mercenaries"].Values<JObject>().Select(value => value.Value<string>("instanceId")), Is.EqualTo(MigrationIds.Select(value => value.ToString("D"))));
            Assert.That(migration.CanApply(after), Is.False);
            Assert.That(() => migration.Apply(after), Throws.InvalidOperationException.With.Message.EqualTo("P05_MIGRATION_VALIDATION_FAILED"));

            JObject nonEmpty = StrictJson.ParseObject(ReadAsset("KingdomTycoon/Resources/Contracts/p04-new-game.template.json"));
            JObject starter = (JObject)StrictJson.ParseObject(ReadAsset("KingdomTycoon/Resources/Contracts/p05-migration-after.golden.json"))["payload"]["mercenaries"][0];
            nonEmpty["payload"]["mercenaries"] = new JArray(starter.DeepClone());
            JObject preserved = NewMigration(new SequenceUuidV7Provider(Array.Empty<Guid>())).Apply(nonEmpty);
            Assert.That(preserved["payload"]["mercenaries"].Count(), Is.EqualTo(1));
            Assert.That(preserved["payload"]["mercenaries"][0].Value<string>("instanceId"), Is.EqualTo(starter.Value<string>("instanceId")));
        }

        [TestCase("SAVE_MERCENARY_ID_INVALID")]
        [TestCase("SAVE_MERCENARY_GENERATION_INVALID")]
        [TestCase("SAVE_MERCENARY_CONTENT_REF_INVALID")]
        [TestCase("MERCENARY_GRADE_IMMUTABLE")]
        [TestCase("SAVE_MERCENARY_LEVEL_INVALID")]
        [TestCase("SAVE_MERCENARY_TRAITS_INVALID")]
        [TestCase("SAVE_MERCENARY_COUNTER_INVALID")]
        [TestCase("SAVE_MERCENARY_SLOT_LIMIT_EXCEEDED")]
        [TestCase("SAVE_MERCENARY_INACTIVE_STATE_INVALID")]
        [TestCase("SAVE_MERCENARY_AUTONOMY_INVALID")]
        [TestCase("SAVE_MERCENARY_POTION_INVALID")]
        [TestCase("SAVE_MERCENARY_EQUIPMENT_LINK_INVALID")]
        [TestCase("SAVE_MERCENARY_PROMOTION_INVALID")]
        [TestCase("SAVE_MERCENARY_RECORD_INVALID")]
        [TestCase("SAVE_MERCENARY_NAME_INVALID")]
        [TestCase("SAVE_MERCENARY_LODGE_LIMIT_MISMATCH")]
        public void InvariantValidator_RejectsEachContractBoundaryWithExactCode(string expected)
        {
            JObject document = StrictJson.ParseObject(ReadAsset("KingdomTycoon/Resources/Contracts/p05-migration-after.golden.json"));
            JObject mercenary = (JObject)document["payload"]["mercenaries"][0];
            IMercenaryCatalogRules rules = catalog;
            switch (expected)
            {
                case "SAVE_MERCENARY_ID_INVALID": mercenary["instanceId"] = "not-a-uuid"; break;
                case "SAVE_MERCENARY_GENERATION_INVALID": mercenary["nameSeed"] = null; break;
                case "SAVE_MERCENARY_CONTENT_REF_INVALID": mercenary["jobId"] = "JOB_UNKNOWN"; break;
                case "MERCENARY_GRADE_IMMUTABLE": mercenary["gradeId"] = "GRADE_X"; rules = new GradePermissiveCatalog(catalog); break;
                case "SAVE_MERCENARY_LEVEL_INVALID": mercenary["level"] = 999; break;
                case "SAVE_MERCENARY_TRAITS_INVALID": ((JArray)mercenary["traitIds"]).Add(mercenary["traitIds"][0]); break;
                case "SAVE_MERCENARY_COUNTER_INVALID": mercenary["personalGold"] = -1; break;
                case "SAVE_MERCENARY_SLOT_LIMIT_EXCEEDED": document["payload"]["kingdom"]["activeMercenaryLimit"] = 3; break;
                case "SAVE_MERCENARY_INACTIVE_STATE_INVALID": mercenary["active"] = false; mercenary["autonomy"]["state"] = "COMBAT"; break;
                case "SAVE_MERCENARY_AUTONOMY_INVALID": mercenary["autonomy"]["reasonCode"] = "UNKNOWN"; break;
                case "SAVE_MERCENARY_POTION_INVALID": ((JArray)mercenary["potions"]).Add(new JObject { ["potionId"] = "POTION_UNKNOWN", ["quantity"] = 1 }); break;
                case "SAVE_MERCENARY_EQUIPMENT_LINK_INVALID": ((JObject)mercenary["equipmentSlots"]).Property("WEAPON")?.Remove(); break;
                case "SAVE_MERCENARY_PROMOTION_INVALID": mercenary["promotion"]["status"] = "UNKNOWN"; break;
                case "SAVE_MERCENARY_RECORD_INVALID": ((JObject)mercenary["records"]).Property("huntCount")?.Remove(); break;
                case "SAVE_MERCENARY_NAME_INVALID": mercenary["displayName"] = " 레온"; break;
                case "SAVE_MERCENARY_LODGE_LIMIT_MISMATCH": document["payload"]["facilities"].Values<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_LODGE")["level"] = 2; break;
            }
            MercenaryDomainException error = Assert.Throws<MercenaryDomainException>(() => new MercenaryInvariantValidator().Validate(document, rules));
            Assert.That(error.ErrorCode, Is.EqualTo(expected));
        }

        [Test]
        public void InvariantValidatorAcceptsCanonicalEquipmentTemplateIdLink()
        {
            JObject document = StrictJson.ParseObject(ReadAsset("KingdomTycoon/Resources/Contracts/p05-migration-after.golden.json"));
            JObject mercenary = (JObject)document["payload"]!["mercenaries"]![0]!;
            string instanceId = "019f7cd2-8800-7002-8000-000000000099";
            mercenary["equipmentSlots"]!["WEAPON"] = instanceId;
            ((JArray)document["payload"]!["inventory"]!["equipment"]!).Add(new JObject
            {
                ["instanceId"] = instanceId,
                ["equipmentTemplateId"] = "EQ_T1_WARRIOR_WEAPON",
                ["equippedByMercenaryInstanceId"] = mercenary.Value<string>("instanceId")
            });
            Assert.That(() => new MercenaryInvariantValidator().Validate(document, catalog), Throws.Nothing);
        }

        [Test]
        public void ActivityToggle_CommitsOnce_ReplayDoesNotCommit_StaleRevisionConflicts()
        {
            string id = roster.GetRoster().Cards[0].InstanceId;
            long before = roster.Revision;
            var factory = new MercenaryOperationRequestFactory(new SequenceUuidV7Provider(new[] { Guid.Parse("019f7cd2-8800-7002-8000-000000000101"), Guid.Parse("019f7cd2-8800-7002-8000-000000000102") }), new MercenaryRequestHasher());
            SetMercenaryActiveCommand deactivate = factory.CreateSetActive(before, Guid.Parse(id), false);
            MercenaryOperationResult first = roster.SetActive(deactivate);
            Assert.That(first.Replayed, Is.False);
            Assert.That(roster.Revision, Is.EqualTo(before + 1));
            Assert.That(roster.GetRoster().TotalActive, Is.EqualTo(3));
            Assert.That(roster.SetActive(deactivate).Replayed, Is.True);
            Assert.That(roster.Revision, Is.EqualTo(before + 1));
            SetMercenaryActiveCommand stale = factory.CreateSetActive(before, Guid.Parse(id), true);
            MercenaryDomainException conflict = Assert.Throws<MercenaryDomainException>(() => roster.SetActive(stale));
            Assert.That(conflict.ErrorCode, Is.EqualTo("MERCENARY_SAVE_REVISION_CONFLICT"));
            Assert.That(roster.Revision, Is.EqualTo(before + 1));
            Assert.That(services.Get<SaveService>().Repository.Load(game.ActiveProfileId).Document.Value<long>("revision"), Is.EqualTo(before + 1));
        }

        [Test]
        public void ActivityPolicy_EnforcesLimitAndTownSafeState()
        {
            Mercenary idle = MercenaryValue(active: false, state: "IDLE_TOWN");
            Assert.That(() => MercenaryActivityPolicy.RequireAllowed(idle, true, 4, 4), Throws.TypeOf<MercenaryDomainException>().With.Property("ErrorCode").EqualTo("MERCENARY_ACTIVE_LIMIT_REACHED"));
            Mercenary combat = MercenaryValue(active: true, state: "COMBAT", region: "REGION_MEADOW");
            Assert.That(() => MercenaryActivityPolicy.RequireAllowed(combat, false, 4, 4), Throws.TypeOf<MercenaryDomainException>().With.Property("ErrorCode").EqualTo("MERCENARY_ACTIVE_CHANGE_STATE_FORBIDDEN"));
        }

        [Test]
        public void RosterQuery_AppliesSearchFiltersAndStableSorts()
        {
            MercenaryRosterResultDto search = roster.GetRoster(new MercenaryRosterQueryDto(search: "레온"));
            Assert.That(search.FilteredCount, Is.EqualTo(1));
            Assert.That(search.Cards[0].DisplayName, Is.EqualTo("레온"));
            MercenaryRosterResultDto cleric = roster.GetRoster(new MercenaryRosterQueryDto(jobIds: new[] { "JOB_CLERIC" }));
            Assert.That(cleric.Cards.Select(value => value.DisplayName), Is.EqualTo(new[] { "세라" }));
            MercenaryRosterResultDto names = roster.GetRoster(new MercenaryRosterQueryDto(sortId: "NAME_ASC"));
            Assert.That(names.Cards.Select(value => value.DisplayName), Is.EqualTo(names.Cards.Select(value => value.DisplayName).OrderBy(MercenaryRosterFilter.Normalize, StringComparer.Ordinal)));
            MercenaryRosterResultDto none = roster.GetRoster(new MercenaryRosterQueryDto(activeFilter: "INACTIVE"));
            Assert.That(none.TotalOwned, Is.EqualTo(4));
            Assert.That(none.FilteredCount, Is.Zero);
        }

        [Test]
        public void RosterQuery_24FixtureCoversAllDimensionsSortsAndPerformanceBudget()
        {
            IReadOnlyList<Mercenary> values = DiverseRoster(24);
            var query = new GetMercenaryRosterQuery();
            var filter = new MercenaryRosterQueryDto(
                jobIds: new[] { "JOB_WARRIOR", "JOB_CLERIC" },
                gradeIds: new[] { "GRADE_C", "GRADE_B" },
                activeFilter: "ACTIVE");
            MercenaryRosterResultDto result = query.Execute(values, filter, catalog, 24, 16);
            string[] expected = values.Where(value => (value.JobId is "JOB_WARRIOR" or "JOB_CLERIC")
                    && (value.GradeId is "GRADE_C" or "GRADE_B") && value.Active)
                .OrderBy(value => value, new MercenaryRosterComparer("DEFAULT", catalog)).Select(value => value.InstanceId).ToArray();
            Assert.That(result.Cards.Select(value => value.InstanceId), Is.EqualTo(expected));

            foreach (string sortId in new[] { "DEFAULT", "NAME_ASC", "NAME_DESC", "LEVEL_ASC", "LEVEL_DESC", "GRADE_ASC", "GRADE_DESC", "RANK_ASC", "RANK_DESC" })
            {
                string[] first = query.Execute(values, new MercenaryRosterQueryDto(sortId: sortId), catalog, 24, 16).Cards.Select(value => value.InstanceId).ToArray();
                string[] second = query.Execute(values, new MercenaryRosterQueryDto(sortId: sortId), catalog, 24, 16).Cards.Select(value => value.InstanceId).ToArray();
                Assert.That(first, Is.EqualTo(second), sortId);
                Assert.That(first.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(24), sortId);
            }

            var samples = new List<double>();
            for (int index = 0; index < 100; index++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                query.Execute(values, filter, catalog, 24, 16);
                stopwatch.Stop();
                samples.Add(stopwatch.Elapsed.TotalMilliseconds);
            }
            Assert.That(samples.OrderBy(value => value).ElementAt(94), Is.LessThanOrEqualTo(2.0), "P05 query p95");
        }

        [Test]
        public void QueryInput_EnforcesUnicodeScalarLimitAndStrictIdentifiers()
        {
            string twentyEmoji = string.Concat(Enumerable.Repeat("😀", 20));
            Assert.That(new MercenaryRosterQueryDto(search: twentyEmoji).Search, Is.EqualTo(twentyEmoji));
            Assert.That(() => new MercenaryRosterQueryDto(search: twentyEmoji + "😀"), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MercenaryRosterQueryDto(activeFilter: "UNKNOWN"), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new MercenaryRosterQueryDto(sortId: "UNKNOWN"), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(MercenaryRosterQueryDigest.Compute(new MercenaryRosterQueryDto()).Length, Is.EqualTo(64));
        }

        [Test]
        public void ActivityToggle_RoundTripsSemanticSaveDocument()
        {
            string id = roster.GetRoster().Cards[0].InstanceId;
            var factory = new MercenaryOperationRequestFactory(
                new SequenceUuidV7Provider(new[] { Guid.Parse("019f7cd2-8800-7002-8000-000000000121") }),
                new MercenaryRequestHasher());
            roster.SetActive(factory.CreateSetActive(roster.Revision, Guid.Parse(id), false));
            JObject committed = game.Snapshot();
            JObject reloaded = services.Get<SaveService>().Repository.Load(game.ActiveProfileId).Document;
            Assert.That(JToken.DeepEquals(committed, reloaded), Is.True);
        }

        [Test]
        public void InternalCreate_RejectsOwnedCapAndDuplicateIdWithoutMutatingSource()
        {
            JObject source = StrictJson.ParseObject(ReadAsset("KingdomTycoon/Resources/Contracts/p05-migration-after.golden.json"));
            JObject duplicate = (JObject)source["payload"]["mercenaries"][0].DeepClone();
            var create = new CreateMercenaryFromSnapshot(new MercenaryInvariantValidator(), catalog);
            Assert.That(() => create.ExecuteInternal(source, duplicate), Throws.TypeOf<MercenaryDomainException>()
                .With.Property("ErrorCode").EqualTo("SAVE_MERCENARY_ID_INVALID"));
            Assert.That(source["payload"]["mercenaries"].Count(), Is.EqualTo(4));
            source["payload"]["kingdom"]["ownedMercenaryLimit"] = 4;
            duplicate["instanceId"] = "019f7cd2-8800-7002-8000-000000000099";
            Assert.That(() => create.ExecuteInternal(source, duplicate), Throws.TypeOf<MercenaryDomainException>()
                .With.Property("ErrorCode").EqualTo("MERCENARY_OWNED_LIMIT_REACHED"));
        }

        private P04ToP05ContentMigration NewMigration(IUuidV7Provider ids) => new(
            ReadAsset("KingdomTycoon/Resources/Contracts/p05-migration-after.golden.json"), ids, clock,
            new MercenaryInvariantValidator(), catalog);

        private static Mercenary MercenaryValue(bool active, string state, string region = null) => new(
            "019f7cd2-8800-7002-8000-000000000111", "테스트", "JOB_WARRIOR", "GRADE_C", "RANK_APPRENTICE", 1, 0,
            "PERSONALITY_PRACTICAL", Array.Empty<string>(), 0, 0, active, state, "NONE", region, "NONE",
            new Dictionary<string, string>(), new Dictionary<string, long>(), new Dictionary<string, long>());

        private static IReadOnlyList<Mercenary> DiverseRoster(int count)
        {
            string[] jobs = { "JOB_WARRIOR", "JOB_GUARDIAN", "JOB_ARCHER", "JOB_MAGE", "JOB_CLERIC" };
            string[] grades = { "GRADE_C", "GRADE_B", "GRADE_A", "GRADE_S", "GRADE_SS" };
            string[] ranks = { "RANK_APPRENTICE", "RANK_REGULAR", "RANK_SKILLED", "RANK_ELITE", "RANK_HERO", "RANK_LEGEND" };
            string[] states = { "IDLE_TOWN", "PREPARE", "TRAVEL_TO_REGION", "FIND_TARGET", "COMBAT", "LOOT", "CONTINUE_DECISION", "RETURN_TOWN", "SELL_LOOT", "HEAL", "BUY_CONSUMABLES", "EVALUATE_EQUIPMENT", "BUY_EQUIPMENT", "PROMOTION_READY", "PROMOTION_PROCESS", "INJURED", "RAID_READY" };
            var values = new List<Mercenary>();
            for (int index = 0; index < count; index++)
            {
                string state = states[index % states.Length];
                values.Add(new Mercenary(
                    $"019f7cd2-8800-7002-8000-{index + 1:000000000000}", $"용병{index:00}", jobs[index % jobs.Length], grades[index % grades.Length], ranks[index % ranks.Length],
                    1 + index % 20, index * 10, "PERSONALITY_PRACTICAL", Array.Empty<string>(), index, index * 2, index % 3 != 0,
                    state, "NONE", state is "IDLE_TOWN" or "PROMOTION_READY" or "INJURED" ? null : "REGION_MEADOW",
                    index % 7 == 0 ? "READY" : "NONE", new Dictionary<string, string>(), new Dictionary<string, long>(), new Dictionary<string, long>()));
            }
            return values;
        }

        private static readonly Guid[] MigrationIds =
        {
            Guid.Parse("019f7cd2-8800-7002-8000-000000000001"), Guid.Parse("019f7cd2-8800-7002-8000-000000000002"),
            Guid.Parse("019f7cd2-8800-7002-8000-000000000003"), Guid.Parse("019f7cd2-8800-7002-8000-000000000004")
        };

        private static string ReadAsset(string relative) => File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar)));
        private static string Sha256(byte[] value) { using SHA256 sha = SHA256.Create(); return string.Concat(sha.ComputeHash(value).Select(item => item.ToString("x2"))); }

        private sealed class SequenceUuidV7Provider : IUuidV7Provider
        {
            private readonly Queue<Guid> values;
            public SequenceUuidV7Provider(IEnumerable<Guid> source) => values = new Queue<Guid>(source);
            public Guid NewId() => values.Count > 0 ? values.Dequeue() : throw new InvalidOperationException("P05_TEST_UUID_SEQUENCE_EXHAUSTED");
        }

        private sealed class GradePermissiveCatalog : IMercenaryCatalogRules
        {
            private readonly IMercenaryCatalogRules inner;
            public GradePermissiveCatalog(IMercenaryCatalogRules inner) => this.inner = inner;
            public bool HasJob(string id) => inner.HasJob(id); public bool HasGrade(string id) => id == "GRADE_X" || inner.HasGrade(id); public bool HasRank(string id) => inner.HasRank(id);
            public bool HasPersonality(string id) => inner.HasPersonality(id); public bool HasTrait(string id) => inner.HasTrait(id); public bool HasPotion(string id) => inner.HasPotion(id);
            public bool IsTraitEligible(string traitId, string jobId) => inner.IsTraitEligible(traitId, jobId); public bool IsEquipmentEligible(string templateId, string jobId) => inner.IsEquipmentEligible(templateId, jobId);
            public int GradeOrder(string id) => id == "GRADE_X" ? 0 : inner.GradeOrder(id); public int RankOrder(string id) => inner.RankOrder(id); public int JobOrder(string id) => inner.JobOrder(id);
            public int GradeTraitSlots(string id) => id == "GRADE_X" ? 2 : inner.GradeTraitSlots(id); public int RankTraitSlots(string id) => inner.RankTraitSlots(id); public int RankMaxLevel(string id) => inner.RankMaxLevel(id);
            public string Localize(string textKey) => inner.Localize(textKey);
        }
    }
}
