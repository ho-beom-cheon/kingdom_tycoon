using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Progression;
using KingdomTycoon.Domain.Progression;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Progression;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P11ProgressionTests
    {
        private string temporaryRoot;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private ProgressionGameService progression;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p11-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryRoot);
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero)); string contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            services = new ServiceRegistry(); services.Register(new SaveService(temporaryRoot, Read(contracts, "save.schema.json"), Read(contracts, "save.content.5.schema.json"), Read(contracts, "save.content.6.schema.json"), Read(contracts, "save.content.7.schema.json"), Read(contracts, "save.content.8.schema.json"), Read(contracts, "save.content.9.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, Read(contracts, "p11-new-game.template.json"), Read(contracts, "p04-new-game.template.json")); services.Register(game);
            progression = new ProgressionGameService(clock); services.Register(progression); services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult(); game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult(); progression.Bootstrap();
        }

        [TearDown]
        public void TearDown() { services?.ShutdownAll(); if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }

        [Test]
        public void ContentAndNewGameContractsValidateAsEightyFourTablePackage()
        {
            ContentCatalog catalog = services.Get<ContentCatalogService>().Catalog; Assert.That(catalog.ContentVersion, Is.EqualTo("1.0.0-content.9")); Assert.That(catalog.Tables.Count, Is.EqualTo(84));
            Assert.That(catalog.GetTable("mercenary_level_curves.csv").Rows.Count, Is.EqualTo(270)); Assert.That(catalog.GetTable("promotion_review_rules.csv").Rows.Count, Is.EqualTo(5));
            Assert.That(catalog.GetTable("promotion_record_requirements.csv").Rows.Count, Is.EqualTo(5)); Assert.That(catalog.GetTable("promotion_supply_rules.csv").Rows.Count, Is.EqualTo(5)); AssertValid(game.Snapshot(), "P11_NEW_GAME");
        }

        [Test]
        public void MigrationPreservesGrowthAndAddsRecordsAndProgression()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P11", "p11-migration-before.golden.json"); JObject source = StrictJson.ParseObject(File.ReadAllText(path));
            source["payload"]!["equipmentGrowth"]!["nextEventSequence"] = 7; JObject migrated = new P10ToP11ContentMigration().Apply(source); JObject record = (JObject)migrated["payload"]!["mercenaries"]![0]!["records"]!;
            Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.9")); Assert.That(record.Value<long>("region2BattleCount"), Is.Zero); Assert.That(record.Value<long>("eliteKillCount"), Is.Zero); Assert.That(record.Value<long>("bossContributionCount"), Is.Zero);
            Assert.That(migrated["payload"]!["equipmentGrowth"]!.Value<long>("nextEventSequence"), Is.EqualTo(7)); Assert.That(migrated["payload"]!["progression"]!["events"]!.Count(), Is.Zero);
        }

        [Test]
        public void ExperienceAllocationIsSortedAndConservesRemainder()
        {
            var values = ProgressionMath.AllocateExperience(10, new[] { "C", "A", "B" });
            Assert.That(values["A"], Is.EqualTo(4)); Assert.That(values["B"], Is.EqualTo(3)); Assert.That(values["C"], Is.EqualTo(3)); Assert.That(values.Values.Sum(), Is.EqualTo(10));
        }

        [Test]
        public void ExperienceSupportsMultipleLevelsAndDiscardsAtRankCap()
        {
            LevelProgress progress = ProgressionMath.ApplyExperience("R", 1, 0, 100, _ => 3, (_, _) => 25);
            Assert.That(progress.LevelBefore, Is.EqualTo(1)); Assert.That(progress.LevelAfter, Is.EqualTo(3)); Assert.That(progress.ExperienceAfter, Is.Zero);
            Assert.That(ProgressionMath.ScaleCost(1, 11_000), Is.EqualTo(2)); Assert.That(ProgressionMath.ScaleCost(500, 11_000), Is.EqualTo(550));
        }

        [Test]
        public void StartReviewDebitsSnapshotOnceAndReplayIsIdempotent()
        {
            SeedReady(); StartPromotionReviewCommand command = Start("019f9100-0000-7000-8000-000000000011"); PromotionOperationResult first = progression.StartReview(command); long revision = game.Revision; PromotionOperationResult replay = progression.StartReview(command);
            JObject saved = game.Snapshot(); JObject actor = Actor(saved); JObject promotion = (JObject)actor["promotion"]!;
            Assert.That(first.ResultCode, Is.EqualTo("P11_PROMOTION_REVIEW_STARTED")); Assert.That(replay.Replayed, Is.True); Assert.That(game.Revision, Is.EqualTo(revision)); Assert.That(promotion.Value<string>("status"), Is.EqualTo("IN_REVIEW"));
            Assert.That(actor.Value<long>("personalGold"), Is.EqualTo(9_450)); Assert.That(saved["payload"]!["kingdom"]!.Value<long>("kingdomGold"), Is.EqualTo(9_780)); Assert.That(Stack(saved, "MAT_PROMO_BRONZE_EMBLEM"), Is.Zero);
            Assert.That(saved["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!.Count(), Is.EqualTo(1)); AssertValid(saved, "P11_START");
        }

        [Test]
        public void CompletedReviewAppliesNextRankAndPreservesGradeContributionAndEquipment()
        {
            SeedReady(true); progression.StartReview(Start("019f9100-0000-7000-8000-000000000021")); clock.Advance(TimeSpan.FromSeconds(60)); Assert.That(progression.NormalizeExpiredReviews(), Is.True);
            JObject before = game.Snapshot(); JObject actorBefore = Actor(before); string equipmentId = actorBefore["equipmentSlots"]!.Value<string>("WEAPON"); string grade = actorBefore.Value<string>("gradeId"); long contribution = actorBefore.Value<long>("contribution");
            PromotionOperationResult result = progression.ApplyPromotion(Apply("019f9100-0000-7000-8000-000000000022")); JObject actor = Actor(game.Snapshot());
            Assert.That(result.ResultCode, Is.EqualTo("P11_PROMOTION_APPLIED")); Assert.That(actor.Value<string>("rankId"), Is.EqualTo("RANK_REGULAR")); Assert.That(actor.Value<int>("level"), Is.EqualTo(1)); Assert.That(actor.Value<long>("exp"), Is.Zero);
            Assert.That(actor.Value<string>("gradeId"), Is.EqualTo(grade)); Assert.That(actor.Value<long>("contribution"), Is.EqualTo(contribution)); Assert.That(actor["equipmentSlots"]!.Value<string>("WEAPON"), Is.EqualTo(equipmentId)); Assert.That(actor["promotion"]!.Value<string>("status"), Is.EqualTo("NONE")); AssertValid(game.Snapshot(), "P11_APPLY");
        }

        [Test]
        public void MissingRecordGateRollsBackWithoutRevisionOrCurrencyChange()
        {
            SeedReady(); JObject seed = game.Snapshot(); Actor(seed)["records"]!["huntCount"] = 2; Actor(seed)["promotion"]!["status"] = "NONE"; Actor(seed)["promotion"]!["targetRankId"] = null; Commit(seed);
            long revision = game.Revision; long gold = Actor(game.Snapshot()).Value<long>("personalGold"); ProgressionDomainException error = Assert.Throws<ProgressionDomainException>(() => progression.StartReview(Start("019f9100-0000-7000-8000-000000000031")));
            Assert.That(error.ErrorCode, Is.EqualTo("P11_PROMOTION_NOT_READY")); Assert.That(game.Revision, Is.EqualTo(revision)); Assert.That(Actor(game.Snapshot()).Value<long>("personalGold"), Is.EqualTo(gold));
        }

        [Test]
        public void CombatSettlementLevelsAndIssuesSupportOnlyOnce()
        {
            JObject draft = game.Snapshot(); JObject actor = Actor(draft); actor["records"]!["huntCount"] = 3; string id = actor.Value<string>("instanceId");
            progression.ApplyCombatSettlement(draft, new[] { id }, 1_000, "REGION_R01", 1, 0, 0, clock.UtcNow); progression.ApplyCombatSettlement(draft, new[] { id }, 0, "REGION_R01", 0, 0, 0, clock.UtcNow); Commit(draft);
            JObject saved = game.Snapshot(); Assert.That(Actor(saved).Value<int>("level"), Is.GreaterThan(1)); Assert.That(Stack(saved, "MAT_PROMO_BRONZE_EMBLEM"), Is.EqualTo(1));
            Assert.That(saved["payload"]!["progression"]!["issuedSupplyKeys"]!.Count(), Is.EqualTo(1)); Assert.That(saved["payload"]!["progression"]!["events"]!.Children<JObject>().Count(value => value.Value<string>("eventType") == "PromotionSupportIssued"), Is.EqualTo(1)); AssertValid(saved, "P11_SETTLEMENT");
        }

        private void SeedReady(bool equipment = false)
        {
            JObject seed = game.Snapshot(); JObject actor = Actor(seed); actor["rankId"] = "RANK_APPRENTICE"; actor["level"] = 20; actor["exp"] = 0; actor["contribution"] = 100; actor["personalGold"] = 10_000; actor["records"]!["huntCount"] = 3;
            actor["promotion"] = new JObject { ["status"] = "NONE", ["targetRankId"] = null, ["operationId"] = null, ["startedAtUtc"] = null, ["finishesAtUtc"] = null, ["costSnapshot"] = null };
            seed["payload"]!["kingdom"]!["kingdomGold"] = 10_000; ((JArray)seed["payload"]!["inventory"]!["itemStacks"]!).Add(new JObject { ["itemId"] = "MAT_PROMO_BRONZE_EMBLEM", ["quantity"] = 1 });
            if (equipment)
            {
                const string equipmentId = "019f9100-0000-7000-8000-000000000101"; ((JArray)seed["payload"]!["inventory"]!["equipment"]!).Add(Equipment(equipmentId, actor.Value<string>("instanceId")));
                actor["equipmentSlots"]!["WEAPON"] = equipmentId;
            }
            Commit(seed);
        }

        private static JObject Equipment(string id, string owner) => new()
        {
            ["instanceId"] = id, ["equipmentTemplateId"] = "EQ_T1_WARRIOR_WEAPON", ["tier"] = 1, ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = 0,
            ["refineOption"] = null, ["locked"] = false, ["equippedByMercenaryInstanceId"] = owner, ["sourceContentVersion"] = "1.0.0-content.9", ["generationOperationId"] = "019f9100-0000-7000-8000-000000000102",
            ["enhancementPityBps"] = 0, ["enhancementAttemptCount"] = 0, ["enhancementMaterialInvested"] = new JArray(), ["pendingRefineOption"] = null, ["refineRollCount"] = 0
        };
        private StartPromotionReviewCommand Start(string id) { Guid operation = Guid.Parse(id); var draft = new StartPromotionReviewCommand(operation, game.Revision, null, Actor(game.Snapshot()).Value<string>("instanceId")); return new StartPromotionReviewCommand(operation, game.Revision, new ProgressionRequestHasher().Compute(draft), draft.MercenaryInstanceId); }
        private ApplyPromotionCommand Apply(string id) { Guid operation = Guid.Parse(id); var draft = new ApplyPromotionCommand(operation, game.Revision, null, Actor(game.Snapshot()).Value<string>("instanceId")); return new ApplyPromotionCommand(operation, game.Revision, new ProgressionRequestHasher().Compute(draft), draft.MercenaryInstanceId); }
        private static JObject Actor(JObject document) => document["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("jobId") == "JOB_WARRIOR");
        private static long Stack(JObject document, string id) => document["payload"]!["inventory"]!["itemStacks"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == id)?.Value<long>("quantity") ?? 0;
        private void Commit(JObject draft)
        {
            SaveService save = services.Get<SaveService>(); JObject prepared = save.Validator.PrepareForCommit(draft, game.Revision, clock.UtcNow); ValidationReport report = save.Validator.Validate(prepared, "P11_TEST_SEED"); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
            SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow); Assert.That(result.Success, Is.True, result.ErrorCode); game.SynchronizeCommittedDocument(result.Document);
        }
        private void AssertValid(JObject document, string source) { ValidationReport report = services.Get<SaveService>().Validator.Validate(document, source); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues)); }
        private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));
    }
}
