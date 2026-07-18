using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KingdomTycoon.Infrastructure.Content;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

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

            Assert.That(result.Report.Issues.Any(issue => issue.Code == "CSV_LEGACY_COMPOUND_CELL_FORBIDDEN"), Is.True);
        }

        [Test]
        public void Importer_RejectsNonCanonicalBoolean()
        {
            var files = CreateValidFiles();
            files["items.csv"] = files["items.csv"].Replace(",TRUE\n", ",true\n");
            string manifest = CreateManifest(files);

            ContentImportResult result = new CsvContentImporter().Import(manifest, file => files[file]);

            Assert.That(result.Report.Issues.Any(issue => issue.Code == "CSV_FIELD_DOMAIN_INVALID"), Is.True);
        }

        [Test]
        public void Importer_RejectsGeneratedMercenaryInRewardEntries()
        {
            var files = CreateValidFiles();
            files["reward_entries.csv"] = files["reward_entries.csv"].Replace("ITEM,ITEM_WOOD", "GENERATED_MERCENARY,ITEM_WOOD");
            string manifest = CreateManifest(files);

            ContentImportResult result = new CsvContentImporter().Import(manifest, file => files[file]);

            Assert.That(result.Report.Issues.Any(issue => issue.Code == "CSV_REWARD_GENERATED_MERCENARY_FORBIDDEN"), Is.True);
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
                ["schemaId"] = "urn:tycoon:content-manifest:v1",
                ["contractVersion"] = 1,
                ["contentVersion"] = "1.0.0-content.1",
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
                ["enumValues"] = enumValues == null ? JValue.CreateNull() : new JArray(enumValues)
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
