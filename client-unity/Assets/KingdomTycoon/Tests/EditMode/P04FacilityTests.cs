using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Content.Migrations;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P04FacilityTests
    {
        private string temporaryRoot;
        private ServiceRegistry services;
        private DeterministicClock clock;
        private FacilityGameService game;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-p04-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            clock = new DeterministicClock(new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero));
            string assetRoot = UnityEngine.Application.dataPath;
            string schema = File.ReadAllText(Path.Combine(assetRoot, "KingdomTycoon", "Resources", "Contracts", "save.schema.json"));
            string template = File.ReadAllText(Path.Combine(assetRoot, "KingdomTycoon", "Resources", "Contracts", "p04-new-game.template.json"));
            services = new ServiceRegistry();
            services.Register(new SaveService(temporaryRoot, schema));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(assetRoot, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, template);
            services.Register(game);
            services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
            game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [TearDown]
        public void TearDown()
        {
            services?.ShutdownAll();
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void RequestHashGoldens_MatchCorrectionAppendix()
        {
            var hasher = new FacilityRequestHasher();
            Assert.That(hasher.ComputeHash(new StartFacilityBuildCommand(Guid.Parse("019f77ac-2c00-7002-8000-000000000001"), null, 12, "FAC_STORE", 1)), Is.EqualTo("1aeb5d362aa9e3cac14c7b41815706033eb89ac48dc0db738e834eebd7a57f88"));
            Assert.That(hasher.ComputeHash(new StartFacilityUpgradeCommand(Guid.Parse("019f77ac-2c00-7002-8000-000000000002"), null, 12, "FAC_STORE", 2)), Is.EqualTo("33d1a481922f108cbf9f6741bdc1d6dae05fe674839a9c9af888dc31986f7196"));
            Assert.That(hasher.ComputeHash(new ClaimFacilityJobCommand(Guid.Parse("019f77ac-2c00-7002-8000-000000000003"), null, 13, "FAC_STORE", Guid.Parse("019f77ac-2c00-7002-8000-000000000001"))), Is.EqualTo("5a0c35f7fe9a5ac6598a3ad395ff82c172d11d60a33f5a1c652972978fcdb693"));
            Assert.That(hasher.ComputeHash(new AssignManagementNpcCommand(Guid.Parse("019f77ac-2c00-7002-8000-000000000004"), null, 14, "FAC_STORE", Guid.Parse("019f77ac-2c00-7001-8000-000000000001"))), Is.EqualTo("c196c8327648bf8d72ecca3ed13e3b1429493e368b58cb405e281247cef98349"));
            Assert.That(hasher.ComputeHash(new UnassignManagementNpcCommand(Guid.Parse("019f77ac-2c00-7002-8000-000000000005"), null, 15, "FAC_STORE", Guid.Parse("019f77ac-2c00-7001-8000-000000000001"))), Is.EqualTo("9eebf15000fbfe2f8c40f82a321270cc3362e5ec7959e9554c71d5069884a589"));
        }

        [Test]
        public void NewGame_ContainsP04CanonicalBaseline()
        {
            Assert.That(game.Revision, Is.EqualTo(1));
            Assert.That(game.CurrentDocument.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.2"));
            Assert.That(game.CurrentDocument["payload"]["kingdom"].Value<long>("kingdomGold"), Is.EqualTo(5000));
            Assert.That(game.CurrentDocument["payload"]["facilities"].Count(), Is.EqualTo(8));
            Assert.That(game.CurrentDocument["payload"]["managementNpcs"].Count(), Is.EqualTo(4));
        }

        [Test]
        public void P03BootstrapSignature_MigratesToP04Baseline()
        {
            string templateJson = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts", "p04-new-game.template.json"));
            JObject before = JObject.Parse(templateJson);
            before["contentVersion"] = "1.0.0-content.1";
            before["payload"]["kingdom"]["kingdomGold"] = 0;
            before["payload"]["managementNpcs"] = new JArray();
            foreach (JObject facility in before["payload"]["facilities"].Children<JObject>()) facility["state"] = "LOCKED";
            var migration = new P03ToP04ContentMigration(templateJson);
            Assert.That(migration.CanApply(before), Is.True);
            JObject after = migration.Apply(before);
            Assert.That(after.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.2"));
            Assert.That(after["payload"]["kingdom"].Value<long>("kingdomGold"), Is.EqualTo(5000));
            Assert.That(after["payload"]["managementNpcs"].Count(), Is.EqualTo(4));
            Assert.That(after["payload"]["facilities"].Children<JObject>().Count(value => value.Value<string>("state") == "ACTIVE"), Is.EqualTo(4));
        }

        [Test]
        public void ManagedFacility_BuildClaimAssignUnassign_PersistsStateMachine()
        {
            var requests = new FacilityOperationRequestFactory(new SystemUuidV7Provider(), new FacilityRequestHasher());
            var build = requests.CreateBuild(game.Revision, "FAC_STORE");
            game.StartBuild(build);
            AssertState("FAC_STORE", "BUILDING", "RUNNING");
            clock.Advance(TimeSpan.FromSeconds(45));
            Assert.That(game.NormalizeExpiredJobs(), Is.True);
            AssertState("FAC_STORE", "BUILDING", "READY");
            game.Claim(requests.CreateClaim(game.Revision, "FAC_STORE", build.OperationId));
            AssertState("FAC_STORE", "STOPPED", "CLAIMED");

            Guid merchant = Guid.Parse(game.CurrentDocument["payload"]["managementNpcs"].Children<Newtonsoft.Json.Linq.JObject>().Single(value => value.Value<string>("professionId") == "NPC_MERCHANT").Value<string>("instanceId"));
            game.Assign(requests.CreateAssign(game.Revision, "FAC_STORE", merchant));
            AssertState("FAC_STORE", "ACTIVE", null);
            game.Unassign(requests.CreateUnassign(game.Revision, "FAC_STORE", merchant));
            AssertState("FAC_STORE", "STOPPED", null);

            SaveLoadResult reloaded = services.Get<SaveService>().Repository.Load(game.ActiveProfileId);
            Assert.That(reloaded.Success, Is.True, reloaded.ErrorCode);
            Assert.That(reloaded.Document["payload"]["facilities"].Children<Newtonsoft.Json.Linq.JObject>().Single(value => value.Value<string>("facilityId") == "FAC_STORE").Value<string>("state"), Is.EqualTo("STOPPED"));
        }

        private void AssertState(string facilityId, string state, string jobStatus)
        {
            Newtonsoft.Json.Linq.JObject facility = game.CurrentDocument["payload"]["facilities"].Children<Newtonsoft.Json.Linq.JObject>().Single(value => value.Value<string>("facilityId") == facilityId);
            Assert.That(facility.Value<string>("state"), Is.EqualTo(state));
            if (jobStatus == null) Assert.That(facility["job"].Type, Is.EqualTo(Newtonsoft.Json.Linq.JTokenType.Null));
            else Assert.That(facility["job"].Value<string>("status"), Is.EqualTo(jobStatus));
        }
    }
}
