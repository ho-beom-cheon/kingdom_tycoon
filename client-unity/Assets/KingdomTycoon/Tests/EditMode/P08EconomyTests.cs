using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Economy;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Domain.Economy;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Economy;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P08EconomyTests
    {
        private string temporaryRoot;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private EconomyGameService economy;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p08-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero));
            string contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            string legacy = File.ReadAllText(Path.Combine(contracts, "save.schema.json"));
            string content5 = File.ReadAllText(Path.Combine(contracts, "save.content.5.schema.json"));
            string content6 = File.ReadAllText(Path.Combine(contracts, "save.content.6.schema.json"));
            string template = File.ReadAllText(Path.Combine(contracts, "p08-new-game.template.json"));
            string p04 = File.ReadAllText(Path.Combine(contracts, "p04-new-game.template.json"));
            services = new ServiceRegistry();
            services.Register(new SaveService(temporaryRoot, legacy, content5, content6));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, template, p04); services.Register(game);
            economy = new EconomyGameService(clock); services.Register(economy);
            services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
            game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult();
            economy.Bootstrap();
        }

        [TearDown]
        public void TearDown()
        {
            services?.ShutdownAll();
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void GeneratedContentImportsSeventyFiveTablesAndSchemasRejectMixedVersions()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.6");
            JObject manifest = StrictJson.ParseObject(File.ReadAllText(Path.Combine(root, "content_manifest.json")));
            ContentImportResult imported = new CsvContentImporter().Import(manifest.ToString(), file => File.ReadAllText(Path.Combine(root, file)));
            Assert.That(imported.IsValid, Is.True, string.Join("\n", imported.Report.Issues));
            Assert.That(imported.Catalog.Tables.Count, Is.EqualTo(75));
            Assert.That(imported.Catalog.GetTable("pricing_policies.csv").Rows.Count, Is.EqualTo(3));
            Assert.That(imported.Catalog.GetTable("transaction_reason_codes.csv").Rows.Count, Is.EqualTo(10));

            SaveDocumentValidator validator = NewValidator();
            JObject p07 = StrictJson.ParseObject(ReadGolden("p08-migration-buildable-before.golden.json"));
            var p07Report = validator.Validate(p07, "P07");
            Assert.That(p07Report.IsValid, Is.True, string.Join("\n", p07Report.Issues));
            p07["payload"]!["economy"] = P07ToP08ContentMigration.NewEconomy();
            Assert.That(validator.Validate(p07, "MIXED").IsValid, Is.False);
        }

        [Test]
        public void P07MigrationMatchesBuildableActiveAndStoppedGoldens()
        {
            foreach (string state in new[] { "buildable", "active", "stopped" })
            {
                JObject source = StrictJson.ParseObject(ReadGolden($"p08-migration-{state}-before.golden.json"));
                JObject expected = StrictJson.ParseObject(ReadGolden($"p08-migration-{state}-after.golden.json"));
                JObject migrated = new P07ToP08ContentMigration().Apply(source);
                JObject committed = NewValidator().PrepareForCommit(migrated, source.Value<long>("revision"), clock.UtcNow);
                Assert.That(JToken.DeepEquals(committed, expected), Is.True, state + "\n" + committed);
            }
        }

        [Test]
        public void PricingUsesExactIntegerFloorAndCeilingContracts()
        {
            var pricing = new StorePricingService();
            Assert.That(pricing.EquipmentAcquire(100), Is.EqualTo(25));
            Assert.That(pricing.EquipmentCustomer(100, 11_500), Is.EqualTo(115));
            Assert.That(pricing.PotionAcquire(40), Is.EqualTo(10));
            Assert.That(pricing.PotionCustomer(40, 11_500), Is.EqualTo(46));
            Assert.That(pricing.ItemCustomer(5, 9_000), Is.EqualTo(9));
        }

        [Test]
        public void OpenSupplyBuyAndSellCommitWalletStockJournalAndLedgerAtomically()
        {
            OpenStore();
            string mercenaryId = game.CurrentDocument["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).First().Value<string>("instanceId");
            var hasher = new EconomyRequestHasher();

            Guid supplyId = Guid.Parse("018f0000-0000-7000-8000-000000000904");
            var supplyDraft = new RefreshSystemStoreSupplyCommand(supplyId, game.Revision, null, 0);
            EconomyOperationResult supplied = economy.RefreshSystemStoreSupply(new RefreshSystemStoreSupplyCommand(supplyId, game.Revision, hasher.Compute(supplyDraft), 0));
            Assert.That(supplied.StockVersion, Is.EqualTo(1));
            Assert.That(economy.GetStorefront(mercenaryId).Products.Count, Is.EqualTo(6));

            StoreProductDto potion = economy.GetStorefront(mercenaryId).Products.Single(value => value.Kind == "POTION");
            var buyLine = new StoreCommandLine("POTION", potion.Id, 2, potion.UnitPrice);
            string quote = economy.GetQuoteContextHash(mercenaryId, new[] { buyLine });
            Guid buyId = Guid.Parse("018f0000-0000-7000-8000-000000000903");
            var buyDraft = new BuyFromStoreCommand(buyId, game.Revision, null, quote, mercenaryId, false, new[] { buyLine });
            EconomyOperationResult bought = economy.BuyFromStore(new BuyFromStoreCommand(buyId, game.Revision, hasher.Compute(buyDraft), quote, mercenaryId, false, new[] { buyLine }));
            Assert.That((bought.PersonalGoldDelta, bought.KingdomGoldDelta), Is.EqualTo((-80L, 80L)));
            Assert.That(economy.GetStorefront(mercenaryId).PersonalGold, Is.EqualTo(420));

            JObject draft = game.Snapshot(); ((JArray)draft["payload"]!["inventory"]!["itemStacks"]!).Add(new JObject { ["itemId"] = "MAT_R01_WILD_HERB", ["quantity"] = 3 });
            SaveWriteResult seeded = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow);
            Assert.That(seeded.Success, Is.True, seeded.ErrorCode); game.SynchronizeCommittedDocument(seeded.Document);
            Guid sellId = Guid.Parse("018f0000-0000-7000-8000-000000000902");
            var sellLine = new StoreCommandLine("ITEM", "MAT_R01_WILD_HERB", 3);
            var sellDraft = new SellToStoreCommand(sellId, game.Revision, null, mercenaryId, new[] { sellLine });
            EconomyOperationResult sold = economy.SellToStore(new SellToStoreCommand(sellId, game.Revision, hasher.Compute(sellDraft), mercenaryId, new[] { sellLine }));
            Assert.That(sold.PersonalGoldDelta, Is.EqualTo(15));
            Assert.That(economy.GetStorefront(mercenaryId).PersonalGold, Is.EqualTo(435));
            JObject saved = services.Get<SaveService>().Repository.Load(game.ActiveProfileId).Document;
            Assert.That(saved["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!.Count(), Is.EqualTo(3));
            Assert.That(saved["payload"]!["operationJournal"]!.Children<JObject>().Count(value => value.Value<string>("operationType") == "STORE_TRANSACTION"), Is.EqualTo(3));
            Assert.That(NewValidator().Validate(saved, "COMMITTED").IsValid, Is.True);
        }

        [Test]
        public void AutonomyCycleSellsLootThenBuysPotionsAndEligibleEquipmentWithinBound()
        {
            OpenStore(); economy.EnsureSystemSupply();
            JObject seed = game.Snapshot();
            ((JArray)seed["payload"]!["inventory"]!["itemStacks"]!).Add(new JObject { ["itemId"] = "MAT_R01_WILD_HERB", ["quantity"] = 4 });
            SaveWriteResult seeded = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, seed, game.Revision, clock.UtcNow);
            Assert.That(seeded.Success, Is.True, seeded.ErrorCode); game.SynchronizeCommittedDocument(seeded.Document);

            int commands = economy.RunStoreAutonomyCycle(Guid.Parse("018f0000-0000-7000-8000-000000000908"));
            Assert.That(commands, Is.InRange(3, 48));
            JObject saved = game.Snapshot();
            Assert.That(saved["payload"]!["inventory"]!["itemStacks"]!.Count(), Is.EqualTo(0));
            Assert.That(saved["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!.Count(), Is.EqualTo(commands + 1));
            Assert.That(saved["payload"]!["mercenaries"]!.Children<JObject>().Any(value => value["potions"]!.Children<JObject>().Any(line => line.Value<long>("quantity") >= 3)), Is.True);
            Assert.That(saved["payload"]!["mercenaries"]!.Children<JObject>().Any(value => value["equipmentSlots"]!.Children<JProperty>().Any(slot => slot.Value.Type == JTokenType.String)), Is.True);
            Assert.That(NewValidator().Validate(saved, "AUTONOMY").IsValid, Is.True);
        }

        [Test]
        public void AutonomyCycleCreatesInitialSupplyWithoutOpeningStoreScreen()
        {
            OpenStore();
            Assert.That(economy.GetStorefront().Products, Is.Empty);

            int commands = economy.RunStoreAutonomyCycle(Guid.Parse("018f0000-0000-7000-8000-000000000910"));

            Assert.That(commands, Is.InRange(1, 48));
            Assert.That(economy.GetStorefront().Products, Is.Not.Empty);
            JObject supply = (JObject)game.Snapshot()["payload"]!["economy"]!["store"]!["supplyState"]!;
            Assert.That(supply["lastRefreshOperationId"]!.Type, Is.EqualTo(JTokenType.String));
            Assert.That(supply.Value<long>("epoch"), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void EighthPurchaseTriggersDeterministicExplicitSupplyEpochRefresh()
        {
            OpenStore(); EconomyOperationResult initial = economy.EnsureSystemSupply(); Assert.That(initial, Is.Not.Null);
            string mercenaryId = game.CurrentDocument["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).First().Value<string>("instanceId");
            var hasher = new EconomyRequestHasher();
            for (int index = 0; index < 8; index++)
            {
                StoreProductDto potion = economy.GetStorefront(mercenaryId).Products.Single(value => value.Kind == "POTION");
                var line = new StoreCommandLine("POTION", potion.Id, 1, potion.UnitPrice); string quote = economy.GetQuoteContextHash(mercenaryId, new[] { line });
                Guid operation = Guid.Parse($"018f0000-0000-7000-8000-{index + 1:000000000000}");
                var draft = new BuyFromStoreCommand(operation, game.Revision, null, quote, mercenaryId, false, new[] { line });
                economy.BuyFromStore(new BuyFromStoreCommand(operation, game.Revision, hasher.Compute(draft), quote, mercenaryId, false, new[] { line }));
            }
            JObject before = game.Snapshot(); Assert.That(before["payload"]!["economy"]!["store"]!["supplyState"]!.Value<int>("buyCountSinceRefresh"), Is.EqualTo(8));
            EconomyOperationResult refreshed = economy.EnsureSystemSupply(); Assert.That(refreshed, Is.Not.Null);
            JObject after = game.Snapshot(); JObject state = (JObject)after["payload"]!["economy"]!["store"]!["supplyState"]!;
            Assert.That(state.Value<long>("epoch"), Is.EqualTo(2)); Assert.That(state.Value<int>("buyCountSinceRefresh"), Is.EqualTo(0));
            Assert.That(economy.GetStorefront(mercenaryId).Products.Single(value => value.Kind == "POTION").Quantity, Is.EqualTo(8));
            Assert.That(economy.EnsureSystemSupply(), Is.Null);
        }

        [Test]
        public void SixPersonalitiesAndSixteenMercenariesRemainBoundedAndCycleReplayIsIdempotent()
        {
            OpenStore(); economy.EnsureSystemSupply(); JObject seed = game.Snapshot(); JObject[] starters = seed["payload"]!["mercenaries"]!.Children<JObject>().ToArray();
            string[] personalities = { "PERSONALITY_PRACTICAL", "PERSONALITY_FRUGAL", "PERSONALITY_GEARHEAD", "PERSONALITY_BRAVE", "PERSONALITY_CAUTIOUS", "PERSONALITY_COLLECTOR" };
            var mercenaries = new JArray();
            for (int index = 0; index < 16; index++)
            {
                JObject value = (JObject)starters[index % starters.Length].DeepClone();
                value["instanceId"] = $"019f7cd2-9900-7002-8000-{index + 1:000000000000}"; value["displayName"] = "정산용병" + (index + 1).ToString("00"); value["personalityId"] = personalities[index % personalities.Length]; value["personalGold"] = 1_000; value["active"] = true;
                value["potions"] = new JArray(); value["autonomy"]!["state"] = "IDLE_TOWN"; value["autonomy"]!["reasonCode"] = "NONE";
                foreach (JProperty slot in value["equipmentSlots"]!.Children<JProperty>()) slot.Value = JValue.CreateNull();
                mercenaries.Add(value);
            }
            seed["payload"]!["mercenaries"] = mercenaries; seed["payload"]!["kingdom"]!["activeMercenaryLimit"] = 16; seed["payload"]!["kingdom"]!["ownedMercenaryLimit"] = 24; seed["payload"]!["kingdom"]!["facilityUpgradeCount"] = 3;
            seed["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_LODGE")["level"] = 4;
            SaveWriteResult seeded = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, seed, game.Revision, clock.UtcNow);
            Assert.That(seeded.Success, Is.True, seeded.ErrorCode); game.SynchronizeCommittedDocument(seeded.Document);
            Assert.That(seed["payload"]!["mercenaries"]!.Children<JObject>().Select(value => value.Value<string>("personalityId")).Distinct().Count(), Is.EqualTo(6));

            Guid cycle = Guid.Parse("018f0000-0000-7000-8000-000000000909"); int commands = economy.RunStoreAutonomyCycle(cycle); long revision = game.Revision;
            Assert.That(commands, Is.InRange(1, 48)); Assert.That(economy.RunStoreAutonomyCycle(cycle), Is.EqualTo(commands)); Assert.That(game.Revision, Is.EqualTo(revision));
            Assert.That(NewValidator().Validate(game.Snapshot(), "CONTENTION").IsValid, Is.True);
        }

        private void OpenStore()
        {
            var requests = new FacilityOperationRequestFactory(new SystemUuidV7Provider(), new FacilityRequestHasher());
            var build = requests.CreateBuild(game.Revision, "FAC_STORE"); game.StartBuild(build); clock.Advance(TimeSpan.FromSeconds(31));
            Assert.That(game.NormalizeExpiredJobs(), Is.True); game.Claim(requests.CreateClaim(game.Revision, "FAC_STORE", build.OperationId));
            Guid merchant = Guid.Parse(game.CurrentDocument["payload"]!["managementNpcs"]!.Children<JObject>().Single(value => value.Value<string>("professionId") == "NPC_MERCHANT").Value<string>("instanceId"));
            game.Assign(requests.CreateAssign(game.Revision, "FAC_STORE", merchant));
            Assert.That(economy.GetStorefront().Availability, Is.EqualTo("OPEN"));
        }

        private static SaveDocumentValidator NewValidator()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            return new SaveDocumentValidator(File.ReadAllText(Path.Combine(root, "save.schema.json")), File.ReadAllText(Path.Combine(root, "save.content.5.schema.json")), File.ReadAllText(Path.Combine(root, "save.content.6.schema.json")));
        }
        private static string ReadGolden(string name) => File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P08", name));
    }
}
