using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Raids;
using KingdomTycoon.Domain.Raids;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Raids;
using KingdomTycoon.Infrastructure.Regions;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P14RaidTests
    {
        private string temporaryRoot; private string contracts; private ServiceRegistry services; private DeterministicClock clock; private FacilityGameService game; private RaidGameService raids;
        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p14-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temporaryRoot); contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts"); clock = new DeterministicClock(new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero)); services = new ServiceRegistry();
            services.Register(new SaveService(temporaryRoot, Read("save.schema.json"), Read("save.content.5.schema.json"), Read("save.content.6.schema.json"), Read("save.content.7.schema.json"), Read("save.content.8.schema.json"), Read("save.content.9.schema.json"), Read("save.content.10.schema.json"), Read("save.content.11.schema.json"), Read("save.content.12.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider())); game = new FacilityGameService(clock, Read("p14-new-game.template.json"), Read("p04-new-game.template.json")); services.Register(game); services.Register(new RegionGameService(clock)); raids = new RaidGameService(clock); services.Register(raids); services.InitializeAll(); services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult(); game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult(); services.Get<RegionGameService>().Bootstrap(); SeedRaidReadyParty(); raids.Bootstrap();
        }
        [TearDown] public void TearDown() { services?.ShutdownAll(); if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }

        [Test]
        public void ContentAndSaveContractsLoadNinetyFiveCanonicalTables()
        {
            ContentCatalog catalog = services.Get<ContentCatalogService>().Catalog; Assert.That(catalog.ContentVersion, Is.EqualTo("1.0.0-content.12")); Assert.That(catalog.Tables.Count, Is.EqualTo(95)); Assert.That(catalog.GetTable("raids.csv").Rows.Count, Is.EqualTo(2)); Assert.That(catalog.GetTable("raid_difficulties.csv").Rows.Count, Is.EqualTo(6)); Assert.That(catalog.GetTable("raid_parts.csv").Rows.Count, Is.EqualTo(7)); Assert.That(raids.GetOverview().Raids.Count, Is.EqualTo(6)); AssertValid(game.Snapshot(), "P14_NEW_GAME");
        }

        [Test]
        public void MigrationPreservesSourceAndBuildsCanonicalSixDifficultyProgressRows()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "..", "..", "docs", "goldens", "P14", "p14-migration-before.golden.json"); JObject source = StrictJson.ParseObject(File.ReadAllText(path)); JObject untouched = (JObject)source.DeepClone(); JObject migrated = new P13ToP14ContentMigration().Apply(source); Assert.That(JToken.DeepEquals(source, untouched), Is.True); Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.12")); Assert.That(migrated["payload"]!["regions"]!["raids"]!.Count(), Is.EqualTo(6)); Assert.Throws<InvalidOperationException>(() => new P13ToP14ContentMigration().Apply(migrated));
        }

        [Test]
        public void HydraClearPersistsHistoryJournalRewardsAndReplayExactlyOnce()
        {
            RaidOverviewDto state = raids.GetOverview(); RaidSummaryDto raid = state.Raids.Single(value => value.Id == "RAID_HYDRA" && value.Difficulty == "NORMAL"); string[] party = state.PartyCandidates.Where(value => value.Eligible).Take(6).Select(value => value.Id).ToArray(); ResolveRaidCommand command = raids.CreateResolveCommand(raid.Id, raid.Difficulty, party, "HYDRA_BODY", true); RaidOperationResult first = raids.Resolve(command); long revision = game.Revision; RaidOperationResult replay = raids.Resolve(command); JObject saved = game.Snapshot();
            Assert.That(first.Success, Is.True, "Hydra should be defeated by the seeded raid-ready party."); Assert.That(first.ResultCode, Is.EqualTo("P14_RAID_CLEARED")); Assert.That(replay.Replayed, Is.True, "The identical operation must replay."); Assert.That(game.Revision, Is.EqualTo(revision)); Assert.That(saved["payload"]!["regions"]!["raidHistory"]!.Count(), Is.EqualTo(1)); Assert.That(saved["payload"]!["operationJournal"]!.Children<JObject>().Count(value => value.Value<string>("operationType") == "RAID_RESOLVE"), Is.EqualTo(1)); Assert.That(saved["payload"]!["regions"]!["progress"]!.Children<JObject>().Single(value => value.Value<string>("regionId") == "REGION_R05").Value<bool>("unlocked"), Is.True, "Hydra NORMAL first clear must unlock R05."); AssertValid(saved, "P14_HYDRA_CLEAR");
        }

        [Test]
        public void HashRevisionPartyAndWarningFailuresDoNotMutateSave()
        {
            RaidOverviewDto state = raids.GetOverview(); string[] party = state.PartyCandidates.Take(6).Select(value => value.Id).ToArray(); long before = game.Revision; Guid badId = Guid.Parse("019fa180-0000-7000-8000-000000000141"); var bad = new ResolveRaidCommand(badId, before, new string('0', 64), "RAID_HYDRA", "NORMAL", party, "HYDRA_HEAD", true); Assert.That(Assert.Throws<RaidDomainException>(() => raids.Resolve(bad)).Code, Is.EqualTo("P14_OPERATION_HASH_MISMATCH")); Guid staleId = Guid.Parse("019fa180-0000-7000-8000-000000000142"); var staleDraft = new ResolveRaidCommand(staleId, before - 1, null, "RAID_HYDRA", "NORMAL", party, "HYDRA_HEAD", true); var stale = new ResolveRaidCommand(staleId, before - 1, new RaidRequestHasher().Compute(staleDraft), "RAID_HYDRA", "NORMAL", party, "HYDRA_HEAD", true); Assert.That(Assert.Throws<RaidDomainException>(() => raids.Resolve(stale)).Code, Is.EqualTo("P14_SAVE_REVISION_CONFLICT")); Assert.That(game.Revision, Is.EqualTo(before));
        }

        [Test]
        public void PureSimulationIsDeterministicAndBoundsTrace()
        {
            var party = Enumerable.Range(1, 6).Select(index => new RaidCombatantSpec { Id = "M" + index, JobId = "JOB_WARRIOR", MaxHp = 3000, Attack = 240, Defense = 160, HealPower = index == 1 ? 120 : 0, AvailablePotions = 2 }).ToArray(); var spec = new RaidSimulationSpec { RaidId = "RAID_HYDRA", TargetPartId = "BODY", TimeLimitTicks = 3000, BossMaxHp = 65000, BossAttack = 950, BossDefense = 320, DifficultyScaleBps = 10000, Party = party, Parts = new[] { new RaidPartSpec { Id = "BODY", MaxHp = 24700, BossDamageShareBps = 8000, TargetOrder = 1 } }, Phases = new[] { new RaidPhaseSpec { PhaseNo = 1, TriggerBossHpBps = 10000, AbilityTag = "BITE", ActionIntervalTicks = 20, AttackMultiplierBps = 10000 } } }; DeterministicRaidSimulation simulation = new(); RaidSimulationOutcome left = simulation.Run(spec); RaidSimulationOutcome right = simulation.Run(spec); Assert.That(left.Success, Is.True); Assert.That(right.DurationTicks, Is.EqualTo(left.DurationTicks)); Assert.That(right.Members.Select(value => value.Total), Is.EqualTo(left.Members.Select(value => value.Total))); Assert.That(left.Trace.Count, Is.LessThanOrEqualTo(64));
        }

        private void SeedRaidReadyParty()
        {
            JObject draft = game.Snapshot(); JObject kingdom = (JObject)draft["payload"]!["kingdom"]!; kingdom["kingdomStageId"] = "KINGDOM_5"; kingdom["activeMercenaryLimit"] = 8; JArray mercenaries = (JArray)draft["payload"]!["mercenaries"]!; while (mercenaries.Count < 6) { JObject clone = (JObject)mercenaries[mercenaries.Count % 4]!.DeepClone(); clone["instanceId"] = $"019fa180-0000-7000-8000-{mercenaries.Count + 1:000000000000}"; clone["displayName"] = "레이드원" + (mercenaries.Count + 1); mercenaries.Add(clone); } int index = 0; foreach (JObject member in mercenaries.Children<JObject>()) { member["rankId"] = "RANK_HERO"; member["level"] = 60; member["active"] = true; member["autonomy"]!["state"] = "IDLE_TOWN"; member["autonomy"]!["currentRegionId"] = null; member["autonomy"]!["reasonCode"] = "NONE"; member["jobId"] = new[] { "JOB_GUARDIAN", "JOB_CLERIC", "JOB_ARCHER", "JOB_MAGE", "JOB_WARRIOR", "JOB_GUARDIAN" }[index++ % 6]; }
            JObject region4 = draft["payload"]!["regions"]!["progress"]!.Children<JObject>().Single(value => value.Value<string>("regionId") == "REGION_R04"); region4["unlocked"] = true; region4["progressPercent"] = 100; region4["firstUnlockedAtUtc"] = "2026-07-22T00:00:00.000Z"; Commit(draft);
        }
        private void Commit(JObject draft) { SaveWriteResult result = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow); Assert.That(result.Success, Is.True, result.ErrorCode + "\n" + string.Join("\n", result.Report.Issues)); game.SynchronizeCommittedDocument(result.Document); }
        private void AssertValid(JObject document, string source) { ValidationReport report = services.Get<SaveService>().Validator.Validate(document, source); Assert.That(report.IsValid, Is.True, string.Join("\n", report.Issues)); }
        private string Read(string name) => File.ReadAllText(Path.Combine(contracts, name));
    }
}
