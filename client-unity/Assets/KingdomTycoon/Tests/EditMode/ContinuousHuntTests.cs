using System;
using System.IO;
using System.Linq;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Economy;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class ContinuousHuntTests
    {
        private string temporaryRoot;
        private string contracts;
        private ServiceRegistry services;
        private MutableClock clock;
        private FacilityGameService game;
        private ContinuousHuntGameService hunt;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(Path.GetTempPath(), "kingdom-tycoon-continuous-hunt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            contracts = Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts");
            clock = new MutableClock(new DateTimeOffset(2026, 7, 22, 0, 0, 0, TimeSpan.Zero));
            services = new ServiceRegistry();
            services.Register(new SaveService(temporaryRoot, Read("save.schema.json"), Read("save.content.5.schema.json"), Read("save.content.6.schema.json"), Read("save.content.7.schema.json"), Read("save.content.8.schema.json"), Read("save.content.9.schema.json"), Read("save.content.10.schema.json"), Read("save.content.11.schema.json"), Read("save.content.12.schema.json"), Read("save.content.13.schema.json")));
            services.Register(new ContentCatalogService(new LocalStreamingAssetReader(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets")), new CompileTimeActiveContentVersionProvider()));
            game = new FacilityGameService(clock, Read("p15-new-game.template.json"), Read("p04-new-game.template.json")); services.Register(game);
            var economy = new EconomyGameService(clock); services.Register(economy);
            hunt = new ContinuousHuntGameService(clock); services.Register(hunt);
            services.InitializeAll();
            services.Get<ContentCatalogService>().LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
            game.BootstrapAsync(CancellationToken.None).GetAwaiter().GetResult(); economy.Bootstrap(); hunt.Bootstrap();
        }

        [TearDown]
        public void TearDown() { services?.ShutdownAll(); if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true); }

        [Test]
        public void LegacyAutonomyIsNormalizedWithoutChangingContentOrSaveVersion()
        {
            JObject legacy = game.Snapshot();
            foreach (JObject autonomy in legacy["payload"]!["mercenaries"]!.Children<JObject>().Select(value => (JObject)value["autonomy"]!))
                foreach (string field in new[] { "assignedRegionId", "autoResume", "currentHpBps", "bagFill", "bagCapacity", "pendingSaleGold", "cyclesCompleted", "earnedGold" }) autonomy.Remove(field);
            Assert.That(ContinuousHuntGameService.NormalizeDocument(legacy, clock.UtcNow), Is.True);
            Assert.That(legacy.Value<int>("saveVersion"), Is.EqualTo(1));
            Assert.That(legacy.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.13"));
            Assert.That(legacy["payload"]!["mercenaries"]!.Children<JObject>().All(value => value["autonomy"]!.Value<int>("currentHpBps") == 10000), Is.True);
            SaveWriteResult write = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, legacy, game.Revision, clock.UtcNow);
            Assert.That(write.Success, Is.True, write.ErrorCode + "\n" + string.Join("\n", write.Report.Issues));
        }

        [Test]
        public void SeveralMercenariesCanPersistInOneGroundAndAdvanceIndependently()
        {
            string[] ids = hunt.GetOverview().Members.Take(2).Select(value => value.InstanceId).ToArray();
            hunt.Assign(ids[0], "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(2));
            hunt.Assign(ids[1], "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(42));
            hunt.AdvanceTo(clock.UtcNow);
            ContinuousHuntMemberDto[] members = hunt.GetOverview().Members.Where(value => ids.Contains(value.InstanceId)).ToArray();
            Assert.That(members.All(value => value.AssignedRegionId == "REGION_R01"), Is.True);
            Assert.That(members.All(value => value.CyclesCompleted > 0), Is.True);
            Assert.That(members[0].State, Is.Not.EqualTo(members[1].State), "staggered assignment must not collapse to a party state");
        }

        [Test]
        public void RecallDoesNotStopOtherMercenaryAndAssignmentSurvivesTownCycle()
        {
            string[] ids = hunt.GetOverview().Members.Take(2).Select(value => value.InstanceId).ToArray();
            hunt.Assign(ids[0], "REGION_R01"); hunt.Assign(ids[1], "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(90)); hunt.AdvanceTo(clock.UtcNow);
            hunt.Unassign(ids[0]); clock.Advance(TimeSpan.FromSeconds(40)); hunt.AdvanceTo(clock.UtcNow);
            ContinuousHuntOverviewDto overview = hunt.GetOverview();
            ContinuousHuntMemberDto recalled = overview.Members.Single(value => value.InstanceId == ids[0]);
            ContinuousHuntMemberDto active = overview.Members.Single(value => value.InstanceId == ids[1]);
            Assert.That(recalled.AssignedRegionId, Is.Null);
            Assert.That(active.AssignedRegionId, Is.EqualTo("REGION_R01"));
            Assert.That(active.CyclesCompleted, Is.GreaterThan(0));
            Assert.That(active.EarnedGold, Is.GreaterThan(0), "full bag must be sold before automatic resume");
        }

        private string Read(string name) => File.ReadAllText(Path.Combine(contracts, name));

        private sealed class MutableClock : ITrustedUtcClock
        {
            public MutableClock(DateTimeOffset value) => UtcNow = value;
            public DateTimeOffset UtcNow { get; private set; }
            public bool IsTrusted => true;
            public long MonotonicTicks => UtcNow.UtcTicks;
            public void Advance(TimeSpan value) => UtcNow = UtcNow.Add(value);
        }
    }
}
