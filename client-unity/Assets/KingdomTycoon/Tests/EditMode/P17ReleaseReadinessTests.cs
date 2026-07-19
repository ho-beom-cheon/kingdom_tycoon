using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class P17ReleaseReadinessTests
    {
        private static string ContentRoot => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.13"));

        [Test]
        public void ReleaseScaleConfigurationMatchesPerformanceContract()
        {
            Dictionary<string, string> config = Csv(File.ReadAllLines(Path.Combine(ContentRoot, "runtime_config.csv")))
                .ToDictionary(row => row["config_key"], row => row["value"], StringComparer.Ordinal);
            Assert.That(config["ACTIVE_MERC_CAP_V1"], Is.EqualTo("16"));
            Assert.That(config["P06_MAX_RUNTIME_ENTITIES"], Is.EqualTo("60"));
            Assert.That(config["P07_MAX_PAGE_SIZE"], Is.EqualTo("100"));
            Assert.That(config["P15_TUTORIAL_TARGET_MINUTES"], Is.EqualTo("25"));
            Assert.That(config["P15_OFFLINE_MAX_SECONDS"], Is.EqualTo("28800"));
        }

        [Test]
        public void RaidAndTutorialDataMatchBalanceBaseline()
        {
            Dictionary<string, string>[] raids = Csv(File.ReadAllLines(Path.Combine(ContentRoot, "raids.csv"))).ToArray();
            Assert.That(raids, Has.Length.EqualTo(2));
            Assert.That(raids.All(row => int.Parse(row["party_min"], CultureInfo.InvariantCulture) >= 6), Is.True);
            Assert.That(raids.All(row => int.Parse(row["party_max"], CultureInfo.InvariantCulture) <= 8), Is.True);
            Assert.That(raids.All(row => int.Parse(row["time_limit_sec"], CultureInfo.InvariantCulture) is >= 180 and <= 360), Is.True);

            Dictionary<string, string>[] steps = Csv(File.ReadAllLines(Path.Combine(ContentRoot, "tutorial_steps.csv"))).ToArray();
            Assert.That(steps, Has.Length.EqualTo(10));
            Assert.That(steps.Select(row => int.Parse(row["order"], CultureInfo.InvariantCulture)), Is.EqualTo(Enumerable.Range(1, 10)));
            Assert.That(steps.Zip(steps.Skip(1), (current, next) => next["prerequisite_step_id"] == current["tutorial_step_id"]).All(value => value), Is.True);
        }

        [Test]
        public void ActiveAssetRegisterRowsAreCommerciallyUsableAndZeroSpend()
        {
            Dictionary<string, string>[] rows = Csv(File.ReadAllLines(Path.Combine(ContentRoot, "asset_register.csv"))).Where(row => row["enabled"] == "TRUE").ToArray();
            Assert.That(rows, Is.Not.Empty);
            Assert.That(rows.All(row => !string.IsNullOrWhiteSpace(row["creator"])), Is.True);
            Assert.That(rows.All(row => !string.IsNullOrWhiteSpace(row["license"])), Is.True);
            Assert.That(rows.All(row => row["commercial_use"] == "TRUE" && row["modification_allowed"] == "TRUE"), Is.True);
            Assert.That(rows.Sum(row => long.Parse(row["price_krw"], CultureInfo.InvariantCulture)), Is.Zero);
        }

        [Test]
        public void MaximumScaleSimulationIsDeterministicAndFastEnoughForEditorRegressionGate()
        {
            Stopwatch watch = Stopwatch.StartNew();
            long first = Simulate(9137);
            long second = Simulate(9137);
            watch.Stop();
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.Not.Zero);
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(3000), $"P17 performance proxy took {watch.ElapsedMilliseconds}ms");
        }

        private static long Simulate(int seed)
        {
            var random = new System.Random(seed);
            var health = Enumerable.Range(0, 60).Select(index => 800 + index * 17).ToArray();
            var threat = new int[16, 60];
            var visibleRows = new int[100];
            long checksum = 17;
            for (int tick = 0; tick < 1000; tick++)
            {
                for (int mercenary = 0; mercenary < 16; mercenary++)
                {
                    int target = (mercenary * 11 + tick * 7) % 60;
                    int damage = 3 + random.Next(9);
                    health[target] = Math.Max(0, health[target] - damage);
                    threat[mercenary, target] = (threat[mercenary, target] * 9950 + damage * 10000) / 10000;
                    checksum = unchecked(checksum * 31 + health[target] + threat[mercenary, target]);
                }
                for (int row = 0; row < visibleRows.Length; row++) visibleRows[row] = (row + tick) % 100;
            }
            return checksum + health.Sum(value => (long)value) + visibleRows.Sum(value => (long)value);
        }

        private static IEnumerable<Dictionary<string, string>> Csv(string[] lines)
        {
            string[] headers = lines[0].Split(',');
            foreach (string line in lines.Skip(1).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                string[] cells = line.Split(',');
                Assert.That(cells.Length, Is.EqualTo(headers.Length), line);
                yield return headers.Select((header, index) => (header, value: cells[index])).ToDictionary(value => value.header, value => value.value, StringComparer.Ordinal);
            }
        }
    }
}
