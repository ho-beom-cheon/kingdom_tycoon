using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.EquipmentGrowth;
using KingdomTycoon.Domain.EquipmentGrowth;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.EquipmentGrowth;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P10EquipmentGrowthTests
    {
        private string temporaryRoot;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private EquipmentGrowthGameService growth;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p10-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryRoot);
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 26, 0, 0, 0, TimeSpan.Zero)); string contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            services = new ServiceRegistry(); services.Register(new SaveService(temporaryRoot, Read(contracts, "save.schema.json"), Read(contracts, "save.content.5.schema.json"), Read(contracts, "save.content.6.schema.json"), Read(contracts, "save.content.7.schema.json"), Read(contracts, "save.content.8.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, Read(contracts, "p10-new-game.template.json"), Read(contracts, "p04-new-game.template.json")); services.Register(game);
            growth = new EquipmentGrowthGameService(clock); services.Register(growth); services.InitializeAll(); services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult(); game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult(); growth.Bootstrap();
        }

        [TearDown]
        public void TearDown() { services?.ShutdownAll(); if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }

        [Test]
        public void ContentAndNewGameContractsValidateAsEightyTablePackage()
        {
            ContentCatalog catalog = services.Get<ContentCatalogService>().Catalog; Assert.That(catalog.ContentVersion, Is.EqualTo("1.0.0-content.8")); Assert.That(catalog.Tables.Count, Is.EqualTo(80));
            Assert.That(catalog.GetTable("equipment_growth_facility_rules.csv").Rows.Count, Is.EqualTo(4)); Assert.That(catalog.GetTable("dismantle_rules.csv").Rows.Count, Is.EqualTo(5));
            ValidationReport report = services.Get<SaveService>().Validator.Validate(game.Snapshot(), "P10_NEW_GAME"); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
        }

        [Test]
        public void MigrationPreservesInventoryAndInitializesEveryGrowthField()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P10", "p10-migration-before.golden.json"); JObject source = StrictJson.ParseObject(File.ReadAllText(path));
            source["payload"]!["inventory"]!["equipment"] = new JArray(LegacyEquipment()); JObject migrated = new P09ToP10ContentMigration().Apply(source); JObject item = (JObject)migrated["payload"]!["inventory"]!["equipment"]![0]!;
            Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.8")); Assert.That(item.Value<int>("enhancementPityBps"), Is.Zero); Assert.That(item["enhancementMaterialInvested"]!.Count(), Is.Zero);
            Assert.That(item["pendingRefineOption"]!.Type, Is.EqualTo(JTokenType.Null)); Assert.That(migrated["payload"]!["equipmentGrowth"]!["events"]!.Count(), Is.Zero);
        }

        [Test]
        public void GrowthMathUsesBasisPointsAndFloorsRefunds()
        {
            Assert.That(EquipmentGrowthMath.EffectiveChanceBps(3800, 6500), Is.EqualTo(10000)); Assert.That(EquipmentGrowthMath.IsSuccess(9999, 10000), Is.True);
            Assert.That(EquipmentGrowthMath.InclusiveBps(900, 300, 1200), Is.EqualTo(1200)); Assert.That(EquipmentGrowthMath.DismantleBase(2, 15000), Is.EqualTo(3)); Assert.That(EquipmentGrowthMath.Refund(9, 7000), Is.EqualTo(6));
        }

        [Test]
        public void GuaranteedEnhancementDebitsOnceAndReplayIsIdempotent()
        {
            SeedWorkshopAndEquipment(0, null, ("MAT_ENHANCE_1", 5)); EnhanceEquipmentCommand command = Enhance("019f9000-0000-7000-8000-000000000011"); EquipmentGrowthOperationResult first = growth.Enhance(command); long revision = game.Revision; EquipmentGrowthOperationResult replay = growth.Enhance(command);
            JObject saved = game.Snapshot(); JObject item = Equipment(saved); JObject actor = Actor(saved); Assert.That(first.ResultCode, Is.EqualTo("P10_ENHANCE_SUCCESS")); Assert.That(replay.Replayed, Is.True); Assert.That(game.Revision, Is.EqualTo(revision));
            Assert.That(item.Value<int>("enhancementLevel"), Is.EqualTo(1)); Assert.That(actor.Value<long>("personalGold"), Is.EqualTo(49_900)); Assert.That(Stack(saved, "MAT_ENHANCE_1"), Is.EqualTo(4));
            Assert.That(saved["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!.Count(), Is.EqualTo(1)); AssertValid(saved, "P10_ENHANCE");
        }

        [Test]
        public void PityGuaranteesHighEnhancementWithoutDestroyOrDownrank()
        {
            SeedWorkshopAndEquipment(9, null, ("MAT_ENHANCE_3", 4)); JObject seed = game.Snapshot(); Equipment(seed)["enhancementPityBps"] = 10_000; Commit(seed);
            EquipmentGrowthOperationResult result = growth.Enhance(Enhance("019f9000-0000-7000-8000-000000000021")); JObject item = Equipment(game.Snapshot()); Assert.That(result.ResultCode, Is.EqualTo("P10_ENHANCE_SUCCESS")); Assert.That(item.Value<int>("enhancementLevel"), Is.EqualTo(10)); Assert.That(item.Value<int>("enhancementPityBps"), Is.Zero);
        }

        [Test]
        public void RefineCandidateRequiresExplicitResolutionAndAcceptsExactRolledValue()
        {
            SeedWorkshopAndEquipment(3, null, ("MAT_R01_WOLF_FANG", 1)); RollRefineOptionCommand roll = Roll("019f9000-0000-7000-8000-000000000031", "REF_ATK_POWER"); growth.RollRefine(roll); JObject pending = (JObject)Equipment(game.Snapshot())["pendingRefineOption"]!;
            Assert.That(pending.Value<string>("optionId"), Is.EqualTo("REF_ATK_POWER")); Assert.That(pending.Value<int>("value"), Is.InRange(300, 1200)); Assert.That(Equipment(game.Snapshot())["refineOption"]!.Type, Is.EqualTo(JTokenType.Null));
            int rolled = pending.Value<int>("value"); growth.ResolveRefine(Resolve("019f9000-0000-7000-8000-000000000032", true)); JObject item = Equipment(game.Snapshot()); Assert.That(item["pendingRefineOption"]!.Type, Is.EqualTo(JTokenType.Null)); Assert.That(item["refineOption"]!.Value<int>("value"), Is.EqualTo(rolled)); AssertValid(game.Snapshot(), "P10_REFINE");
        }

        [Test]
        public void DismantleReturnsBaseAndSeventyPercentOfInvestedStones()
        {
            SeedWorkshopAndEquipment(5, new JArray(new JObject { ["itemId"] = "MAT_ENHANCE_1", ["quantity"] = 10 })); DismantleEquipmentCommand command = Dismantle("019f9000-0000-7000-8000-000000000041", true); growth.Dismantle(command);
            JObject saved = game.Snapshot(); Assert.That(saved["payload"]!["inventory"]!["equipment"]!.Count(), Is.Zero); Assert.That(Stack(saved, "MAT_ENHANCE_1"), Is.EqualTo(8)); Assert.That(saved["payload"]!["equipmentGrowth"]!["events"]!.Last!.Value<string>("eventType"), Is.EqualTo("EquipmentDismantled")); AssertValid(saved, "P10_DISMANTLE");
        }

        [Test]
        public void InsufficientMaterialRollsBackWithoutRevisionOrGoldChange()
        {
            SeedWorkshopAndEquipment(0, null); long revision = game.Revision; long gold = Actor(game.Snapshot()).Value<long>("personalGold"); EquipmentGrowthDomainException error = Assert.Throws<EquipmentGrowthDomainException>(() => growth.Enhance(Enhance("019f9000-0000-7000-8000-000000000051")));
            Assert.That(error.ErrorCode, Is.EqualTo("P10_MATERIAL_INSUFFICIENT")); Assert.That(game.Revision, Is.EqualTo(revision)); Assert.That(Actor(game.Snapshot()).Value<long>("personalGold"), Is.EqualTo(gold)); Assert.That(Equipment(game.Snapshot()).Value<int>("enhancementLevel"), Is.Zero);
        }

        private void SeedWorkshopAndEquipment(int level, JArray investments = null, params (string Id, long Quantity)[] materials)
        {
            JObject seed = game.Snapshot(); JObject facility = seed["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_BLACKSMITH"); JObject npc = seed["payload"]!["managementNpcs"]!.Children<JObject>().Single(value => value.Value<string>("professionId") == "NPC_BLACKSMITH");
            facility["state"] = "ACTIVE"; facility["level"] = 4; facility["assignedNpcInstanceId"] = npc.Value<string>("instanceId"); npc["assignedFacilityId"] = "FAC_BLACKSMITH"; npc["working"] = true;
            seed["payload"]!["kingdom"]!["facilityUpgradeCount"] = 3;
            Actor(seed)["personalGold"] = 50_000; ((JArray)seed["payload"]!["inventory"]!["equipment"]!).Add(EquipmentJson(level, investments)); JArray stacks = (JArray)seed["payload"]!["inventory"]!["itemStacks"]!;
            foreach ((string id, long quantity) in materials) stacks.Add(new JObject { ["itemId"] = id, ["quantity"] = quantity }); Commit(seed);
        }
        private static JObject EquipmentJson(int level, JArray investments) => new()
        {
            ["instanceId"] = "019f9000-0000-7000-8000-000000000101", ["equipmentTemplateId"] = "EQ_T1_WARRIOR_WEAPON", ["tier"] = 1, ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = level,
            ["refineOption"] = null, ["locked"] = false, ["equippedByMercenaryInstanceId"] = null, ["sourceContentVersion"] = "1.0.0-content.8", ["generationOperationId"] = "019f9000-0000-7000-8000-000000000102",
            ["enhancementPityBps"] = 0, ["enhancementAttemptCount"] = 0, ["enhancementMaterialInvested"] = investments ?? new JArray(), ["pendingRefineOption"] = null, ["refineRollCount"] = 0
        };
        private static JObject LegacyEquipment() => new()
        {
            ["instanceId"] = "019f9000-0000-7000-8000-000000000101", ["equipmentTemplateId"] = "EQ_T1_WARRIOR_WEAPON", ["tier"] = 1, ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = 0,
            ["refineOption"] = null, ["locked"] = false, ["equippedByMercenaryInstanceId"] = null, ["sourceContentVersion"] = "1.0.0-content.7", ["generationOperationId"] = "019f9000-0000-7000-8000-000000000102"
        };
        private EnhanceEquipmentCommand Enhance(string id) { Guid operation = Guid.Parse(id); var draft = new EnhanceEquipmentCommand(operation, game.Revision, null, ActorId, EquipmentId); return new EnhanceEquipmentCommand(operation, game.Revision, new EquipmentGrowthRequestHasher().Compute(draft), ActorId, EquipmentId); }
        private RollRefineOptionCommand Roll(string id, string option) { Guid operation = Guid.Parse(id); var draft = new RollRefineOptionCommand(operation, game.Revision, null, ActorId, EquipmentId, option); return new RollRefineOptionCommand(operation, game.Revision, new EquipmentGrowthRequestHasher().Compute(draft), ActorId, EquipmentId, option); }
        private ResolveRefineOptionCommand Resolve(string id, bool accept) { Guid operation = Guid.Parse(id); var draft = new ResolveRefineOptionCommand(operation, game.Revision, null, ActorId, EquipmentId, accept); return new ResolveRefineOptionCommand(operation, game.Revision, new EquipmentGrowthRequestHasher().Compute(draft), ActorId, EquipmentId, accept); }
        private DismantleEquipmentCommand Dismantle(string id, bool confirm) { Guid operation = Guid.Parse(id); var draft = new DismantleEquipmentCommand(operation, game.Revision, null, ActorId, new[] { EquipmentId }, confirm); return new DismantleEquipmentCommand(operation, game.Revision, new EquipmentGrowthRequestHasher().Compute(draft), ActorId, new[] { EquipmentId }, confirm); }
        private string ActorId => Actor(game.Snapshot()).Value<string>("instanceId");
        private const string EquipmentId = "019f9000-0000-7000-8000-000000000101";
        private static JObject Actor(JObject document) => document["payload"]!["mercenaries"]!.Children<JObject>().First();
        private static JObject Equipment(JObject document) => document["payload"]!["inventory"]!["equipment"]!.Children<JObject>().Single();
        private static long Stack(JObject document, string id) => document["payload"]!["inventory"]!["itemStacks"]!.Children<JObject>().Single(value => value.Value<string>("itemId") == id).Value<long>("quantity");
        private void Commit(JObject draft)
        {
            SaveService save = services.Get<SaveService>(); JObject prepared = save.Validator.PrepareForCommit(draft, game.Revision, clock.UtcNow); ValidationReport report = save.Validator.Validate(prepared, "P10_TEST_SEED");
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues)); SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow); Assert.That(result.Success, Is.True, result.ErrorCode); game.SynchronizeCommittedDocument(result.Document);
        }
        private void AssertValid(JObject document, string source) { ValidationReport report = services.Get<SaveService>().Validator.Validate(document, source); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues)); }
        private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));
    }
}
