using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace KingdomTycoon.Tests.EditMode
{
    public sealed class ContentPipelineTests
    {
        [Test]
        public void Importer_LoadsCanonicalCatalogAndRewardForeignKey()
        {
            var files = CreateValidFiles();
            string manifest = CreateManifest(files);

            ContentImportResult result = new CsvContentImporter().Import(manifest, file => files[file]);

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Report.Issues));
            Assert.That(result.Catalog.ContentVersion, Is.EqualTo("1.0.0-content.1"));
            Assert.That(result.Catalog.GetTable("items.csv").Rows.Single()["item_id"], Is.EqualTo("ITEM_WOOD"));
        }

        [Test]
        public void Importer_RejectsLegacyPipeCell()
        {
            var files = CreateValidFiles();
            files["items.csv"] = files["items.csv"].Replace("Wood", "Wood|Stone");
            string manifest = CreateManifest(files);

            ContentImportResult result = new CsvContentImporter().Import(manifest, file => files[file]);

            Assert.That(result.Report.Issues.Any(issue => issue.Code == "CSV_PIPE_LIST_FORBIDDEN"), Is.True);
        }

        [Test]
        public void Importer_RejectsNonCanonicalBoolean()
        {
            var files = CreateValidFiles();
            files["items.csv"] = files["items.csv"].Replace(",TRUE\n", ",true\n");
            string manifest = CreateManifest(files);

            ContentImportResult result = new CsvContentImporter().Import(manifest, file => files[file]);

            Assert.That(result.Report.Issues.Any(issue => issue.Code == "CSV_DOMAIN_INVALID"), Is.True);
        }

        [Test]
        public void Importer_RejectsGeneratedMercenaryInRewardEntries()
        {
            var files = CreateValidFiles();
            files["reward_entries.csv"] = files["reward_entries.csv"].Replace("ITEM,ITEM_WOOD", "GENERATED_MERCENARY,ITEM_WOOD");
            string manifest = CreateManifest(files);

            ContentImportResult result = new CsvContentImporter().Import(manifest, file => files[file]);

            Assert.That(result.Report.Issues.Any(issue => issue.Code == "CSV_REWARD_DISCRIMINATOR_MISMATCH"), Is.True);
        }

        [Test]
        public void Importer_LoadsCheckedInSixtyTablePackage()
        {
            string packageDirectory = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Content", "1.0.0-content.1");
            string manifest = File.ReadAllText(Path.Combine(packageDirectory, "content_manifest.json"), new UTF8Encoding(false, true));

            ContentImportResult result = new CsvContentImporter().Import(
                manifest,
                file => File.ReadAllText(Path.Combine(packageDirectory, file), new UTF8Encoding(false, true)));

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Report.Issues));
            Assert.That(result.Catalog.Tables.Count, Is.EqualTo(60));
            Assert.That(result.Catalog.GetTable("localizations.csv").Rows.Count, Is.EqualTo(470));
        }

        [Test]
        public void Importer_RejectsOutOfOrderManifestTables()
        {
            var files = CreateValidFiles();
            JObject manifest = JObject.Parse(CreateManifest(files));
            manifest["generatedAtUtc"] = "2026-07-18T00:00:00.000Z";
            JArray tables = (JArray)manifest["tables"];
            tables.Insert(0, tables[1].DeepClone());
            tables.RemoveAt(2);

            ContentImportResult result = new CsvContentImporter().Import(manifest.ToString(Newtonsoft.Json.Formatting.None), file => files[file]);

            Assert.That(result.Report.Issues.Any(issue => issue.Code == "CONTENT_MANIFEST_TABLE_ORDER_INVALID"), Is.True, string.Join("\n", result.Report.Issues));
        }

        private static Dictionary<string, string> CreateValidFiles()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["items.csv"] = "item_id,name,status,enabled\nITEM_WOOD,Wood,CONFIRMED,TRUE\n",
                ["reward_entries.csv"] =
                    "reward_group_id,entry_no,reward_type,reward_id,quantity,probability,weight,status,enabled\n" +
                    "REWARD_TEST,1,ITEM,ITEM_WOOD,1,,,CONFIRMED,TRUE\n"
            };
        }

        private static string CreateManifest(IReadOnlyDictionary<string, string> files)
        {
            var tables = new JArray
            {
                Table(
                    "items.csv",
                    files["items.csv"],
                    new[] { "item_id" },
                    new[]
                    {
                        Field("item_id", "STABLE_ID"),
                        Field("name", "STRING"),
                        Field("status", "STATUS"),
                        Field("enabled", "BOOL")
                    }),
                Table(
                    "reward_entries.csv",
                    files["reward_entries.csv"],
                    new[] { "reward_group_id", "entry_no" },
                    new[]
                    {
                        Field("reward_group_id", "STABLE_ID"),
                        Field("entry_no", "INT32"),
                        Field("reward_type", "ENUM", false, new[]
                        {
                            "ITEM", "GENERATED_MERCENARY"
                        }),
                        Field("reward_id", "STABLE_ID"),
                        Field("quantity", "SAFE_INT"),
                        Field("probability", "DECIMAL", true),
                        Field("weight", "SAFE_INT", true),
                        Field("status", "STATUS"),
                        Field("enabled", "BOOL")
                    })
            };

            return new JObject
            {
                ["schemaId"] = "urn:tycoon:content-manifest:v2",
                ["contractVersion"] = 2,
                ["contentVersion"] = "1.0.0-content.1",
                ["csvSchemaSetVersion"] = 2,
                ["packageKind"] = "BASE",
                ["baseContentVersion"] = JValue.CreateNull(),
                ["minimumGameVersion"] = "1.0.0",
                ["channel"] = "DEV",
                ["generatedAtUtc"] = "2026-07-18T00:00:00.000Z",
                ["tables"] = tables
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static JObject Table(
            string file,
            string contents,
            IEnumerable<string> primaryKey,
            IEnumerable<JObject> fields)
        {
            return new JObject
            {
                ["file"] = file,
                ["schemaVersion"] = 1,
                ["required"] = true,
                ["sha256"] = Sha256(contents),
                ["rowCount"] = contents.Count(character => character == '\n') - 1,
                ["primaryKey"] = new JArray(primaryKey),
                ["fields"] = new JArray(fields),
                ["foreignKeys"] = new JArray()
            };
        }

        private static JObject Field(string name, string domain, bool nullable = false, IEnumerable<string> enumValues = null)
        {
            return new JObject
            {
                ["name"] = name,
                ["domain"] = domain,
                ["nullable"] = nullable,
                ["enumValues"] = enumValues == null ? new JArray() : new JArray(enumValues)
            };
        }

        private static string Sha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            return string.Concat(
                sha256.ComputeHash(new UTF8Encoding(false).GetBytes(value))
                    .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
