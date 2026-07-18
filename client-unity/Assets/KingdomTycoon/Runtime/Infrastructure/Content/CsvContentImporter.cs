using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using KingdomTycoon.Infrastructure;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Content
{
    public sealed class ContentImportResult
    {
        public ContentImportResult(ContentCatalog catalog, ValidationReport report)
        {
            Catalog = catalog;
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public ContentCatalog Catalog { get; }

        public ValidationReport Report { get; }

        public bool IsValid => Catalog != null && Report.IsValid;
    }

    public sealed class ContentCatalog
    {
        private readonly IReadOnlyDictionary<string, ContentTable> tables;

        internal ContentCatalog(string contentVersion, IDictionary<string, ContentTable> tables)
        {
            ContentVersion = contentVersion;
            this.tables = new ReadOnlyDictionary<string, ContentTable>(
                new Dictionary<string, ContentTable>(tables, StringComparer.Ordinal));
        }

        public string ContentVersion { get; }

        public IReadOnlyDictionary<string, ContentTable> Tables => tables;

        public ContentTable GetTable(string fileName)
        {
            return tables.TryGetValue(fileName, out ContentTable table)
                ? table
                : throw new KeyNotFoundException($"Content table {fileName} is not loaded.");
        }
    }

    public sealed class ContentTable
    {
        private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> rows;

        internal ContentTable(TableContract contract, IList<IReadOnlyDictionary<string, string>> rows)
        {
            Contract = contract;
            this.rows = new ReadOnlyCollection<IReadOnlyDictionary<string, string>>(rows);
        }

        internal TableContract Contract { get; }

        public string FileName => Contract.FileName;

        public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows => rows;
    }

    public sealed class CsvContentImporter
    {
        private const long SafeIntegerMaximum = 9_007_199_254_740_991L;
        private static readonly Regex HeaderPattern = new("^[a-z][a-z0-9]*(?:_[a-z0-9]+)*$", RegexOptions.CultureInvariant);
        private static readonly Regex StableIdPattern = new("^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant);
        private static readonly Regex IntegerPattern = new("^(0|-?[1-9][0-9]*)$", RegexOptions.CultureInvariant);
        private static readonly Regex DecimalPattern = new("^(0|-?[1-9][0-9]*)(?:\\.[0-9]*[1-9])?$", RegexOptions.CultureInvariant);
        private static readonly Regex HashPattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);

        public ContentImportResult Import(string manifestJson, Func<string, string> readTable)
        {
            if (readTable == null)
            {
                throw new ArgumentNullException(nameof(readTable));
            }

            var report = new ValidationReport();
            ContentManifest manifest;
            try
            {
                manifest = ParseManifest(manifestJson);
            }
            catch (Exception exception)
            {
                report.AddError("CSV_MANIFEST_INVALID", "content_manifest.json", "/", exception.Message);
                return new ContentImportResult(null, report);
            }

            var tables = new Dictionary<string, ContentTable>(StringComparer.Ordinal);
            foreach (TableContract contract in manifest.Tables.OrderBy(table => table.FileName, StringComparer.Ordinal))
            {
                string text;
                try
                {
                    text = readTable(contract.FileName);
                }
                catch (Exception exception)
                {
                    report.AddError("CSV_FILE_READ_FAILED", contract.FileName, "/", exception.Message);
                    continue;
                }

                if (text == null)
                {
                    report.AddError("CSV_FILE_MISSING", contract.FileName, "/", "Manifest table is missing.");
                    continue;
                }

                ValidateHash(text, contract, report);
                ContentTable table = ParseTable(text, contract, report);
                if (table != null)
                {
                    tables.Add(contract.FileName, table);
                }
            }

            ValidateForeignKeys(tables, report);
            ValidateTaggedUnions(tables, report);
            return report.IsValid
                ? new ContentImportResult(new ContentCatalog(manifest.ContentVersion, tables), report)
                : new ContentImportResult(null, report);
        }

        private static ContentManifest ParseManifest(string json)
        {
            JObject root = StrictJson.ParseObject(json);
            RequireExactProperties(root, "schemaId", "contractVersion", "contentVersion", "generatedAtUtc", "tables");
            if (root.Value<string>("schemaId") != "urn:tycoon:content-manifest:v1" || root.Value<int>("contractVersion") != 1)
            {
                throw new FormatException("Unsupported content manifest contract.");
            }

            string contentVersion = root.Value<string>("contentVersion");
            if (string.IsNullOrWhiteSpace(contentVersion))
            {
                throw new FormatException("contentVersion is required.");
            }

            if (!DateTimeOffset.TryParseExact(
                    root.Value<string>("generatedAtUtc"),
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out _))
            {
                throw new FormatException("generatedAtUtc must be a canonical UTC instant.");
            }

            var tables = new List<TableContract>();
            var files = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject table in root["tables"].Children<JObject>())
            {
                RequireExactProperties(table, "file", "sha256", "rowCount", "primaryKey", "fields", "foreignKeys");
                string fileName = table.Value<string>("file");
                if (string.IsNullOrEmpty(fileName) || fileName.Contains('/') || fileName.Contains('\\') || !fileName.EndsWith(".csv", StringComparison.Ordinal))
                {
                    throw new FormatException($"Invalid table file name: {fileName}.");
                }

                if (!files.Add(fileName))
                {
                    throw new FormatException($"Duplicate table contract: {fileName}.");
                }

                string hash = table.Value<string>("sha256");
                if (!HashPattern.IsMatch(hash ?? string.Empty))
                {
                    throw new FormatException($"Invalid SHA-256 for {fileName}.");
                }

                if (table.Value<int>("rowCount") < 0)
                {
                    throw new FormatException($"rowCount cannot be negative for {fileName}.");
                }

                var fields = new List<FieldContract>();
                var fieldNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (JObject field in table["fields"].Children<JObject>())
                {
                    RequireExactProperties(field, "name", "domain", "nullable", "enumValues");
                    string name = field.Value<string>("name");
                    if (!HeaderPattern.IsMatch(name ?? string.Empty) || !fieldNames.Add(name))
                    {
                        throw new FormatException($"Invalid or duplicate field {name} in {fileName}.");
                    }

                    string domain = field.Value<string>("domain");
                    if (!Enum.TryParse(domain, false, out ContentFieldDomain parsedDomain))
                    {
                        throw new FormatException($"Unknown domain {domain} in {fileName}.{name}.");
                    }

                    IReadOnlyList<string> enumValues = field["enumValues"].Type == JTokenType.Null
                        ? Array.Empty<string>()
                        : field["enumValues"].Values<string>().ToArray();
                    if (parsedDomain == ContentFieldDomain.ENUM && enumValues.Count == 0)
                    {
                        throw new FormatException($"ENUM field {fileName}.{name} requires enumValues.");
                    }

                    if (enumValues.Count != enumValues.Distinct(StringComparer.Ordinal).Count())
                    {
                        throw new FormatException($"enumValues must be unique for {fileName}.{name}.");
                    }

                    fields.Add(new FieldContract(name, parsedDomain, field.Value<bool>("nullable"), enumValues));
                }

                string[] primaryKey = table["primaryKey"].Values<string>().ToArray();
                if (primaryKey.Length == 0 ||
                    primaryKey.Length != primaryKey.Distinct(StringComparer.Ordinal).Count() ||
                    primaryKey.Any(key => !fieldNames.Contains(key)))
                {
                    throw new FormatException($"Invalid primary key for {fileName}.");
                }

                var foreignKeys = new List<ForeignKeyContract>();
                foreach (JObject foreignKey in table["foreignKeys"].Children<JObject>())
                {
                    RequireExactProperties(foreignKey, "fields", "targetFile", "targetFields");
                    string[] sourceFields = foreignKey["fields"].Values<string>().ToArray();
                    string[] targetFields = foreignKey["targetFields"].Values<string>().ToArray();
                    if (sourceFields.Length == 0 || sourceFields.Length != targetFields.Length || sourceFields.Any(field => !fieldNames.Contains(field)))
                    {
                        throw new FormatException($"Invalid foreign key in {fileName}.");
                    }

                    foreignKeys.Add(new ForeignKeyContract(sourceFields, foreignKey.Value<string>("targetFile"), targetFields));
                }

                tables.Add(new TableContract(
                    fileName,
                    hash,
                    table.Value<int>("rowCount"),
                    primaryKey,
                    fields,
                    foreignKeys));
            }

            var tablesByFile = tables.ToDictionary(table => table.FileName, StringComparer.Ordinal);
            foreach (TableContract table in tables)
            {
                foreach (ForeignKeyContract foreignKey in table.ForeignKeys)
                {
                    if (!tablesByFile.TryGetValue(foreignKey.TargetFile, out TableContract target) ||
                        foreignKey.TargetFields.Any(field => target.Fields.All(candidate => candidate.Name != field)))
                    {
                        throw new FormatException($"Foreign key target contract is invalid in {table.FileName}.");
                    }
                }
            }

            return new ContentManifest(contentVersion, tables);
        }

        private static ContentTable ParseTable(string text, TableContract contract, ValidationReport report)
        {
            List<List<string>> records;
            try
            {
                records = Rfc4180.Parse(text);
            }
            catch (FormatException exception)
            {
                report.AddError("CSV_RFC4180_INVALID", contract.FileName, "/", exception.Message);
                return null;
            }

            if (records.Count == 0)
            {
                report.AddError("CSV_HEADER_MISSING", contract.FileName, "1", "CSV file is empty.");
                return null;
            }

            IReadOnlyList<string> expectedHeaders = contract.Fields.Select(field => field.Name).ToArray();
            List<string> actualHeaders = records[0];
            if (actualHeaders.Count != actualHeaders.Distinct(StringComparer.Ordinal).Count())
            {
                report.AddError("CSV_HEADER_DUPLICATE", contract.FileName, "1", "Header names must be unique.");
            }

            if (!actualHeaders.SequenceEqual(expectedHeaders, StringComparer.Ordinal))
            {
                report.AddError("CSV_HEADER_CONTRACT_MISMATCH", contract.FileName, "1", "Header and order must match the manifest contract exactly.");
                return null;
            }

            if (records.Count - 1 != contract.RowCount)
            {
                report.AddError("CSV_ROW_COUNT_MISMATCH", contract.FileName, "/", $"Expected {contract.RowCount}, found {records.Count - 1}.");
            }

            var rows = new List<IReadOnlyDictionary<string, string>>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int recordIndex = 1; recordIndex < records.Count; recordIndex++)
            {
                List<string> values = records[recordIndex];
                string location = (recordIndex + 1).ToString(CultureInfo.InvariantCulture);
                if (values.Count != expectedHeaders.Count)
                {
                    report.AddError("CSV_COLUMN_COUNT_MISMATCH", contract.FileName, location, $"Expected {expectedHeaders.Count} columns, found {values.Count}.");
                    continue;
                }

                var row = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int fieldIndex = 0; fieldIndex < contract.Fields.Count; fieldIndex++)
                {
                    FieldContract field = contract.Fields[fieldIndex];
                    string value = values[fieldIndex];
                    row.Add(field.Name, value);
                    ValidateField(contract.FileName, location, field, value, report);
                }

                string key = BuildKey(row, contract.PrimaryKey);
                if (!keys.Add(key))
                {
                    report.AddError("CSV_PRIMARY_KEY_DUPLICATE", contract.FileName, location, "Primary key is duplicated.", key);
                }

                rows.Add(new ReadOnlyDictionary<string, string>(row));
            }

            if (!expectedHeaders.Contains("status") || !expectedHeaders.Contains("enabled"))
            {
                report.AddError("CSV_LIFECYCLE_FIELDS_MISSING", contract.FileName, "1", "Every canonical table requires status and enabled.");
            }

            return new ContentTable(contract, rows);
        }

        private static void ValidateField(
            string fileName,
            string row,
            FieldContract field,
            string value,
            ValidationReport report)
        {
            string location = row + "/" + field.Name;
            if (value.Length == 0)
            {
                if (!field.Nullable)
                {
                    report.AddError("CSV_NULL_FORBIDDEN", fileName, location, "Empty cell is allowed only for nullable fields.");
                }

                return;
            }

            if (value.Contains('|') || value.StartsWith("{", StringComparison.Ordinal) || value.StartsWith("[", StringComparison.Ordinal))
            {
                report.AddError("CSV_LEGACY_COMPOUND_CELL_FORBIDDEN", fileName, location, "Canonical cells cannot contain pipe lists or embedded JSON.");
                return;
            }

            bool valid = field.Domain switch
            {
                ContentFieldDomain.STRING => value == value.Trim() && value.All(character => !char.IsControl(character)),
                ContentFieldDomain.STABLE_ID => StableIdPattern.IsMatch(value),
                ContentFieldDomain.BOOL => value is "TRUE" or "FALSE",
                ContentFieldDomain.INT32 => IntegerPattern.IsMatch(value) && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
                ContentFieldDomain.SAFE_INT => IntegerPattern.IsMatch(value) && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed) && parsed >= -SafeIntegerMaximum && parsed <= SafeIntegerMaximum,
                ContentFieldDomain.DECIMAL => DecimalPattern.IsMatch(value) && decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
                ContentFieldDomain.UTC_INSTANT => DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _),
                ContentFieldDomain.STATUS => value is "CONFIRMED" or "TUNABLE" or "DEPRECATED" or "TEMPLATE",
                ContentFieldDomain.ENUM => field.EnumValues.Contains(value, StringComparer.Ordinal),
                _ => false
            };
            if (!valid)
            {
                report.AddError("CSV_FIELD_DOMAIN_INVALID", fileName, location, $"Value does not match {field.Domain} canonical lexical form.", value);
            }
        }

        private static void ValidateForeignKeys(IDictionary<string, ContentTable> tables, ValidationReport report)
        {
            foreach (ContentTable table in tables.Values.OrderBy(value => value.FileName, StringComparer.Ordinal))
            {
                foreach (ForeignKeyContract foreignKey in table.Contract.ForeignKeys)
                {
                    if (!tables.TryGetValue(foreignKey.TargetFile, out ContentTable target))
                    {
                        report.AddError("CSV_FOREIGN_KEY_TARGET_MISSING", table.FileName, "/", $"Target table {foreignKey.TargetFile} is missing.");
                        continue;
                    }

                    var targetKeys = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
                    foreach (IReadOnlyDictionary<string, string> targetRow in target.Rows)
                    {
                        targetKeys[BuildKey(targetRow, foreignKey.TargetFields)] = targetRow;
                    }

                    for (int index = 0; index < table.Rows.Count; index++)
                    {
                        IReadOnlyDictionary<string, string> row = table.Rows[index];
                        string key = BuildKey(row, foreignKey.Fields);
                        if (foreignKey.Fields.Any(field => string.IsNullOrEmpty(row[field])))
                        {
                            continue;
                        }

                        if (!targetKeys.TryGetValue(key, out IReadOnlyDictionary<string, string> targetRow))
                        {
                            report.AddError("CSV_FOREIGN_KEY_INVALID", table.FileName, (index + 2).ToString(CultureInfo.InvariantCulture), $"Foreign key to {foreignKey.TargetFile} is missing.", key);
                        }
                        else if (IsEnabled(row) && !IsEnabled(targetRow))
                        {
                            report.AddError("CSV_ENABLED_REFERENCE_DISABLED", table.FileName, (index + 2).ToString(CultureInfo.InvariantCulture), $"Enabled row references disabled row in {foreignKey.TargetFile}.", key);
                        }
                    }
                }
            }
        }

        private static void ValidateTaggedUnions(IDictionary<string, ContentTable> tables, ValidationReport report)
        {
            if (tables.TryGetValue("condition_group_members.csv", out ContentTable members))
            {
                for (int index = 0; index < members.Rows.Count; index++)
                {
                    IReadOnlyDictionary<string, string> row = members.Rows[index];
                    bool conditionMember = row["member_type"] == "CONDITION";
                    bool valid = conditionMember
                        ? row["condition_id"].Length > 0 && row["child_group_id"].Length == 0
                        : row["condition_id"].Length == 0 && row["child_group_id"].Length > 0;
                    if (!valid)
                    {
                        report.AddError("CSV_CONDITION_MEMBER_UNION_INVALID", members.FileName, (index + 2).ToString(CultureInfo.InvariantCulture), "Exactly one member reference must match member_type.");
                    }
                }
            }

            if (tables.TryGetValue("reward_entries.csv", out ContentTable rewards))
            {
                ValidateRewardEntries(rewards, tables, report);
            }
        }

        private static void ValidateRewardEntries(ContentTable rewards, IDictionary<string, ContentTable> tables, ValidationReport report)
        {
            var registryByType = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ITEM"] = "items.csv",
                ["POTION"] = "potions.csv",
                ["CURRENCY"] = "currencies.csv",
                ["EQUIPMENT_TEMPLATE"] = "equipment_templates.csv",
                ["RANDOM_EQUIPMENT_TIER"] = "random_equipment_tier_specs.csv",
                ["REGION_UNLOCK"] = "regions.csv",
                ["PROGRESSION_FLAG"] = "progression_flags.csv"
            };
            var sentinelByType = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PERSONAL_GOLD"] = "SYSTEM_PERSONAL_GOLD",
                ["PLAYER_EXP"] = "SYSTEM_PLAYER_EXP",
                ["KINGDOM_EXP"] = "SYSTEM_KINGDOM_EXP"
            };

            for (int index = 0; index < rewards.Rows.Count; index++)
            {
                IReadOnlyDictionary<string, string> row = rewards.Rows[index];
                string rewardType = row["reward_type"];
                string rewardId = row["reward_id"];
                string location = (index + 2).ToString(CultureInfo.InvariantCulture);
                if (rewardType == "GENERATED_MERCENARY")
                {
                    report.AddError("CSV_REWARD_GENERATED_MERCENARY_FORBIDDEN", rewards.FileName, location, "Generated mercenaries are recruitment results, not reward entries.");
                    continue;
                }

                if (sentinelByType.TryGetValue(rewardType, out string sentinel))
                {
                    if (rewardId != sentinel)
                    {
                        report.AddError("CSV_REWARD_SENTINEL_INVALID", rewards.FileName, location, $"{rewardType} requires {sentinel}.", rewardId);
                    }

                    continue;
                }

                if (!registryByType.TryGetValue(rewardType, out string registryFile) || !tables.TryGetValue(registryFile, out ContentTable registry))
                {
                    report.AddError("CSV_REWARD_REGISTRY_INVALID", rewards.FileName, location, $"Reward type {rewardType} has no loaded registry.", rewardId);
                    continue;
                }

                string registryKey = registry.Contract.PrimaryKey.Count == 1 ? registry.Contract.PrimaryKey[0] : null;
                if (registryKey == null || !registry.Rows.Any(registryRow => registryRow[registryKey] == rewardId && IsEnabled(registryRow)))
                {
                    report.AddError("CSV_REWARD_ID_INVALID", rewards.FileName, location, $"Reward ID is not enabled in {registryFile}.", rewardId);
                }
            }
        }

        private static void ValidateHash(string text, TableContract contract, ValidationReport report)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            string actual = string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
            if (!string.Equals(actual, contract.Sha256, StringComparison.Ordinal))
            {
                report.AddError("CSV_FILE_HASH_MISMATCH", contract.FileName, "/", "CSV SHA-256 does not match manifest.");
            }
        }

        private static bool IsEnabled(IReadOnlyDictionary<string, string> row)
        {
            return !row.TryGetValue("enabled", out string enabled) || enabled == "TRUE";
        }

        private static string BuildKey(IReadOnlyDictionary<string, string> row, IReadOnlyList<string> fields)
        {
            return string.Join("\u001f", fields.Select(field => row[field]));
        }

        private static void RequireExactProperties(JObject value, params string[] expected)
        {
            string[] actual = value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] sortedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(sortedExpected, StringComparer.Ordinal))
            {
                throw new FormatException("Manifest object has missing or unknown properties.");
            }
        }

        private sealed class ContentManifest
        {
            public ContentManifest(string contentVersion, IReadOnlyList<TableContract> tables)
            {
                ContentVersion = contentVersion;
                Tables = tables;
            }

            public string ContentVersion { get; }

            public IReadOnlyList<TableContract> Tables { get; }
        }
    }

    internal enum ContentFieldDomain
    {
        STRING,
        STABLE_ID,
        BOOL,
        INT32,
        SAFE_INT,
        DECIMAL,
        UTC_INSTANT,
        STATUS,
        ENUM
    }

    internal sealed class FieldContract
    {
        public FieldContract(string name, ContentFieldDomain domain, bool nullable, IReadOnlyList<string> enumValues)
        {
            Name = name;
            Domain = domain;
            Nullable = nullable;
            EnumValues = enumValues;
        }

        public string Name { get; }

        public ContentFieldDomain Domain { get; }

        public bool Nullable { get; }

        public IReadOnlyList<string> EnumValues { get; }
    }

    internal sealed class ForeignKeyContract
    {
        public ForeignKeyContract(IReadOnlyList<string> fields, string targetFile, IReadOnlyList<string> targetFields)
        {
            Fields = fields;
            TargetFile = targetFile;
            TargetFields = targetFields;
        }

        public IReadOnlyList<string> Fields { get; }

        public string TargetFile { get; }

        public IReadOnlyList<string> TargetFields { get; }
    }

    internal sealed class TableContract
    {
        public TableContract(
            string fileName,
            string sha256,
            int rowCount,
            IReadOnlyList<string> primaryKey,
            IReadOnlyList<FieldContract> fields,
            IReadOnlyList<ForeignKeyContract> foreignKeys)
        {
            FileName = fileName;
            Sha256 = sha256;
            RowCount = rowCount;
            PrimaryKey = primaryKey;
            Fields = fields;
            ForeignKeys = foreignKeys;
        }

        public string FileName { get; }

        public string Sha256 { get; }

        public int RowCount { get; }

        public IReadOnlyList<string> PrimaryKey { get; }

        public IReadOnlyList<FieldContract> Fields { get; }

        public IReadOnlyList<ForeignKeyContract> ForeignKeys { get; }
    }

    internal static class Rfc4180
    {
        public static List<List<string>> Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length > 0 && text[0] == '\ufeff')
            {
                text = text.Substring(1);
            }

            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            bool afterQuote = false;

            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (quoted)
                {
                    if (character == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            quoted = false;
                            afterQuote = true;
                        }
                    }
                    else
                    {
                        field.Append(character);
                    }

                    continue;
                }

                if (afterQuote && character is not ',' and not '\r' and not '\n')
                {
                    throw new FormatException("Unexpected character after a quoted field.");
                }

                if (character == '"')
                {
                    if (field.Length != 0 || afterQuote)
                    {
                        throw new FormatException("Quote is allowed only at the start of a field.");
                    }

                    quoted = true;
                }
                else if (character == ',')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    afterQuote = false;
                }
                else if (character is '\r' or '\n')
                {
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();
                    afterQuote = false;
                }
                else
                {
                    field.Append(character);
                }
            }

            if (quoted)
            {
                throw new FormatException("Quoted field is not terminated.");
            }

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }

            return records;
        }
    }
}
