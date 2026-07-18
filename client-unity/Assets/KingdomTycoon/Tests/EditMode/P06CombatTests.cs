using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Combat;
using KingdomTycoon.Domain.Combat;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Combat;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Content.Migrations;
using KingdomTycoon.Infrastructure.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P06CombatTests
    {
        private const string ContentVersion = "1.0.0-content.4";

        [Test]
        public void ContentGolden_ImportsAllSixtySixTablesWithExactDigests()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", ContentVersion);
            JObject manifest = StrictJson.ParseObject(File.ReadAllText(Path.Combine(root, "content_manifest.json")));
            Assert.That(Rfc8785Canonicalizer.ComputeSha256(manifest), Is.EqualTo("d541f924b230eb9b21e1e2a46fe9c5f30d72d75eddacef8cb2c05bd620151fb5"));
            Assert.That(Sha256(File.ReadAllBytes(Path.Combine(root, "localizations.csv"))), Is.EqualTo("215286bfe6b80a2ed19dc794af1bdc4d6d31e57bbcfc84a8e7b1be760dd47442"));

            ContentImportResult imported = new CsvContentImporter().Import(manifest.ToString(), file => File.ReadAllText(Path.Combine(root, file)));
            Assert.That(imported.IsValid, Is.True, string.Join("\n", imported.Report.Issues));
            Assert.That(imported.Catalog.Tables.Count, Is.EqualTo(66));
            Assert.That(imported.Catalog.GetTable("autonomy_rules.csv").Rows.Count, Is.EqualTo(37));
            Assert.That(imported.Catalog.GetTable("skill_runtime_rules.csv").Rows.Count, Is.EqualTo(15));
        }

        [Test]
        public void AStarTie_UsesContractPathMetricsAndCacheKey()
        {
            var blocked = new HashSet<GridPoint> { new(2, 1) };
            var search = new DeterministicAStar();
            PathSearchResult first = search.FindPath(5, 3, blocked, new GridPoint(0, 1), new GridPoint(4, 1), "grid", 7);
            Assert.That(first.Path.Select(value => value.ToString()), Is.EqualTo(new[] { "(0,1)", "(0,2)", "(1,2)", "(2,2)", "(3,2)", "(4,2)", "(4,1)" }));
            Assert.That(first.Cost, Is.EqualTo(60));
            Assert.That(first.ExpandedNodes, Is.EqualTo(7));
            Assert.That(first.Insertions, Is.EqualTo(10));
            Assert.That(first.CacheHit, Is.False);

            PathSearchResult cached = search.FindPath(5, 3, blocked, new GridPoint(0, 1), new GridPoint(4, 1), "grid", 7);
            Assert.That(cached.CacheHit, Is.True);
            Assert.That(cached.ExpandedNodes, Is.Zero);
            Assert.That(cached.Path, Is.EqualTo(first.Path));
            Assert.That(search.FindPath(5, 3, blocked, new GridPoint(0, 1), new GridPoint(4, 1), "grid", 8).CacheHit, Is.False);
            Assert.That(search.FindPath(5, 3, blocked, new GridPoint(1, 1), new GridPoint(1, 1), "grid", 7).Cost, Is.Zero);

            var wall = new HashSet<GridPoint> { new(2, 0), new(2, 1), new(2, 2) };
            Assert.That(search.FindPath(5, 3, wall, new GridPoint(0, 1), new GridPoint(4, 1), "wall", 1).Reachable, Is.False);
        }

        [Test]
        public void FixedClockMovementRangeAndGrowthMatchGoldens()
        {
            var clock = new FixedStepClock();
            var ticks = new List<long>();
            Assert.That(clock.Advance(800, ticks.Add), Is.EqualTo(5));
            Assert.That(clock.Tick, Is.EqualTo(5));
            Assert.That(clock.DroppedTicks, Is.EqualTo(3));

            var mover = new FixedPointMover(500, 500);
            for (int index = 0; index < 3; index++) mover.MoveCardinal(1, 0, 3600);
            Assert.That((mover.X, mover.Y), Is.EqualTo((1580, 500)));
            for (int index = 3; index < 10; index++) mover.MoveCardinal(1, 0, 3600);
            Assert.That(mover.X, Is.EqualTo(4100));
            Assert.That(CombatMath.InRange(0, 0, 1500, 0, 1500), Is.True);
            Assert.That(CombatMath.InRange(0, 0, 1501, 0, 1500), Is.False);
            Assert.That(CombatDeterminism.GrowthFactorBps(ContentVersion, 3001, "ATTACK"), Is.EqualTo(10490));
        }

        [Test]
        public void RequestHashesMatchExecutableGoldens()
        {
            Guid operationId = Guid.Parse("019f8320-1800-7000-8000-000000000001");
            Guid huntId = Guid.Parse("019f8320-1800-7000-8000-000000000002");
            Guid[] party = Enumerable.Range(1, 4).Select(index => Guid.Parse($"019f7cd2-8800-7002-8000-{index:000000000000}")).ToArray();
            var hasher = new CombatRequestHasher();
            Assert.That(hasher.ComputeHash(new StartHuntCommand(operationId, huntId, 2, null, "REGION_R01", party)), Is.EqualTo("97f7c3d1c799948c48cbd00ffe54188ed4b7084c3544a13886620deb2a94faa1"));
            Assert.That(hasher.ComputeHash(new RecallHuntCommand(huntId, 3, null)), Is.EqualTo("027b3f13dd4dc316455ae6b668d5f8cbcf6d4805f472b16d3303eb7bebb4e920"));
        }

        [Test]
        public void AutonomyRulesHonorSafetyPriorityAndDisabledP07Rows()
        {
            CanonicalCombatCatalog catalog = LoadCatalog();
            var engine = new AutonomyRuleEngine(catalog.AutonomyRules);
            Assert.That(engine.Evaluate("IDLE_TOWN", new AutonomyContext { HasInjury = true, PromotionAvailable = true }).State, Is.EqualTo("INJURED"));
            Assert.That(engine.Evaluate("PREPARE", new AutonomyContext { PotionCount = 0 }).State, Is.EqualTo("TRAVEL_TO_REGION"));
            Assert.That(engine.Evaluate("COMBAT", new AutonomyContext { HpRatioBps = 10000 }).State, Is.EqualTo("COMBAT"));
            Assert.That(catalog.AutonomyRules.Count, Is.EqualTo(37));
            Assert.That(catalog.AutonomyRules.Count(value => !value.Enabled), Is.EqualTo(2));
        }

        [Test]
        public void SimulationIsDeterministicAndPoolLimitIsStrict()
        {
            string first = RunFixture();
            string second = RunFixture();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.EqualTo("LOOT_COMPLETE|2|13|210|55"));

            var pool = new HuntSimulation();
            for (int index = 0; index < 60; index++)
                pool.Add(new Combatant(index.ToString("D3"), CombatTeam.Party, 1, 1, 0, 0, 1000, index, 0));
            CombatDomainException error = Assert.Throws<CombatDomainException>(() => pool.Add(new Combatant("overflow", CombatTeam.Party, 1, 1, 0, 0, 1000, 61, 0)));
            Assert.That(error.ErrorCode, Is.EqualTo("P06_ENTITY_POOL_EXHAUSTED"));
        }

        [Test]
        public void OutOfRangeCombatantsMoveAtTenHertzAndEventuallyResolve()
        {
            var simulation = new HuntSimulation();
            var party = new Combatant("P:001", CombatTeam.Party, 200, 80, 20, 1000, 1000, 0, 0, 3600);
            var hostile = new Combatant("M:001", CombatTeam.Hostile, 100, 25, 10, 1000, 800, 5000, 0, 3000);
            simulation.Add(party); simulation.Add(hostile);
            simulation.Step();
            Assert.That(party.X, Is.EqualTo(360));
            Assert.That(hostile.X, Is.EqualTo(4700));
            while (!simulation.IsComplete && simulation.Tick < 100) simulation.Step();
            Assert.That(simulation.IsComplete, Is.True);
            Assert.That(simulation.TerminalReason, Is.EqualTo("LOOT_COMPLETE"));
        }

        [Test]
        public void P05ToP06MigrationPreservesStablePayloadAndNormalizesTransientState()
        {
            var clock = new FixedClock(new DateTimeOffset(2026, 7, 20, 0, 0, 0, TimeSpan.Zero));
            JObject source = StrictJson.ParseObject(ReadAsset("KingdomTycoon/Resources/Contracts/p05-migration-after.golden.json"));
            JObject original = (JObject)source.DeepClone();
            var migration = new P05ToP06ContentMigration(clock);
            JObject migrated = migration.Apply(source);
            Assert.That(migrated.Value<string>("contentVersion"), Is.EqualTo(ContentVersion));
            Assert.That(JToken.DeepEquals(migrated["payload"], source["payload"]), Is.True);
            Assert.That(JToken.DeepEquals(source, original), Is.True);
            Assert.That(migration.CanApply(migrated), Is.False);

            JObject autonomy = (JObject)migrated["payload"]!["mercenaries"]![0]!["autonomy"]!;
            autonomy["state"] = "COMBAT";
            autonomy["reasonCode"] = "TARGET_FOUND";
            autonomy["currentRegionId"] = "REGION_R01";
            Assert.That(P05ToP06ContentMigration.NormalizeTransientAutonomy(migrated, clock.UtcNow), Is.True);
            Assert.That(autonomy.Value<string>("state"), Is.EqualTo("IDLE_TOWN"));
            Assert.That(autonomy.Value<string>("reasonCode"), Is.EqualTo("NONE"));
            Assert.That(autonomy["currentRegionId"].Type, Is.EqualTo(JTokenType.Null));
        }

        [Test]
        public void ThirtyMinuteSoakMatchesCommittedBuildDerivedDigest()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            JObject metrics = RunSoak(18000);
            stopwatch.Stop();
            string digest = Rfc8785Canonicalizer.ComputeSha256(metrics);
            string goldenPath = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath)!.Parent!.FullName, "docs", "goldens", "P06", "soak_30m.digest.json");
            JObject golden = StrictJson.ParseObject(File.ReadAllText(goldenPath));
            Assert.That(golden.Value<string>("sha256"), Is.EqualTo(digest));
            Assert.That(JToken.DeepEquals(golden["metrics"], metrics), Is.True, metrics.ToString());
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(5));
        }

        private static CanonicalCombatCatalog LoadCatalog()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", ContentVersion);
            JObject manifest = StrictJson.ParseObject(File.ReadAllText(Path.Combine(root, "content_manifest.json")));
            ContentImportResult imported = new CsvContentImporter().Import(manifest.ToString(), file => File.ReadAllText(Path.Combine(root, file)));
            Assert.That(imported.IsValid, Is.True, string.Join("\n", imported.Report.Issues));
            return new CanonicalCombatCatalog(imported.Catalog);
        }

        private static string RunFixture()
        {
            var value = new HuntSimulation();
            value.Add(new Combatant("P:001", CombatTeam.Party, 180, 70, 20, 2000, 1000, 0, 0));
            value.Add(new Combatant("P:002", CombatTeam.Party, 160, 65, 15, 2000, 900, 0, 500));
            value.Add(new Combatant("M:001", CombatTeam.Hostile, 100, 35, 10, 2000, 800, 1000, 0));
            value.Add(new Combatant("M:002", CombatTeam.Hostile, 110, 30, 15, 2000, 700, 1000, 500));
            while (!value.IsComplete && value.Tick < 100) value.Step();
            long partyDamage = value.Entities.Where(item => item.Team == CombatTeam.Party).Sum(item => item.DamageDealt);
            long hostileDamage = value.Entities.Where(item => item.Team == CombatTeam.Hostile).Sum(item => item.DamageDealt);
            return $"{value.TerminalReason}|{value.KillCount}|{value.Tick}|{partyDamage}|{hostileDamage}";
        }

        private static JObject RunSoak(int ticks)
        {
            int encounters = 0;
            int kills = 0;
            long partyDamage = 0;
            long hostileDamage = 0;
            HuntSimulation current = NewSoakEncounter(encounters);
            for (int tick = 0; tick < ticks; tick++)
            {
                current.Step();
                if (!current.IsComplete) continue;
                encounters++;
                kills += current.KillCount;
                partyDamage += current.Entities.Where(value => value.Team == CombatTeam.Party).Sum(value => value.DamageDealt);
                hostileDamage += current.Entities.Where(value => value.Team == CombatTeam.Hostile).Sum(value => value.DamageDealt);
                current = NewSoakEncounter(encounters);
            }
            return new JObject
            {
                ["fixture"] = "SOAK_30M",
                ["ticks"] = ticks,
                ["encounters"] = encounters,
                ["kills"] = kills,
                ["partyDamage"] = partyDamage,
                ["hostileDamage"] = hostileDamage,
                ["activeEntityCount"] = current.Entities.Count(value => !value.IsDown)
            };
        }

        private static HuntSimulation NewSoakEncounter(int index)
        {
            var value = new HuntSimulation();
            value.Add(new Combatant($"{index:000000}:P:001", CombatTeam.Party, 1000, 100, 40, 2000, 1000, 0, 0));
            value.Add(new Combatant($"{index:000000}:P:002", CombatTeam.Party, 900, 90, 35, 2000, 900, 0, 500));
            value.Add(new Combatant($"{index:000000}:M:001", CombatTeam.Hostile, 300, 45, 25, 2000, 800, 1000, 0));
            value.Add(new Combatant($"{index:000000}:M:002", CombatTeam.Hostile, 320, 40, 30, 2000, 750, 1000, 500));
            return value;
        }

        private static string ReadAsset(string path) => File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, path));
        private static string Sha256(byte[] bytes)
        {
            using SHA256 sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private sealed class FixedClock : ITrustedUtcClock
        {
            public FixedClock(DateTimeOffset value) => UtcNow = value;
            public DateTimeOffset UtcNow { get; }
            public bool IsTrusted => true;
            public long MonotonicTicks => 0;
        }
    }
}
