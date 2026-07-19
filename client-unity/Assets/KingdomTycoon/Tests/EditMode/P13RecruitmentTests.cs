using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Recruitment;
using KingdomTycoon.Domain.Recruitment;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Mercenaries;
using KingdomTycoon.Infrastructure.Recruitment;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P13RecruitmentTests
    {
        private string temporaryRoot;
        private string contracts;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;
        private RecruitmentGameService recruitment;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p13-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero));
            services = new ServiceRegistry();
            services.Register(new SaveService(temporaryRoot, Read("save.schema.json"), Read("save.content.5.schema.json"), Read("save.content.6.schema.json"), Read("save.content.7.schema.json"), Read("save.content.8.schema.json"), Read("save.content.9.schema.json"), Read("save.content.10.schema.json"), Read("save.content.11.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, Read("p13-new-game.template.json"), Read("p04-new-game.template.json"));
            services.Register(game);
            services.Register(new MercenaryRosterService(clock, Read("p05-migration-after.golden.json")));
            recruitment = new RecruitmentGameService(clock);
            services.Register(recruitment);
            services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
            game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult();
            services.Get<MercenaryRosterService>().Bootstrap();
            recruitment.Bootstrap();
        }

        [TearDown]
        public void TearDown()
        {
            services?.ShutdownAll();
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void ContentAndNewGameContractsValidateAsNinetyOneTablePackage()
        {
            ContentCatalog catalog = services.Get<ContentCatalogService>().Catalog;
            Assert.That(catalog.ContentVersion, Is.EqualTo("1.0.0-content.11"));
            Assert.That(catalog.Tables.Count, Is.EqualTo(91));
            Assert.That(catalog.GetTable("recruitment_tavern_rules.csv").Rows.Count, Is.EqualTo(4));
            Assert.That(catalog.GetTable("recruitment_grade_cost_rules.csv").Rows.Count, Is.EqualTo(3));
            Assert.That(catalog.GetTable("recruitment_history_rules.csv").Rows.Count, Is.EqualTo(1));
            RecruitmentOverviewDto overview = recruitment.GetOverview();
            Assert.That(overview.Candidates.Count, Is.EqualTo(3));
            Assert.That(overview.Candidates.All(value => new[] { "GRADE_C", "GRADE_B", "GRADE_A" }.Contains(value.GradeId)), Is.True);
            Assert.That(overview.Authority, Is.EqualTo("MOCK_ONLY"));
            AssertValid(game.Snapshot(), "P13_NEW_GAME");
        }

        [Test]
        public void MigrationPreservesServerCacheAndPendingRequestsWithoutMutatingSource()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P13", "p13-migration-before.golden.json");
            JObject source = StrictJson.ParseObject(File.ReadAllText(path));
            JObject untouched = (JObject)source.DeepClone();
            JObject migrated = new P12ToP13ContentMigration().Apply(source);
            Assert.That(JToken.DeepEquals(source, untouched), Is.True);
            Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.11"));
            Assert.That(migrated["payload"]!["recruitmentMockState"]!["pendingRequests"]!.Count(), Is.EqualTo(source["payload"]!["recruitmentMockState"]!["pendingRequests"]!.Count()));
            Assert.That(migrated["payload"]!["recruitmentMockState"]!["pityCounters"]!.Count(), Is.EqualTo(source["payload"]!["recruitmentMockState"]!["pityCounters"]!.Count()));
            Assert.That(migrated["payload"]!["recruitmentMockState"]!["tavern"]!["candidates"]!.Count(), Is.Zero);
            Assert.Throws<InvalidOperationException>(() => new P12ToP13ContentMigration().Apply(migrated));
        }

        [Test]
        public void LockedCandidateSurvivesPaidRefreshAndGoldIsLedgeredOnce()
        {
            RecruitmentCandidateDto selected = recruitment.GetOverview().Candidates[0];
            recruitment.SetCandidateLock(recruitment.CreateLockCommand(selected.CandidateId, true));
            long goldBefore = recruitment.GetOverview().KingdomGold;
            RefreshTavernCommand command = recruitment.CreateRefreshCommand(false);
            RecruitmentOperationResult first = recruitment.Refresh(command);
            long revision = game.Revision;
            RecruitmentOperationResult replay = recruitment.Refresh(command);
            JObject saved = game.Snapshot();
            Assert.That(first.ResultCode, Is.EqualTo("P13_TAVERN_REFRESHED"));
            Assert.That(replay.Replayed, Is.True);
            Assert.That(game.Revision, Is.EqualTo(revision));
            Assert.That(recruitment.GetOverview().KingdomGold, Is.EqualTo(goldBefore - 250));
            Assert.That(recruitment.GetOverview().Candidates.Single(value => value.CandidateId == selected.CandidateId).Locked, Is.True);
            Assert.That(saved["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!.Children<JObject>().Count(value => value.Value<string>("transactionType") == "TAVERN_REFRESH"), Is.EqualTo(1));
            AssertValid(saved, "P13_PAID_REFRESH");
        }

        [Test]
        public void HiringCreatesUniqueMercenaryAndAtomicEconomyHistoryEvent()
        {
            RecruitmentOverviewDto before = recruitment.GetOverview();
            RecruitmentCandidateDto candidate = before.Candidates.OrderBy(value => value.HireCost).First();
            RecruitmentOperationResult result = recruitment.Hire(recruitment.CreateHireCommand(candidate.CandidateId));
            JObject saved = game.Snapshot();
            Assert.That(result.ResultCode, Is.EqualTo("P13_CANDIDATE_HIRED"));
            Assert.That(recruitment.GetOverview().Owned, Is.EqualTo(before.Owned + 1));
            Assert.That(recruitment.GetOverview().KingdomGold, Is.EqualTo(before.KingdomGold - candidate.HireCost));
            Assert.That(saved["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == result.InstanceId).Value<string>("gradeId"), Is.EqualTo(candidate.GradeId));
            Assert.That(saved["payload"]!["recruitmentMockState"]!["history"]!.Children<JObject>().Single().Value<string>("poolType"), Is.EqualTo("TAVERN"));
            Assert.That(saved["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!.Children<JObject>().Single(value => value.Value<string>("transactionType") == "TAVERN_HIRE").Value<long>("kingdomGoldDelta"), Is.EqualTo(-candidate.HireCost));
            AssertValid(saved, "P13_HIRE");
        }

        [Test]
        public void SpecialTicketRecruitmentConsumesWalletAndWritesServerStyleReceipt()
        {
            RecruitmentOverviewDto before = recruitment.GetOverview();
            RecruitmentOperationResult result = recruitment.RecruitSpecial(recruitment.CreateSpecialCommand("SPECIAL_STANDARD_TICKET", "TICKET"));
            JObject saved = game.Snapshot();
            JObject history = saved["payload"]!["recruitmentMockState"]!["history"]!.Children<JObject>().Single();
            Assert.That(result.ResultCode, Is.EqualTo("P13_SPECIAL_COMPLETED"));
            Assert.That(recruitment.GetOverview().Tickets, Is.EqualTo(before.Tickets - 1));
            Assert.That(recruitment.GetOverview().Owned, Is.EqualTo(before.Owned + 1));
            Assert.That(history.Value<string>("serverReceiptId"), Does.StartWith("DEV-"));
            Assert.That(new[] { "GRADE_A", "GRADE_S", "GRADE_SS" }, Does.Contain(history["resultGradeIds"]!.Values<string>().Single()));
            AssertValid(saved, "P13_SPECIAL");
        }

        [Test]
        public void HardPityAndFeaturedGuaranteeProduceWarriorSsAndResetCounters()
        {
            JObject draft = game.Snapshot();
            JObject state = (JObject)draft["payload"]!["recruitmentMockState"]!;
            state["pityCounters"] = new JArray(
                new JObject { ["pityGroupId"] = "PITY_GROUP_SPECIAL_STANDARD", ["pityRuleId"] = "PITY_S_PLUS", ["pullCount"] = 9, ["lastUpdatedContentVersion"] = "1.0.0-content.11" },
                new JObject { ["pityGroupId"] = "PITY_GROUP_SPECIAL_STANDARD", ["pityRuleId"] = "PITY_SS", ["pullCount"] = 79, ["lastUpdatedContentVersion"] = "1.0.0-content.11" });
            state["featuredGuarantees"] = new JArray(new JObject { ["pityGroupId"] = "PITY_GROUP_SPECIAL_STANDARD", ["rateUpGroupId"] = "RATE_UP_WARRIOR", ["state"] = "NEXT_S_OR_SS_FEATURED", ["lastUpdatedContentVersion"] = "1.0.0-content.11" });
            Commit(draft);
            RecruitmentOperationResult result = recruitment.RecruitSpecial(recruitment.CreateSpecialCommand("SPECIAL_WARRIOR_TICKET", "TICKET"));
            JObject saved = game.Snapshot();
            JObject mercenary = saved["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == result.InstanceId);
            JObject history = saved["payload"]!["recruitmentMockState"]!["history"]!.Children<JObject>().Single();
            Assert.That(mercenary.Value<string>("gradeId"), Is.EqualTo("GRADE_SS"));
            Assert.That(mercenary.Value<string>("jobId"), Is.EqualTo("JOB_WARRIOR"));
            Assert.That(saved["payload"]!["recruitmentMockState"]!["pityCounters"]!.Children<JObject>().All(value => value.Value<long>("pullCount") == 0), Is.True);
            Assert.That(history["guaranteeReasonIds"]!.Values<string>(), Does.Contain("PITY_SS"));
            Assert.That(history["guaranteeReasonIds"]!.Values<string>(), Does.Contain("RATE_UP_GUARANTEED"));
            AssertValid(saved, "P13_PITY");
        }

        [Test]
        public void HashRevisionAndCapacityFailuresLeaveSaveUntouched()
        {
            long before = game.Revision;
            Guid badId = Guid.Parse("019fa180-0000-7000-8000-000000000131");
            Assert.That(Assert.Throws<RecruitmentDomainException>(() => recruitment.Refresh(new RefreshTavernCommand(badId, before, new string('0', 64), false))).Code, Is.EqualTo("P13_OPERATION_HASH_MISMATCH"));
            Assert.That(game.Revision, Is.EqualTo(before));

            Guid staleId = Guid.Parse("019fa180-0000-7000-8000-000000000132");
            var staleDraft = new RefreshTavernCommand(staleId, before - 1, null, false);
            var stale = new RefreshTavernCommand(staleId, before - 1, new RecruitmentRequestHasher().Compute(staleDraft), false);
            Assert.That(Assert.Throws<RecruitmentDomainException>(() => recruitment.Refresh(stale)).Code, Is.EqualTo("P13_SAVE_REVISION_CONFLICT"));
            Assert.That(game.Revision, Is.EqualTo(before));

            while (recruitment.GetOverview().Owned < recruitment.GetOverview().Limit)
            {
                RecruitmentOverviewDto overview = recruitment.GetOverview();
                string pool = overview.Tickets > 0 ? "SPECIAL_STANDARD_TICKET" : "SPECIAL_STANDARD_FREE_PREMIUM";
                string payment = overview.Tickets > 0 ? "TICKET" : "FREE_PREMIUM";
                recruitment.RecruitSpecial(recruitment.CreateSpecialCommand(pool, payment));
            }
            long capacityRevision = game.Revision;
            Assert.That(Assert.Throws<RecruitmentDomainException>(() => recruitment.Hire(recruitment.CreateHireCommand(recruitment.GetOverview().Candidates[0].CandidateId))).Code, Is.EqualTo("P13_OWNED_LIMIT_REACHED"));
            Assert.That(game.Revision, Is.EqualTo(capacityRevision));
        }

        private void Commit(JObject draft)
        {
            SaveService save = services.Get<SaveService>();
            JObject prepared = save.Validator.PrepareForCommit(draft, game.Revision, clock.UtcNow);
            ValidationReport report = save.Validator.Validate(prepared, "P13_TEST_SEED");
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
            SaveWriteResult result = save.Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow);
            Assert.That(result.Success, Is.True, result.ErrorCode);
            game.SynchronizeCommittedDocument(result.Document);
        }

        private void AssertValid(JObject document, string source)
        {
            ValidationReport report = services.Get<SaveService>().Validator.Validate(document, source);
            Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues));
        }

        private string Read(string name) => File.ReadAllText(Path.Combine(contracts, name));
    }
}
