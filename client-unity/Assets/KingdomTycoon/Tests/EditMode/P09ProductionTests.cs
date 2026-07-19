using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Production;
using KingdomTycoon.Domain.Production;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Production;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P09ProductionTests
    {
        private string temporaryRoot;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private ProductionGameService production;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p09-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryRoot);
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero)); string contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            services = new ServiceRegistry(); services.Register(new SaveService(temporaryRoot, Read(contracts, "save.schema.json"), Read(contracts, "save.content.5.schema.json"), Read(contracts, "save.content.6.schema.json"), Read(contracts, "save.content.7.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, Read(contracts, "p09-new-game.template.json"), Read(contracts, "p04-new-game.template.json")); services.Register(game);
            production = new ProductionGameService(clock); services.Register(production); services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult(); game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult(); production.Bootstrap();
        }

        [TearDown]
        public void TearDown() { services?.ShutdownAll(); if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }

        [Test]
        public void ContentAndNewGameContractsValidateAsSeventyEightTablePackage()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.7"); JObject manifest = StrictJson.ParseObject(File.ReadAllText(Path.Combine(root, "content_manifest.json")));
            ContentImportResult imported = new CsvContentImporter().Import(manifest.ToString(), file => File.ReadAllText(Path.Combine(root, file)));
            Assert.That(imported.IsValid, Is.True, string.Join("\n", imported.Report.Issues)); Assert.That(imported.Catalog.Tables.Count, Is.EqualTo(78)); Assert.That(imported.Catalog.GetTable("production_stock_targets.csv").Rows.Count, Is.EqualTo(6));
            var report = services.Get<SaveService>().Validator.Validate(game.Snapshot(), "P09_NEW_GAME"); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
            Assert.That(game.CurrentDocument["payload"]!["economy"]!["store"]!["supplyState"]!.Value<string>("mode"), Is.EqualTo("PRODUCTION_OWNED"));
        }

        [Test]
        public void MigrationPreservesP08EconomyAndSwitchesSupplyOwnership()
        {
            JObject source = StrictJson.ParseObject(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P09", "p09-migration-before.golden.json")));
            source["payload"]!["economy"]!["store"]!["stackLines"] = new JArray(new JObject { ["stockLineId"] = "STACK:POTION:POT_HEAL_SMALL:SYSTEM_SUPPLY", ["productKind"] = "POTION", ["productId"] = "POT_HEAL_SMALL", ["quantity"] = 3, ["sourceType"] = "SYSTEM_SUPPLY", ["firstAcquiredOperationId"] = "018f0000-0000-7000-8000-000000000991", ["lastAcquiredOperationId"] = "018f0000-0000-7000-8000-000000000991" });
            P09ProductionCatalog catalog = new(services.Get<ContentCatalogService>().Catalog); JObject migrated = new P08ToP09ContentMigration().Apply(source, catalog.DefaultTargets());
            Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.7")); Assert.That(migrated["payload"]!["economy"]!["store"]!["supplyState"]!.Value<string>("mode"), Is.EqualTo("PRODUCTION_OWNED"));
            Assert.That(migrated["payload"]!["economy"]!["store"]!["stackLines"]!.Count(), Is.EqualTo(1)); Assert.That(migrated["payload"]!["production"]!["stockTargets"]!.Count(), Is.EqualTo(6));
        }

        [Test]
        public void ProductionMathUsesCeilingForEfficiencyAndCombinedSpeed()
        {
            Assert.That(ProductionMath.EfficientQuantity(7, 9600), Is.EqualTo(7)); Assert.That(ProductionMath.EfficientQuantity(20, 8200), Is.EqualTo(17));
            Assert.That(ProductionMath.DurationTicks(20, 10000, 10000), Is.EqualTo(20)); Assert.That(ProductionMath.DurationTicks(20, 11250, 11200), Is.EqualTo(16));
            Assert.That(ProductionMath.ClampTarget("POTION", 0, 2, 99), Is.EqualTo(2));
        }

        [Test]
        public void PotionEnqueueReservesMaterialsAndCompletionStocksStoreAndAwardsXp()
        {
            SeedActive("FAC_ALCHEMY", "NPC_ALCHEMIST", ("MAT_R01_SLIME_GEL", 2), ("MAT_R01_WILD_HERB", 4));
            EnqueueProductionCommand enqueue = Enqueue("REC_POT_HEAL_SMALL", 2, "018f0000-0000-7000-8000-000000000911"); production.Enqueue(enqueue);
            JObject queued = game.Snapshot(); Assert.That(queued["payload"]!["inventory"]!["itemStacks"]!.Count(), Is.EqualTo(0)); Assert.That(queued["payload"]!["production"]!["facilityQueues"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_ALCHEMY")["jobs"]!.Count(), Is.EqualTo(1));
            ProductionOperationResult advanced = production.Advance(Advance(30, "018f0000-0000-7000-8000-000000000912")); Assert.That(advanced.Completed, Is.EqualTo(1));
            JObject saved = game.Snapshot(); JObject stock = saved["payload"]!["economy"]!["store"]!["stackLines"]!.Children<JObject>().Single(value => value.Value<string>("sourceType") == "PRODUCTION");
            Assert.That(stock.Value<long>("quantity"), Is.EqualTo(2)); JObject npc = Npc(saved, "NPC_ALCHEMIST"); Assert.That(npc.Value<long>("proficiencyExp"), Is.EqualTo(24));
            Assert.That(services.Get<SaveService>().Validator.Validate(saved, "POTION_COMPLETED").IsValid, Is.True);
        }

        [Test]
        public void EquipmentOutputIsDeterministicAndReplayDoesNotDuplicateIt()
        {
            SeedActive("FAC_BLACKSMITH", "NPC_BLACKSMITH", ("MAT_R01_SOFTWOOD", 4), ("MAT_R01_WOLF_FANG", 2));
            production.Enqueue(Enqueue("REC_EQ_T1_WARRIOR_WEAPON", 1, "018f0000-0000-7000-8000-000000000921")); AdvanceProductionTicksCommand command = Advance(25, "018f0000-0000-7000-8000-000000000922");
            ProductionOperationResult first = production.Advance(command); string instanceId = game.Snapshot()["payload"]!["economy"]!["store"]!["equipment"]![0]!.Value<string>("instanceId"); long revision = game.Revision;
            ProductionOperationResult replay = production.Advance(command); Assert.That(first.Completed, Is.EqualTo(1)); Assert.That(replay.Replayed, Is.True); Assert.That(game.Revision, Is.EqualTo(revision));
            Assert.That(game.Snapshot()["payload"]!["economy"]!["store"]!["equipment"]!.Count(), Is.EqualTo(1)); Assert.That(instanceId[14], Is.EqualTo('7'));
        }

        [Test]
        public void AutomationRecordsMaterialStopThenQueuesWhenMaterialsArrive()
        {
            SeedActive("FAC_ALCHEMY", "NPC_ALCHEMIST"); production.RunAutomation(Automation("018f0000-0000-7000-8000-000000000931"));
            JObject stopped = game.Snapshot()["payload"]!["production"]!["stockTargets"]!.Children<JObject>().Single(value => value.Value<string>("targetId") == "TARGET_SMALL_HEAL"); Assert.That(stopped.Value<string>("lastStopReason"), Is.EqualTo("P09_MATERIAL_INSUFFICIENT"));
            SeedMaterials(("MAT_R01_SLIME_GEL", 1), ("MAT_R01_WILD_HERB", 2)); production.RunAutomation(Automation("018f0000-0000-7000-8000-000000000932"));
            JObject queue = game.Snapshot()["payload"]!["production"]!["facilityQueues"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_ALCHEMY"); Assert.That(queue["jobs"]!.Count(), Is.EqualTo(1));
        }

        [Test]
        public void TreatmentReturnsInjuredMercenaryToTownAndAwardsHealerXp()
        {
            SeedActive("FAC_INFIRMARY", "NPC_HEALER"); JObject seed = game.Snapshot(); JObject mercenary = seed["payload"]!["mercenaries"]!.Children<JObject>().First(); mercenary["autonomy"]!["state"] = "INJURED"; Commit(seed);
            string id = game.Snapshot()["payload"]!["mercenaries"]!.Children<JObject>().First().Value<string>("instanceId"); production.EnqueueTreatment(Treatment(id, "018f0000-0000-7000-8000-000000000941")); production.Advance(Advance(40, "018f0000-0000-7000-8000-000000000942"));
            JObject saved = game.Snapshot(); Assert.That(saved["payload"]!["mercenaries"]!.Children<JObject>().First()["autonomy"]!.Value<string>("state"), Is.EqualTo("IDLE_TOWN")); Assert.That(Npc(saved, "NPC_HEALER").Value<long>("proficiencyExp"), Is.EqualTo(30));
        }

        private void SeedActive(string facilityId, string profession, params (string Id, long Quantity)[] materials)
        {
            JObject seed = game.Snapshot(); JObject facility = seed["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == facilityId); JObject npc = Npc(seed, profession);
            facility["state"] = "ACTIVE"; facility["assignedNpcInstanceId"] = npc.Value<string>("instanceId"); npc["assignedFacilityId"] = facilityId; npc["working"] = true; AddMaterials(seed, materials); Commit(seed);
        }
        private void SeedMaterials(params (string Id, long Quantity)[] materials) { JObject seed = game.Snapshot(); AddMaterials(seed, materials); Commit(seed); }
        private static void AddMaterials(JObject seed, params (string Id, long Quantity)[] materials)
        {
            JArray stacks = (JArray)seed["payload"]!["inventory"]!["itemStacks"]!; foreach ((string id, long quantity) in materials) { JObject line = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == id); if (line == null) stacks.Add(new JObject { ["itemId"] = id, ["quantity"] = quantity }); else line["quantity"] = line.Value<long>("quantity") + quantity; }
        }
        private void Commit(JObject draft) { SaveWriteResult result = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow); Assert.That(result.Success, Is.True, result.ErrorCode); game.SynchronizeCommittedDocument(result.Document); }
        private EnqueueProductionCommand Enqueue(string recipe, int quantity, string id) { Guid operation = Guid.Parse(id); var draft = new EnqueueProductionCommand(operation, game.Revision, null, recipe, quantity); return new EnqueueProductionCommand(operation, game.Revision, new ProductionRequestHasher().Compute(draft), recipe, quantity); }
        private AdvanceProductionTicksCommand Advance(int ticks, string id) { Guid operation = Guid.Parse(id); var draft = new AdvanceProductionTicksCommand(operation, game.Revision, null, ticks); return new AdvanceProductionTicksCommand(operation, game.Revision, new ProductionRequestHasher().Compute(draft), ticks); }
        private RunProductionAutomationCommand Automation(string id) { Guid operation = Guid.Parse(id); var draft = new RunProductionAutomationCommand(operation, game.Revision, null); return new RunProductionAutomationCommand(operation, game.Revision, new ProductionRequestHasher().Compute(draft)); }
        private EnqueueTreatmentCommand Treatment(string mercenary, string id) { Guid operation = Guid.Parse(id); var draft = new EnqueueTreatmentCommand(operation, game.Revision, null, mercenary); return new EnqueueTreatmentCommand(operation, game.Revision, new ProductionRequestHasher().Compute(draft), mercenary); }
        private static JObject Npc(JObject document, string profession) => document["payload"]!["managementNpcs"]!.Children<JObject>().Single(value => value.Value<string>("professionId") == profession);
        private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));
    }
}
