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
            hunt = new ContinuousHuntGameService(clock, Read("automatic-growth.rules.json"), Read("world-hunt.rules.json")); services.Register(hunt);
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
            ((JObject)legacy["payload"]!).Remove("worldHunt");
            foreach (JObject autonomy in legacy["payload"]!["mercenaries"]!.Children<JObject>().Select(value => (JObject)value["autonomy"]!))
                foreach (string field in new[] { "assignedRegionId", "autoResume", "currentHpBps", "bagFill", "bagCapacity", "pendingSaleGold", "cyclesCompleted", "earnedGold", "autoGrowthEnabled", "autoSkillTraining", "autoEquipmentEnhancement", "growthPersonalGoldReserve", "lastGrowthAction", "lastGrowthResultCode", "lastGrowthAtUtc", "huntBag", "pendingBountyGold", "pendingLootTableId", "pendingMonsterId", "pendingKillSequence", "lastCombatDamage", "lastSkillId", "combatTickCount" }) autonomy.Remove(field);
            foreach (JObject mercenary in legacy["payload"]!["mercenaries"]!.Children<JObject>()) mercenary.Remove("skillGrowth");
            Assert.That(ContinuousHuntGameService.NormalizeDocument(legacy, clock.UtcNow), Is.True);
            Assert.That(legacy.Value<int>("saveVersion"), Is.EqualTo(1));
            Assert.That(legacy.Value<string>("contentVersion"), Is.EqualTo("1.0.0-content.13"));
            Assert.That(legacy["payload"]!["mercenaries"]!.Children<JObject>().All(value => value["autonomy"]!.Value<int>("currentHpBps") == 10000), Is.True);
            Assert.That(legacy["payload"]!["mercenaries"]!.Children<JObject>().All(value => value["autonomy"]!["huntBag"] != null), Is.True);
            Assert.That(legacy["payload"]!["worldHunt"]!.Value<int>("worldVersion"), Is.EqualTo(1));
            SaveWriteResult write = services.Get<SaveService>().Repository.Save(game.ActiveProfileId, legacy, game.Revision, clock.UtcNow);
            Assert.That(write.Success, Is.True, write.ErrorCode + "\n" + string.Join("\n", write.Report.Issues));
        }

        [Test]
        public void OverviewDerivesKoreanRiskDropPreviewAndValidatedFeedbackPolicy()
        {
            ContinuousHuntOverviewDto overview = hunt.GetOverview();
            Assert.That(overview.Regions.Select(value => value.RiskLabel), Is.EqualTo(new[] { "안정", "주의", "위험", "고위험", "극한" }));
            Assert.That(overview.Regions.All(value => !string.IsNullOrWhiteSpace(value.DropPreview) && !value.DropPreview.Contains("MAT_")), Is.True);
            Assert.That(overview.Regions.First().DropPreview, Does.Contain("멧돼지 가죽"));
            Assert.That(overview.Feedback.MaximumRewardFeedEntries, Is.EqualTo(3)); Assert.That(overview.Feedback.MasterVolumeBps, Is.EqualTo(2200));

            JObject invalid = JObject.Parse(Read("world-hunt.rules.json")); invalid["feedback"]!["maximumRewardFeedEntries"] = 0;
            Assert.Throws<InvalidOperationException>(() => new ContinuousHuntGameService(clock, Read("automatic-growth.rules.json"), invalid.ToString()));
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
            Assert.That(members.Where(value => value.TargetMonsterInstanceId != null).Select(value => value.TargetMonsterInstanceId).Distinct().Count(), Is.EqualTo(members.Count(value => value.TargetMonsterInstanceId != null)), "each mercenary must keep an independent target reservation");
            Assert.That(hunt.GetOverview().Monsters.Where(value => value.RegionId == "REGION_R01").Any(value => value.CurrentHp < value.MaxHp || value.State == "RESPAWNING"), Is.True);
        }

        [Test]
        public void RecallDoesNotStopOtherMercenaryAndAssignmentSurvivesTownCycle()
        {
            string[] ids = hunt.GetOverview().Members.Take(2).Select(value => value.InstanceId).ToArray();
            hunt.Assign(ids[0], "REGION_R01"); hunt.Assign(ids[1], "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(220)); hunt.AdvanceTo(clock.UtcNow);
            hunt.Unassign(ids[0]); clock.Advance(TimeSpan.FromSeconds(40)); hunt.AdvanceTo(clock.UtcNow);
            ContinuousHuntOverviewDto overview = hunt.GetOverview();
            ContinuousHuntMemberDto recalled = overview.Members.Single(value => value.InstanceId == ids[0]);
            ContinuousHuntMemberDto active = overview.Members.Single(value => value.InstanceId == ids[1]);
            Assert.That(recalled.AssignedRegionId, Is.Null);
            Assert.That(active.AssignedRegionId, Is.EqualTo("REGION_R01"));
            Assert.That(active.CyclesCompleted, Is.GreaterThan(0));
            Assert.That(active.EarnedGold, Is.GreaterThan(0), "full bag must be sold before automatic resume");
        }

        [Test]
        public void TownReturnTrainsUnlockedSkillAndPersistsItsHuntBonus()
        {
            string id = hunt.GetOverview().Members.First().InstanceId;
            hunt.Assign(id, "REGION_R01"); clock.Advance(TimeSpan.FromSeconds(360)); hunt.AdvanceTo(clock.UtcNow); hunt.AdvanceTo(clock.UtcNow);
            ContinuousHuntMemberDto member = hunt.GetOverview().Members.Single(value => value.InstanceId == id);
            JObject saved = game.Snapshot(); JObject actor = saved["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == id);
            Assert.That(member.TotalSkillLevels, Is.GreaterThanOrEqualTo(1));
            Assert.That(actor["skillGrowth"]!["skills"]!.Count(), Is.GreaterThanOrEqualTo(1));
            Assert.That(actor["skillGrowth"]!.Value<long>("totalSpentGold"), Is.GreaterThan(0));
            Assert.That(member.AssignedRegionId, Is.EqualTo("REGION_R01"), "growth must resume the assigned hunt");
        }

        [Test]
        public void TownReturnEnhancesOwnedEquippedItemWithoutBreakingResume()
        {
            JObject seed = game.Snapshot(); JObject actor = seed["payload"]!["mercenaries"]!.Children<JObject>().First(); string actorId = actor.Value<string>("instanceId");
            JObject blacksmith = seed["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_BLACKSMITH");
            JObject blacksmithNpc = seed["payload"]!["managementNpcs"]!.Children<JObject>().Single(value => value.Value<string>("professionId") == "NPC_BLACKSMITH");
            blacksmith["state"] = "ACTIVE"; blacksmith["level"] = 1; blacksmith["assignedNpcInstanceId"] = blacksmithNpc.Value<string>("instanceId");
            blacksmithNpc["assignedFacilityId"] = "FAC_BLACKSMITH"; blacksmithNpc["working"] = true; actor["personalGold"] = 1000;
            ((JArray)seed["payload"]!["inventory"]!["itemStacks"]!).Add(new JObject { ["itemId"] = "MAT_ENHANCE_1", ["quantity"] = 2 });
            ((JArray)seed["payload"]!["inventory"]!["equipment"]!).Add(new JObject
            {
                ["instanceId"] = "019f9000-0000-7000-8000-000000000101", ["equipmentTemplateId"] = "EQ_T1_WARRIOR_WEAPON", ["tier"] = 1,
                ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = 0, ["refineOption"] = null, ["locked"] = false,
                ["equippedByMercenaryInstanceId"] = actorId, ["sourceContentVersion"] = "1.0.0-content.13", ["generationOperationId"] = "019f9000-0000-7000-8000-000000000102",
                ["enhancementPityBps"] = 0, ["enhancementAttemptCount"] = 0, ["enhancementMaterialInvested"] = new JArray(), ["pendingRefineOption"] = null, ["refineRollCount"] = 0
            });
            actor["equipmentSlots"]!["WEAPON"] = "019f9000-0000-7000-8000-000000000101";
            Commit(seed); hunt.Assign(actorId, "REGION_R01"); clock.Advance(TimeSpan.FromSeconds(360)); hunt.AdvanceTo(clock.UtcNow); hunt.AdvanceTo(clock.UtcNow);
            ContinuousHuntMemberDto member = hunt.GetOverview().Members.Single(value => value.InstanceId == actorId);
            Assert.That(member.BestEnhancementLevel, Is.GreaterThanOrEqualTo(1));
            Assert.That(member.AssignedRegionId, Is.EqualTo("REGION_R01"));
        }

        [Test]
        public void AssignedMercenariesReserveDifferentMonstersAndApplyRealHpDamage()
        {
            string[] ids = hunt.GetOverview().Members.Take(2).Select(value => value.InstanceId).ToArray();
            hunt.Assign(ids[0], "REGION_R01"); hunt.Assign(ids[1], "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(8)); hunt.AdvanceTo(clock.UtcNow);
            ContinuousHuntOverviewDto overview = hunt.GetOverview();
            ContinuousHuntMemberDto[] actors = overview.Members.Where(value => ids.Contains(value.InstanceId)).ToArray();
            Assert.That(actors.All(value => value.TargetMonsterInstanceId != null), Is.True);
            Assert.That(actors.Select(value => value.TargetMonsterInstanceId).Distinct().Count(), Is.EqualTo(2));
            Assert.That(overview.Monsters.Count(value => value.RegionId == "REGION_R01" && value.CurrentHp < value.MaxHp), Is.GreaterThanOrEqualTo(2));
            Assert.That(actors.All(value => value.LastCombatDamage > 0), Is.True);
        }

        [Test]
        public void CanonicalLootEntersPersonalBagBeforeTownSettlement()
        {
            string id = hunt.GetOverview().Members.First().InstanceId; hunt.Assign(id, "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(120)); hunt.AdvanceTo(clock.UtcNow);
            JObject actor = game.Snapshot()["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == id);
            JObject autonomy = (JObject)actor["autonomy"]!;
            Assert.That(autonomy.Value<long>("cyclesCompleted"), Is.GreaterThan(0));
            Assert.That(autonomy.Value<long>("pendingBountyGold") + autonomy.Value<long>("earnedGold"), Is.GreaterThan(0));
            Assert.That(autonomy["huntBag"]!["itemStacks"]!.Count() + autonomy["huntBag"]!["equipment"]!.Count() + game.Snapshot()["payload"]!["inventory"]!["itemStacks"]!.Count(), Is.GreaterThan(0));
        }

        [Test]
        public void TownSettlementMovesBagThroughRealStoreLedgerAndPurchaseAi()
        {
            OpenStore(); JObject seed = game.Snapshot(); JObject actor = seed["payload"]!["mercenaries"]!.Children<JObject>().First(); string actorId = actor.Value<string>("instanceId");
            JObject autonomy = (JObject)actor["autonomy"]!; autonomy["assignedRegionId"] = "REGION_R01"; autonomy["autoResume"] = true; autonomy["currentRegionId"] = "REGION_R01";
            autonomy["state"] = "RETURN_TOWN"; autonomy["reasonCode"] = "INVENTORY_FULL"; autonomy["nextDecisionAtUtc"] = clock.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            autonomy["huntBag"] = new JObject { ["itemStacks"] = new JArray(new JObject { ["itemId"] = "MAT_R01_WILD_HERB", ["quantity"] = 3 }), ["equipment"] = new JArray() };
            autonomy["bagFill"] = 3; autonomy["pendingBountyGold"] = 14L; actor["personalGold"] = 1000L;
            foreach (JProperty slot in actor["equipmentSlots"]!.Children<JProperty>()) slot.Value = JValue.CreateNull();
            foreach (JObject equipped in seed["payload"]!["inventory"]!["equipment"]!.Children<JObject>().Where(value => value["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null && value.Value<string>("equippedByMercenaryInstanceId") == actorId)) equipped["equippedByMercenaryInstanceId"] = null;
            SeedProductionStoreStock(seed, actor.Value<string>("jobId"));
            Commit(seed);
            clock.Advance(TimeSpan.FromSeconds(4)); hunt.AdvanceTo(clock.UtcNow);
            JObject saved = game.Snapshot(); JArray ledger = (JArray)saved["payload"]!["economy"]!["store"]!["ledger"]!["entries"]!;
            Assert.That(ledger.Children<JObject>().Any(value => value.Value<string>("transactionType") == "SELL_TO_STORE"), Is.True);
            Assert.That(saved["payload"]!["economy"]!["store"]!["stackLines"]!.Children<JObject>().Any(value => value.Value<string>("productId") == "MAT_R01_WILD_HERB" && value.Value<string>("sourceType") == "MERCENARY_SALE"), Is.True);
            JObject committedActor = saved["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == actorId);
            Assert.That(committedActor["potions"]!.Children<JObject>().Any(value => value.Value<string>("potionId") == "POT_HEAL_SMALL"), Is.True);
            Assert.That(saved["payload"]!["mercenaries"]!.Children<JObject>().Any(value => value["equipmentSlots"]!.Children<JProperty>().Any(slot => slot.Value.Type == JTokenType.String)), Is.True);
        }

        [Test]
        public void RealtimeMutationsCoalesceIntoOneAtomicSave()
        {
            string id = hunt.GetOverview().Members.First().InstanceId;
            long revisionBefore = game.Revision;

            hunt.Assign(id, "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(8));
            int steps = hunt.AdvanceBudgetedTo(clock.UtcNow, 8);

            Assert.That(steps, Is.GreaterThan(0));
            Assert.That(game.Revision, Is.EqualTo(revisionBefore), "실시간 사냥 갱신은 매 틱 디스크 저장을 만들면 안 됩니다.");
            Assert.That(hunt.HasPendingPersistence, Is.True);
            Assert.That(hunt.FlushPending(clock.UtcNow), Is.True);
            Assert.That(game.Revision, Is.EqualTo(revisionBefore + 1), "여러 실시간 변경은 한 번의 원자 저장으로 병합되어야 합니다.");
            Assert.That(hunt.HasPendingPersistence, Is.False);

            SaveLoadResult loaded = services.Get<SaveService>().Repository.Load(game.ActiveProfileId);
            JObject savedMember = loaded.Document["payload"]!["mercenaries"]!.Children<JObject>()
                .Single(value => value.Value<string>("instanceId") == id);
            Assert.That(savedMember["autonomy"]!.Value<string>("assignedRegionId"), Is.EqualTo("REGION_R01"));
        }

        [Test]
        public void BudgetedAdvanceNeverExceedsFrameStepBudget()
        {
            string[] ids = hunt.GetOverview().Members.Take(3).Select(value => value.InstanceId).ToArray();
            foreach (string id in ids) hunt.Assign(id, "REGION_R01");
            clock.Advance(TimeSpan.FromSeconds(360));

            int steps = hunt.AdvanceBudgetedTo(clock.UtcNow, 7);

            Assert.That(steps, Is.EqualTo(7));
            Assert.That(hunt.HasDueWork(clock.UtcNow), Is.True, "남은 따라잡기 작업은 다음 프레임으로 넘겨야 합니다.");
        }

        [Test]
        public void OverviewReadAndTransientAdoptionAvoidRedundantFullSaveClones()
        {
            JObject current = game.CurrentDocument;

            ContinuousHuntOverviewDto overview = hunt.GetOverview();

            Assert.That(overview.Members, Is.Not.Empty);
            Assert.That(game.CurrentDocument, Is.SameAs(current), "읽기 모델 생성은 대형 Save를 교체하거나 복제하면 안 됩니다.");

            JObject isolatedDraft = game.Snapshot();
            game.SynchronizeTransientDocument(isolatedDraft);

            Assert.That(game.CurrentDocument, Is.SameAs(isolatedDraft), "이미 격리된 임시 draft는 두 번째 전체 Save 복제 없이 소유권을 이전해야 합니다.");
            Assert.That(game.Revision, Is.EqualTo(isolatedDraft.Value<long>("revision")));
        }

        private static void SeedProductionStoreStock(JObject document, string jobId)
        {
            JObject store = (JObject)document["payload"]!["economy"]!["store"]!; const string operationId = "019f9000-0000-7000-8000-000000000801";
            ((JArray)store["stackLines"]!).Add(new JObject
            {
                ["stockLineId"] = "STACK:POTION:POT_HEAL_SMALL:PRODUCTION", ["productKind"] = "POTION", ["productId"] = "POT_HEAL_SMALL",
                ["quantity"] = 8L, ["sourceType"] = "PRODUCTION", ["firstAcquiredOperationId"] = operationId, ["lastAcquiredOperationId"] = operationId
            });
            string templateId = jobId switch { "JOB_WARRIOR" => "EQ_T1_WARRIOR_WEAPON", "JOB_GUARDIAN" => "EQ_T1_GUARDIAN_WEAPON", "JOB_ARCHER" => "EQ_T1_ARCHER_WEAPON", "JOB_MAGE" => "EQ_T1_MAGE_WEAPON", _ => "EQ_T1_CLERIC_WEAPON" };
            ((JArray)store["equipment"]!).Add(new JObject
            {
                ["instanceId"] = "019f9000-0000-7000-8000-000000000802", ["equipmentTemplateId"] = templateId, ["tier"] = 1,
                ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = 0, ["refineOption"] = null, ["locked"] = false,
                ["equippedByMercenaryInstanceId"] = null, ["sourceContentVersion"] = document.Value<string>("contentVersion"),
                ["generationOperationId"] = operationId, ["stockAcquiredAtUtc"] = document.Value<string>("savedAtUtc"),
                ["stockAcquiredOperationId"] = operationId, ["sourceType"] = "PRODUCTION", ["enhancementPityBps"] = 0,
                ["enhancementAttemptCount"] = 0L, ["enhancementMaterialInvested"] = new JArray(), ["pendingRefineOption"] = null, ["refineRollCount"] = 0L
            });
            store["stockVersion"] = checked(store.Value<long>("stockVersion") + 1);
        }

        private void OpenStore()
        {
            JObject draft = game.Snapshot(); JObject store = draft["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_STORE");
            JObject merchant = draft["payload"]!["managementNpcs"]!.Children<JObject>().Single(value => value.Value<string>("professionId") == "NPC_MERCHANT");
            store["state"] = "ACTIVE"; store["level"] = 1; store["assignedNpcInstanceId"] = merchant.Value<string>("instanceId");
            merchant["assignedFacilityId"] = "FAC_STORE"; merchant["working"] = true; Commit(draft);
        }

        private void Commit(JObject draft)
        {
            SaveService saveService = services.Get<SaveService>(); SaveWriteResult result = saveService.Repository.Save(game.ActiveProfileId, draft, game.Revision, clock.UtcNow);
            Assert.That(result.Success, Is.True, result.ErrorCode + "\n" + string.Join("\n", result.Report.Issues)); game.SynchronizeCommittedDocument(result.Document);
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
