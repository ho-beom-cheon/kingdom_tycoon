using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Inventory;
using KingdomTycoon.Domain.Combat;
using KingdomTycoon.Domain.Inventory;
using KingdomTycoon.Infrastructure;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Content.Migrations;
using KingdomTycoon.Infrastructure.Inventory;
using KingdomTycoon.Infrastructure.Save;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using CombatSplitMix64 = KingdomTycoon.Domain.Combat.SplitMix64;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P07InventoryTests
    {
        private const string Version = "1.0.0-content.5";

        [Test]
        public void ContentGoldenImportsSixtyEightTables()
        {
            string root = ContentRoot();
            JObject manifest = StrictJson.ParseObject(File.ReadAllText(Path.Combine(root, "content_manifest.json")));
            Assert.That(Rfc8785Canonicalizer.ComputeSha256(manifest), Is.EqualTo("692922c62883a7da9520b7d9bc586b451f5b94b7461e3909dbca23e1cfcecef4"));
            Assert.That(Sha256(File.ReadAllBytes(Path.Combine(root, "localizations.csv"))), Is.EqualTo("a0a784d762d2fda6710480050843f6cbfa539c27cf67801f7b3296db8919db10"));
            ContentImportResult imported = new CsvContentImporter().Import(manifest.ToString(), file => File.ReadAllText(Path.Combine(root, file)));
            Assert.That(imported.IsValid, Is.True, string.Join("\n", imported.Report.Issues));
            Assert.That(imported.Catalog.Tables.Count, Is.EqualTo(68));
            Assert.That(imported.Catalog.GetTable("inventory_capacity_rules.csv").Rows.Count, Is.EqualTo(4));
            Assert.That(imported.Catalog.GetTable("equipment_score_weights.csv").Rows.Count, Is.EqualTo(5));
        }

        [Test]
        public void ConditionalSaveSchemasValidateP06AndP07Goldens()
        {
            var validator = NewValidator();
            SaveValidationResult before = validator.ParseAndValidate(ReadContract("p07-migration-before.golden.json"), "P06_BEFORE");
            SaveValidationResult after = validator.ParseAndValidate(ReadContract("p07-migration-after.golden.json"), "P07_AFTER");
            Assert.That(before.IsValid, Is.True, string.Join("\n", before.Report.Issues));
            Assert.That(after.IsValid, Is.True, string.Join("\n", after.Report.Issues));
            JObject mixed = (JObject)before.Document.DeepClone();
            mixed["payload"]!["kingdom"]!["inventoryPolicies"]!["autoEquipEnabled"] = true;
            Assert.That(validator.Validate(mixed, "MIXED").IsValid, Is.False);
        }

        [Test]
        public void P06ToP07MigrationMatchesFullGolden()
        {
            DateTimeOffset now = new(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);
            JObject source = StrictJson.ParseObject(ReadContract("p07-migration-before.golden.json"));
            JObject expected = StrictJson.ParseObject(ReadContract("p07-migration-after.golden.json"));
            var migration = new P06ToP07ContentMigration(new FixedClock(now));
            JObject migrated = migration.Apply(source);
            JObject committed = NewValidator().PrepareForCommit(migrated, 2, now);
            Assert.That(JToken.DeepEquals(committed, expected), Is.True, committed.ToString());
            Assert.That(migration.CanApply(committed), Is.False);
        }

        [Test]
        public void LootRngMatchesTraceAndIndependentEntryRules()
        {
            var random = new CombatSplitMix64(3933518812831075410UL);
            ulong[] expected = { 11373512251824306030UL, 6514924633081954734UL, 17447235620859875722UL, 9487372518197105740UL, 5267149625346456796UL, 18430482160183153902UL, 13872711261042295418UL };
            Assert.That(expected.Select(_ => random.Next()).ToArray(), Is.EqualTo(expected));

            var lines = LootDeterminism.Resolve(new[]
            {
                new LootEntry(1, "ITEM", "MAT_R01_SOFTWOOD", 750000, 1, 2),
                new LootEntry(2, "ITEM", "MAT_R01_SLIME_GEL", 220000, 1, 1),
                new LootEntry(3, "RANDOM_EQUIPMENT_TIER", "RANDOM_EQ_T1_ANY", 25000, 1, 1)
            }, new CombatSplitMix64(3933518812831075410UL));
            Assert.That(lines.Count, Is.EqualTo(1));
            Assert.That((lines[0].RewardId, lines[0].Quantity), Is.EqualTo(("MAT_R01_SOFTWOOD", 1)));
        }

        [Test]
        public void EquipmentFlatModifiersMatchFiveJobWeaponGoldens()
        {
            var fixtures = new[]
            {
                ("JOB_WARRIOR", "SWORD", 100, 100, 0, 0, 0),
                ("JOB_GUARDIAN", "HAMMER_SHIELD", 100, 60, 40, 0, 0),
                ("JOB_ARCHER", "BOW", 100, 95, 0, 0, 500),
                ("JOB_MAGE", "STAFF", 100, 90, 0, 0, 0),
                ("JOB_CLERIC", "MACE", 100, 65, 0, 55, 0)
            };
            foreach ((string job, string profile, int power, int attack, int defense, int heal, int crit) in fixtures)
            {
                var definition = new EquipmentDefinition("EQ", 1, "WEAPON", profile, power, "CRAFT");
                var item = new EquipmentInstance("ID", definition, "QUALITY_COMMON", 10000, 0, null, false);
                EquipmentStatBlock stats = EquipmentMath.FlatModifier(job, item);
                Assert.That((stats.Attack, stats.Defense, stats.HealPower, stats.CritChance), Is.EqualTo((attack, defense, heal, crit)), job);
            }
        }

        [Test]
        public void EquipmentEnhancementEligibilityAndCapacityRejectAtomically()
        {
            var definition = new EquipmentDefinition("EQ", 1, "WEAPON", "SWORD", 100, "CRAFT");
            Assert.That(EquipmentMath.EffectivePower(new EquipmentInstance("0", definition, "QUALITY_RELIC", 15000, 10, null, false)), Is.EqualTo(225));
            Assert.That(Assert.Throws<InventoryDomainException>(() => new EquipmentInstance("-1", definition, "QUALITY_COMMON", 10000, -1, null, false)).ErrorCode, Is.EqualTo("P07_ENHANCEMENT_LEVEL_INVALID"));
            Assert.That(Assert.Throws<InventoryDomainException>(() => EquipmentMath.FlatModifier("JOB_MAGE", new EquipmentInstance("x", definition, "QUALITY_COMMON", 10000, 0, null, false))).ErrorCode, Is.EqualTo("P07_EQUIPMENT_JOB_INELIGIBLE"));
            var capacity = new InventoryCapacity(2, 1, 1, 4); capacity.Require(2, 1, 1, 4);
            Assert.That(Assert.Throws<InventoryDomainException>(() => capacity.Require(3, 1, 1, 4)).ErrorCode, Is.EqualTo("P07_CAPACITY_EXCEEDED"));
            Assert.That(Assert.Throws<InventoryDomainException>(() => capacity.Require(2, 1, 1, 5)).ErrorCode, Is.EqualTo("P07_HUNT_BUFFER_FULL"));
        }

        [Test]
        public void CommandHashesMatchExecutableGoldens()
        {
            var hasher = new InventoryRequestHasher();
            var transfer = new PotionTransferCommand("TRANSFER_POTION_TO_MERCENARY", Guid.Parse("019f8373-7c00-7000-8000-000000000203"), 6, null,
                "019f7cd2-8800-7002-8000-000000000001", "POT_HEAL_SMALL", 2);
            Assert.That(hasher.Compute(transfer), Is.EqualTo("8aeedfb54c270940d8e85d3659d38f02c0024a91ec5e20660c234f048cfbbf6e"));
            var sell = new SellInventoryCommand(Guid.Parse("019f8373-7c00-7000-8000-000000000201"), 4, null,
                "019f7cd2-8800-7002-8000-000000000001", "ITEM", "MAT_R01_SOFTWOOD", 2);
            Assert.That(hasher.Compute(sell), Is.EqualTo("1ec1e420c0a49c3b87b2106fbc668572ad1fe00bdecf4f5ef4ba7e1eb66fa88c"));
        }

        [Test]
        public void ThirtyMinuteIntegratedLoopIsDeterministicAndWritesOnlyAtTerminal()
        {
            RunIntegratedSoak(1_000);
            long memoryBefore = GC.GetTotalMemory(true);
            Stopwatch stopwatch = Stopwatch.StartNew();
            JObject first = RunIntegratedSoak(18_000);
            JObject second = RunIntegratedSoak(18_000);
            stopwatch.Stop();
            long memoryGrowth = GC.GetTotalMemory(true) - memoryBefore;

            Assert.That(JToken.DeepEquals(second, first), Is.True, first.ToString());
            Assert.That(first.Value<int>("ticks"), Is.EqualTo(18_000));
            Assert.That(first.Value<int>("encounterPersistentWrites"), Is.Zero);
            Assert.That(first.Value<int>("settlementJournals"), Is.EqualTo(first.Value<int>("hunts")));
            Assert.That(first.Value<int>("settlementWrites"), Is.EqualTo(first.Value<int>("hunts")));
            Assert.That(first.Value<int>("lootLines"), Is.GreaterThan(0));
            Assert.That(memoryGrowth, Is.LessThanOrEqualTo(8L * 1024L * 1024L));
            Assert.That(stopwatch.Elapsed.TotalSeconds, Is.LessThan(5));
        }

        [Test]
        public void HundredRowInventoryQueryMeetsEditorReferenceBudget()
        {
            var definition = new EquipmentDefinition("EQ_PERF", 1, "WEAPON", "SWORD", 100, "CRAFT");
            EquipmentInstance[] inventory = Enumerable.Range(0, 100)
                .Select(index => new EquipmentInstance(index.ToString("D3"), definition, "QUALITY_COMMON", 10_000, index % 11, null, false))
                .ToArray();
            Func<EquipmentInstance[]> query = () => inventory
                .OrderByDescending(EquipmentMath.EffectivePower)
                .ThenBy(value => value.InstanceId, StringComparer.Ordinal)
                .Take(30)
                .ToArray();
            query();

            var milliseconds = new double[40];
            long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < milliseconds.Length; index++)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Assert.That(query().Length, Is.EqualTo(30));
                stopwatch.Stop();
                milliseconds[index] = stopwatch.Elapsed.TotalMilliseconds;
            }
            long allocationPerRefresh = (GC.GetAllocatedBytesForCurrentThread() - allocationBefore) / milliseconds.Length;
            Array.Sort(milliseconds);
            double p95 = milliseconds[(int)Math.Ceiling(milliseconds.Length * .95) - 1];
            Assert.That(p95, Is.LessThanOrEqualTo(3.0), $"p95={p95:F4}ms");
            Assert.That(allocationPerRefresh, Is.LessThanOrEqualTo(96L * 1024L), $"allocation={allocationPerRefresh} bytes");
        }

        private static JObject RunIntegratedSoak(int ticks)
        {
            var entries = new[]
            {
                new LootEntry(1, "ITEM", "MAT_R01_SOFTWOOD", 750000, 1, 2),
                new LootEntry(2, "ITEM", "MAT_R01_SLIME_GEL", 220000, 1, 1),
                new LootEntry(3, "RANDOM_EQUIPMENT_TIER", "RANDOM_EQ_T1_ANY", 25000, 1, 1)
            };
            Guid huntId = Guid.Parse("019f8373-7c00-7000-8000-00000000f007");
            int encounters = 0, hunts = 0, lootLines = 0, lootQuantity = 0, equipmentDrops = 0;
            int settlementJournals = 0, settlementWrites = 0, encounterPersistentWrites = 0;
            HuntSimulation current = NewSoakEncounter(encounters);
            for (int tick = 0; tick < ticks; tick++)
            {
                current.Step();
                if (!current.IsComplete) continue;
                IReadOnlyList<LootLine> loot = LootDeterminism.Resolve(entries,
                    new CombatSplitMix64(LootDeterminism.Seed(Version, huntId, encounters)));
                lootLines += loot.Count;
                lootQuantity += loot.Sum(value => value.Quantity);
                equipmentDrops += loot.Count(value => value.RewardType == "RANDOM_EQUIPMENT_TIER");
                encounters++;
                if (encounters % 12 == 0)
                {
                    hunts++;
                    settlementJournals++;
                    settlementWrites++;
                }
                current = NewSoakEncounter(encounters);
            }
            return new JObject
            {
                ["fixture"] = "P07_INTEGRATED_SOAK_30M",
                ["ticks"] = ticks,
                ["encounters"] = encounters,
                ["hunts"] = hunts,
                ["lootLines"] = lootLines,
                ["lootQuantity"] = lootQuantity,
                ["equipmentDrops"] = equipmentDrops,
                ["settlementJournals"] = settlementJournals,
                ["settlementWrites"] = settlementWrites,
                ["encounterPersistentWrites"] = encounterPersistentWrites
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

        private static SaveDocumentValidator NewValidator() => new(ReadContract("save.schema.json"), ReadContract("save.content.5.schema.json"));
        private static string ReadContract(string name) => File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "KingdomTycoon", "Resources", "Contracts", name));
        private static string ContentRoot() => Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", Version);
        private static string Sha256(byte[] bytes) { using SHA256 sha = SHA256.Create(); return string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
        private sealed class FixedClock : ITrustedUtcClock
        {
            public FixedClock(DateTimeOffset value) => UtcNow = value;
            public DateTimeOffset UtcNow { get; }
            public bool IsTrusted => true;
            public long MonotonicTicks => 0;
        }
    }
}
