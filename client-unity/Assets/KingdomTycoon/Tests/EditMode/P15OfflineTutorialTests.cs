using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.OfflineTutorial;
using KingdomTycoon.Domain.OfflineTutorial;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.OfflineTutorial;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P15OfflineTutorialTests
    {
        private string temporaryRoot;
        private string contracts;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private OfflineTutorialGameService offline;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p15-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryRoot);
            contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)); services = new ServiceRegistry();
            services.Register(new SaveService(temporaryRoot, Read("save.schema.json"), Read("save.content.5.schema.json"), Read("save.content.6.schema.json"), Read("save.content.7.schema.json"), Read("save.content.8.schema.json"), Read("save.content.9.schema.json"), Read("save.content.10.schema.json"), Read("save.content.11.schema.json"), Read("save.content.12.schema.json"), Read("save.content.13.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, Read("p15-new-game.template.json"), Read("p04-new-game.template.json")); services.Register(game);
            offline = new OfflineTutorialGameService(clock); services.Register(offline); services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult(); game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult(); offline.Bootstrap();
        }

        [TearDown] public void TearDown() { services?.ShutdownAll(); if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }

        [Test]
        public void ContentAndSaveContractsLoadNinetyFiveCanonicalTables()
        {
            ContentCatalog catalog = services.Get<ContentCatalogService>().Catalog;
            Assert.That(catalog.ContentVersion, Is.EqualTo("1.0.0-content.13")); Assert.That(catalog.Tables.Count, Is.EqualTo(95));
            Assert.That(catalog.GetTable("offline_reward_rules.csv").Rows.Count, Is.EqualTo(6)); Assert.That(catalog.GetTable("tutorial_steps.csv").Rows.Count, Is.EqualTo(10));
            Assert.That(offline.GetOverview().TotalCount, Is.EqualTo(10)); AssertValid(game.Snapshot(), "P15_NEW_GAME");
        }

        [Test]
        public void MigrationPreservesSourceAndAddsOfflineTutorialContracts()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P15", "p15-migration-before.golden.json");
            JObject source = StrictJson.ParseObject(File.ReadAllText(path)); JObject untouched = (JObject)source.DeepClone(); JObject migrated = new P14ToP15ContentMigration().Apply(source);
            Assert.That(JToken.DeepEquals(source, untouched), Is.True); Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.13"));
            Assert.That(migrated["payload"]!["tutorial"]!["currentStepId"]!.Value<string>(), Is.EqualTo("TUT_01_KINGDOM_OVERVIEW")); Assert.That(migrated["payload"]!["offline"]!["history"]!.Count(), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => new P14ToP15ContentMigration().Apply(migrated));
        }

        [Test]
        public void OfflineWindowClampsAtEightHoursAndRejectsClockRollback()
        {
            DateTimeOffset now = clock.UtcNow;
            OfflineWindow capped = OfflineWindowPolicy.Evaluate(now.AddHours(-12), now, 60, 28800, 2);
            Assert.That(capped.Status, Is.EqualTo("CAPPED")); Assert.That(capped.EligibleSeconds, Is.EqualTo(28800));
            OfflineWindow rollback = OfflineWindowPolicy.Evaluate(now.AddSeconds(3), now, 60, 28800, 2);
            Assert.That(rollback.Status, Is.EqualTo("CLOCK_ROLLBACK")); Assert.That(rollback.EligibleSeconds, Is.Zero);
        }

        [Test]
        public void SettlementPersistsSixOrderedLinesAndDoesNotReplay()
        {
            JObject draft = game.Snapshot(); JObject state = (JObject)draft["payload"]!["offline"]!; state["accrualCursorUtc"] = "2026-07-21T22:00:00.000Z"; state["lastTrustedUtc"] = "2026-07-21T22:00:00.000Z";
            JObject member = draft["payload"]!["mercenaries"]!.Children<JObject>().First(); member["active"] = true; member["autonomy"]!["state"] = "TRAVEL_TO_REGION"; member["autonomy"]!["currentRegionId"] = "REGION_R01";
            Commit(draft); offline.SettleOffline(); long revision = game.Revision; JObject saved = game.Snapshot(); offline.SettleOffline();
            JObject history = saved["payload"]!["offline"]!["history"]!.Children<JObject>().Single();
            Assert.That(history.Value<string>("status"), Is.EqualTo("APPLIED")); Assert.That(history["lines"]!.Children<JObject>().Select(value => value.Value<string>("type")), Is.EqualTo(new[] { "HUNT", "POTION_CONSUMPTION", "FACILITY", "NPC_PROFICIENCY", "INJURY_RECOVERY", "PROMOTION_REVIEW" }));
            Assert.That(game.Revision, Is.EqualTo(revision)); AssertValid(saved, "P15_OFFLINE_SETTLEMENT");
        }

        [Test]
        public void TutorialGrantAndReplayAreExactlyOnceThenSkipAllCompletes()
        {
            TutorialCommand command = offline.CreateCurrentActionCommand(); long gold = game.Snapshot()["payload"]!["kingdom"]!.Value<long>("kingdomGold");
            TutorialOperationResult first = offline.Execute(command); long revision = game.Revision; TutorialOperationResult replay = offline.Execute(command);
            Assert.That(first.Replayed, Is.False); Assert.That(replay.Replayed, Is.True); Assert.That(game.Revision, Is.EqualTo(revision));
            Assert.That(game.Snapshot()["payload"]!["kingdom"]!.Value<long>("kingdomGold"), Is.GreaterThanOrEqualTo(gold));
            TutorialOperationResult skipped = offline.Execute(offline.CreateSkipAllCommand()); OfflineTutorialOverviewDto overview = offline.GetOverview();
            Assert.That(skipped.ResultCode, Is.EqualTo("P15_TUTORIAL_COMPLETED")); Assert.That(overview.TutorialCompleted, Is.True); Assert.That(overview.CompletedCount, Is.EqualTo(10)); AssertValid(game.Snapshot(), "P15_TUTORIAL_COMPLETE");
        }

        private void Commit(JObject draft) { SaveWriteResult result = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow); Assert.That(result.Success, Is.True, result.ErrorCode + "\n" + string.Join("\n", result.Report.Issues)); game.SynchronizeCommittedDocument(result.Document); }
        private void AssertValid(JObject document, string source) { ValidationReport report = services.Get<SaveService>().Validator.Validate(document, source); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues)); }
        private string Read(string name) => File.ReadAllText(Path.Combine(contracts, name));
    }
}
