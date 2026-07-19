using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Regions;
using KingdomTycoon.Domain.Regions;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Regions;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P12RegionTests
    {
        private string temporaryRoot;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private RegionGameService regions;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p12-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryRoot);
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)); string contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            services = new ServiceRegistry(); services.Register(new SaveService(temporaryRoot, Read(contracts, "save.schema.json"), Read(contracts, "save.content.5.schema.json"), Read(contracts, "save.content.6.schema.json"), Read(contracts, "save.content.7.schema.json"), Read(contracts, "save.content.8.schema.json"), Read(contracts, "save.content.9.schema.json"), Read(contracts, "save.content.10.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new DevelopmentActiveContentVersionProvider(CompileTimeActiveContentVersionProvider.P12ContentVersion)));
            game = new FacilityGameService(clock, Read(contracts, "p12-new-game.template.json"), Read(contracts, "p04-new-game.template.json")); services.Register(game);
            regions = new RegionGameService(clock); services.Register(regions); services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult(); game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult(); regions.Bootstrap();
        }

        [TearDown]
        public void TearDown() { services?.ShutdownAll(); if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }

        [Test]
        public void ContentAndNewGameContractsValidateAsEightyEightTablePackage()
        {
            ContentCatalog catalog = services.Get<ContentCatalogService>().Catalog;
            Assert.That(catalog.ContentVersion, Is.EqualTo("1.0.0-content.10")); Assert.That(catalog.Tables.Count, Is.EqualTo(88));
            Assert.That(catalog.GetTable("regions.csv").Rows.Count, Is.EqualTo(5)); Assert.That(catalog.GetTable("region_encounter_profiles.csv").Rows.Count, Is.EqualTo(25));
            Assert.That(catalog.GetTable("region_unlock_rules.csv").Rows.Count, Is.EqualTo(5)); Assert.That(regions.GetOverview().Regions.Count, Is.EqualTo(5)); AssertValid(game.Snapshot(), "P12_NEW_GAME");
        }

        [Test]
        public void MigrationPreservesProgressAndCreatesCanonicalPolicyAndEventContainers()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P12", "p12-migration-before.golden.json"); JObject source = StrictJson.ParseObject(File.ReadAllText(path));
            source["payload"]!["regions"]!["progress"]![0]!["huntCount"] = 9; JObject migrated = new P11ToP12ContentMigration().Apply(source);
            Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.10")); Assert.That(migrated["payload"]!["regions"]!.Value<int>("regionVersion"), Is.EqualTo(1));
            Assert.That(migrated["payload"]!["regions"]!["progress"]![0]!.Value<long>("huntCount"), Is.EqualTo(9)); Assert.That(migrated["payload"]!["regions"]!["events"]!.Count(), Is.Zero);
            Assert.That(migrated["payload"]!["kingdom"]!["regionAccessPolicies"]!.Children<JObject>().Select(value => value.Value<string>("regionId")), Is.EqualTo(Enumerable.Range(1, 5).Select(value => $"REGION_R0{value}")));
        }

        [Test]
        public void ProgressMathAppliesVictoryEliteBonusAndCap()
        {
            Assert.That(RegionProgressMath.Apply(70, 12, 20, 1, 100), Is.EqualTo(100));
            Assert.That(RegionProgressMath.Apply(0, 8, 18, 2, 100), Is.EqualTo(44));
            Assert.Throws<RegionDomainException>(() => RegionProgressMath.Apply(-1, 1, 0, 0, 100));
        }

        [Test]
        public void UnlockNormalizationUsesStageAndPreviousProgressAndWritesOnce()
        {
            JObject draft = game.Snapshot(); draft["payload"]!["kingdom"]!["kingdomStageId"] = "KINGDOM_2"; Progress(draft, "REGION_R01")["progressPercent"] = 100; Commit(draft);
            long before = game.Revision; Assert.That(regions.NormalizeUnlocks(), Is.True); JObject saved = game.Snapshot();
            Assert.That(game.Revision, Is.EqualTo(before + 1)); Assert.That(Progress(saved, "REGION_R02").Value<bool>("unlocked"), Is.True); Assert.That(Policy(saved, "REGION_R02").Value<bool>("allowed"), Is.True);
            Assert.That(saved["payload"]!["regions"]!["events"]!.Children<JObject>().Single().Value<string>("eventType"), Is.EqualTo("RegionUnlocked")); Assert.That(regions.NormalizeUnlocks(), Is.False); AssertValid(saved, "P12_UNLOCK");
        }

        [Test]
        public void PolicyCommandIsHashedRevisionGuardedAndReplaySafe()
        {
            SetRegionAccessPolicyCommand command = PolicyCommand("019fa180-0000-7000-8000-000000000011", "REGION_R01", false); RegionPolicyOperationResult first = regions.SetAccessPolicy(command); long revision = game.Revision;
            RegionPolicyOperationResult replay = regions.SetAccessPolicy(command); Assert.That(first.ResultCode, Is.EqualTo("P12_POLICY_UPDATED")); Assert.That(replay.Replayed, Is.True); Assert.That(game.Revision, Is.EqualTo(revision));
            Assert.That(Policy(game.Snapshot(), "REGION_R01").Value<bool>("allowed"), Is.False); Assert.Throws<RegionDomainException>(() => regions.SetAccessPolicy(new SetRegionAccessPolicyCommand(Guid.Parse("019fa180-0000-7000-8000-000000000012"), revision - 1, new string('0', 64), "REGION_R01", true)));
            AssertValid(game.Snapshot(), "P12_POLICY");
        }

        [Test]
        public void ClosingPolicyWhileMercenaryIsAssignedRollsBack()
        {
            JObject draft = game.Snapshot(); JObject actor = Actor(draft); actor["autonomy"]!["state"] = "TRAVEL_TO_REGION"; actor["autonomy"]!["currentRegionId"] = "REGION_R01"; Commit(draft); long revision = game.Revision;
            RegionDomainException error = Assert.Throws<RegionDomainException>(() => regions.SetAccessPolicy(PolicyCommand("019fa180-0000-7000-8000-000000000021", "REGION_R01", false)));
            Assert.That(error.Code, Is.EqualTo("P12_REGION_BUSY")); Assert.That(game.Revision, Is.EqualTo(revision)); Assert.That(Policy(game.Snapshot(), "REGION_R01").Value<bool>("allowed"), Is.True);
        }

        [Test]
        public void DeploymentRequiresUnlockPolicyRankAndTownSafeState()
        {
            JObject draft = game.Snapshot(); string actorId = Actor(draft).Value<string>("instanceId"); Progress(draft, "REGION_R02")["unlocked"] = true; Policy(draft, "REGION_R02")["allowed"] = true;
            RegionDomainException lowRank = Assert.Throws<RegionDomainException>(() => regions.ValidateDeployment(draft, "REGION_R02", new[] { actorId })); Assert.That(lowRank.Code, Is.EqualTo("P12_PARTY_RANK_TOO_LOW"));
            Actor(draft)["rankId"] = "RANK_REGULAR"; Assert.DoesNotThrow(() => regions.ValidateDeployment(draft, "REGION_R02", new[] { actorId }));
            Actor(draft)["autonomy"]!["state"] = "COMBAT"; RegionDomainException unsafeState = Assert.Throws<RegionDomainException>(() => regions.ValidateDeployment(draft, "REGION_R02", new[] { actorId })); Assert.That(unsafeState.Code, Is.EqualTo("P12_PARTY_NOT_TOWN_SAFE"));
        }

        [Test]
        public void HuntSettlementUpdatesProgressEventsHighestRankAndUnlocksNextRegionAtomically()
        {
            JObject draft = game.Snapshot(); draft["payload"]!["kingdom"]!["kingdomStageId"] = "KINGDOM_2"; JObject actor = Actor(draft); string actorId = actor.Value<string>("instanceId"); actor["rankId"] = "RANK_REGULAR";
            for (int index = 0; index < 5; index++) regions.ApplyHuntSettlement(draft, "REGION_R01", new[] { actorId }, 0, true, clock.UtcNow.AddMinutes(index));
            Commit(draft); JObject saved = game.Snapshot(); JObject r01 = Progress(saved, "REGION_R01");
            Assert.That(r01.Value<int>("progressPercent"), Is.EqualTo(100)); Assert.That(r01.Value<long>("huntCount"), Is.EqualTo(5)); Assert.That(r01.Value<string>("highestRankReachedId"), Is.EqualTo("RANK_REGULAR"));
            Assert.That(Progress(saved, "REGION_R02").Value<bool>("unlocked"), Is.True); Assert.That(saved["payload"]!["regions"]!["events"]!.Children<JObject>().Count(value => value.Value<string>("eventType") == "HuntSettled"), Is.EqualTo(5)); AssertValid(saved, "P12_SETTLEMENT");
        }

        private SetRegionAccessPolicyCommand PolicyCommand(string id, string regionId, bool allowed)
        {
            Guid operation = Guid.Parse(id); var draft = new SetRegionAccessPolicyCommand(operation, game.Revision, null, regionId, allowed);
            return new SetRegionAccessPolicyCommand(operation, game.Revision, new RegionRequestHasher().Compute(draft), regionId, allowed);
        }
        private void Commit(JObject draft)
        {
            SaveService save = services.Get<SaveService>(); JObject prepared = save.Validator.PrepareForCommit(draft, game.Revision, clock.UtcNow); ValidationReport report = save.Validator.Validate(prepared, "P12_TEST_SEED"); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
            SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow); Assert.That(result.Success, Is.True, result.ErrorCode); game.SynchronizeCommittedDocument(result.Document);
        }
        private void AssertValid(JObject document, string source) { ValidationReport report = services.Get<SaveService>().Validator.Validate(document, source); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues)); }
        private static JObject Actor(JObject document) => document["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("jobId") == "JOB_WARRIOR");
        private static JObject Progress(JObject document, string id) => document["payload"]!["regions"]!["progress"]!.Children<JObject>().Single(value => value.Value<string>("regionId") == id);
        private static JObject Policy(JObject document, string id) => document["payload"]!["kingdom"]!["regionAccessPolicies"]!.Children<JObject>().Single(value => value.Value<string>("regionId") == id);
        private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));
    }
}
