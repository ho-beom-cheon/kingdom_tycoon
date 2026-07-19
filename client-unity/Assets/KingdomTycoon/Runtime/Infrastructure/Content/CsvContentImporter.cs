using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
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
        private static readonly BigInteger Seed64Maximum = BigInteger.Parse("18446744073709551615", CultureInfo.InvariantCulture);
        private static readonly Regex HeaderPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.CultureInvariant);
        private static readonly Regex StableIdPattern = new("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant);
        private static readonly Regex IntegerPattern = new("^(0|-?[1-9][0-9]*)$", RegexOptions.CultureInvariant);
        private static readonly Regex DecimalPattern = new("^-?(0|[1-9][0-9]*)(?:\\.[0-9]+)?$", RegexOptions.CultureInvariant);
        private static readonly Regex HashPattern = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant);
        private static readonly Regex DatePattern = new("^[0-9]{4}-[0-9]{2}-[0-9]{2}$", RegexOptions.CultureInvariant);
        private static readonly Regex LocalePattern = new("^[a-z]{2}-[A-Z]{2}$", RegexOptions.CultureInvariant);
        private static readonly Regex SemVerPattern = new("^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);
        private static readonly Regex ContentVersionPattern = new("^[0-9]+\\.[0-9]+\\.[0-9]+-content\\.[1-9][0-9]*$", RegexOptions.CultureInvariant);

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
            catch (ContentManifestException exception)
            {
                report.AddError(exception.Code, "content_manifest.json", exception.Location, exception.Message);
                return new ContentImportResult(null, report);
            }
            catch (Exception exception)
            {
                report.AddError("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", "content_manifest.json", "/", exception.Message);
                return new ContentImportResult(null, report);
            }

            var tables = new Dictionary<string, ContentTable>(StringComparer.Ordinal);
            foreach (TableContract contract in manifest.Tables)
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
            ContentSemanticValidator.Validate(tables, report);
            return report.IsValid
                ? new ContentImportResult(new ContentCatalog(manifest.ContentVersion, tables), report)
                : new ContentImportResult(null, report);
        }

        private static ContentManifest ParseManifest(string json)
        {
            JObject root = StrictJson.ParseObject(json);
            RequireExactProperties(root, "/",
                "schemaId", "contractVersion", "contentVersion", "csvSchemaSetVersion", "packageKind",
                "baseContentVersion", "minimumGameVersion", "channel", "generatedAtUtc", "tables");
            int schemaSetVersion = root.Value<int?>("csvSchemaSetVersion") ?? -1;
            if (root.Value<string>("schemaId") != "urn:tycoon:content-manifest:v2" ||
                root.Value<int?>("contractVersion") != 2 ||
                schemaSetVersion is not (2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11))
            {
                throw new ContentManifestException(
                    "CONTENT_MANIFEST_SCHEMA_SET_UNSUPPORTED",
                    "/csvSchemaSetVersion",
                    "Only content manifest contract v2/schema set v2 through v11 is supported.");
            }
            string contentVersion = root.Value<string>("contentVersion");
            if (!ContentVersionPattern.IsMatch(contentVersion ?? string.Empty))
            {
                throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", "/contentVersion", "contentVersion is not canonical.");
            }
            string packageKind = root.Value<string>("packageKind");
            string baseContentVersion = root["baseContentVersion"]?.Type == JTokenType.Null
                ? null
                : root.Value<string>("baseContentVersion");
            bool validBaseVersion = packageKind == "BASE"
                ? baseContentVersion == null
                : packageKind is "PATCH" or "TEMPLATE" && ContentVersionPattern.IsMatch(baseContentVersion ?? string.Empty);
            if (!validBaseVersion)
            {
                throw new ContentManifestException("CONTENT_MANIFEST_BASE_VERSION_INVALID", "/baseContentVersion", "packageKind/baseContentVersion matrix is invalid.");
            }
            string minimumGameVersion = root.Value<string>("minimumGameVersion");
            if (!SemVerPattern.IsMatch(minimumGameVersion ?? string.Empty) || root.Value<string>("channel") is not ("DEV" or "STAGING" or "PRODUCTION"))
            {
                throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", "/minimumGameVersion", "Release metadata is invalid.");
            }
            if (!DateTimeOffset.TryParseExact(
                    root.Value<string>("generatedAtUtc"),
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out _))
            {
                throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", "/generatedAtUtc", "generatedAtUtc must be a canonical UTC instant.");
            }

            var tables = new List<TableContract>();
            var files = new HashSet<string>(StringComparer.Ordinal);
            JArray tableArray = root["tables"] as JArray
                ?? throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", "/tables", "tables must be an array.");
            string previousFile = null;
            for (int tableIndex = 0; tableIndex < tableArray.Count; tableIndex++)
            {
                JObject table = tableArray[tableIndex] as JObject
                    ?? throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"/tables/{tableIndex}", "Table descriptor must be an object.");
                string tableLocation = $"/tables/{tableIndex}";
                RequireExactProperties(table, tableLocation, "file", "schemaVersion", "required", "sha256", "rowCount", "primaryKey", "fields", "foreignKeys");
                string fileName = table.Value<string>("file");
                if (string.IsNullOrEmpty(fileName) || fileName.Contains('/') || fileName.Contains('\\') || !Regex.IsMatch(fileName, "^[a-z][a-z0-9_]*\\.csv$", RegexOptions.CultureInvariant))
                {
                    throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", tableLocation + "/file", $"Invalid table file name: {fileName}.");
                }
                if (!files.Add(fileName) || previousFile != null && string.CompareOrdinal(previousFile, fileName) >= 0)
                {
                    throw new ContentManifestException("CONTENT_MANIFEST_TABLE_ORDER_INVALID", tableLocation + "/file", "Table files must be unique and UTF-8 ordinal ascending.");
                }
                previousFile = fileName;
                if (table.Value<int?>("schemaVersion") != 1 || table.Value<bool?>("required") != true)
                {
                    throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", tableLocation, "P03 tables require schemaVersion=1 and required=true.");
                }
                string hash = table.Value<string>("sha256");
                if (!HashPattern.IsMatch(hash ?? string.Empty))
                {
                    throw new ContentManifestException("CONTENT_MANIFEST_HASH_FORMAT_INVALID", tableLocation + "/sha256", $"Invalid SHA-256 for {fileName}.");
                }
                long rowCount = table.Value<long?>("rowCount") ?? -1;
                if (rowCount < 0 || rowCount > SafeIntegerMaximum)
                {
                    throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", tableLocation + "/rowCount", $"rowCount is invalid for {fileName}.");
                }

                var fields = new List<FieldContract>();
                var fieldNames = new HashSet<string>(StringComparer.Ordinal);
                JArray fieldArray = table["fields"] as JArray
                    ?? throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", tableLocation + "/fields", "fields must be an array.");
                for (int fieldIndex = 0; fieldIndex < fieldArray.Count; fieldIndex++)
                {
                    JObject field = fieldArray[fieldIndex] as JObject
                        ?? throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"{tableLocation}/fields/{fieldIndex}", "Field must be an object.");
                    RequireExactProperties(field, $"{tableLocation}/fields/{fieldIndex}", "name", "domain", "nullable", "enumValues");
                    string name = field.Value<string>("name");
                    if (!HeaderPattern.IsMatch(name ?? string.Empty) || !fieldNames.Add(name))
                    {
                        throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"{tableLocation}/fields/{fieldIndex}/name", $"Invalid or duplicate field {name} in {fileName}.");
                    }

                    string domain = field.Value<string>("domain");
                    if (!Enum.TryParse(domain, false, out ContentFieldDomain parsedDomain))
                    {
                        throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"{tableLocation}/fields/{fieldIndex}/domain", $"Unknown domain {domain} in {fileName}.{name}.");
                    }

                    IReadOnlyList<string> enumValues = field["enumValues"] is JArray enumArray ? enumArray.Values<string>().ToArray() : Array.Empty<string>();
                    if (parsedDomain == ContentFieldDomain.ENUM && enumValues.Count == 0)
                    {
                        throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"{tableLocation}/fields/{fieldIndex}/enumValues", $"ENUM field {fileName}.{name} requires enumValues.");
                    }

                    if (enumValues.Count != enumValues.Distinct(StringComparer.Ordinal).Count())
                    {
                        throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"{tableLocation}/fields/{fieldIndex}/enumValues", $"enumValues must be unique for {fileName}.{name}.");
                    }

                    fields.Add(new FieldContract(name, parsedDomain, field.Value<bool>("nullable"), enumValues));
                }

                string[] primaryKey = table["primaryKey"].Values<string>().ToArray();
                if (primaryKey.Length == 0 ||
                    primaryKey.Length != primaryKey.Distinct(StringComparer.Ordinal).Count() ||
                    primaryKey.Any(key => !fieldNames.Contains(key)))
                {
                    throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", tableLocation + "/primaryKey", $"Invalid primary key for {fileName}.");
                }

                var foreignKeys = new List<ForeignKeyContract>();
                JArray foreignKeyArray = table["foreignKeys"] as JArray
                    ?? throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", tableLocation + "/foreignKeys", "foreignKeys must be an array.");
                for (int foreignKeyIndex = 0; foreignKeyIndex < foreignKeyArray.Count; foreignKeyIndex++)
                {
                    JObject foreignKey = foreignKeyArray[foreignKeyIndex] as JObject
                        ?? throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"{tableLocation}/foreignKeys/{foreignKeyIndex}", "Foreign key must be an object.");
                    RequireExactProperties(foreignKey, $"{tableLocation}/foreignKeys/{foreignKeyIndex}", "sourceFields", "targetFile", "targetFields", "mode");
                    string[] sourceFields = foreignKey["sourceFields"].Values<string>().ToArray();
                    string[] targetFields = foreignKey["targetFields"].Values<string>().ToArray();
                    string mode = foreignKey.Value<string>("mode");
                    if (sourceFields.Length == 0 || sourceFields.Length != targetFields.Length || sourceFields.Any(field => !fieldNames.Contains(field)) || mode is not ("HARD" or "SOFT" or "SOFT_SENTINEL_EMPTY"))
                    {
                        throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", $"{tableLocation}/foreignKeys/{foreignKeyIndex}", $"Invalid foreign key in {fileName}.");
                    }

                    foreignKeys.Add(new ForeignKeyContract(sourceFields, foreignKey.Value<string>("targetFile"), targetFields, mode == "HARD"));
                }

                tables.Add(new TableContract(
                    fileName,
                    hash,
                    checked((int)rowCount),
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
                        throw new ContentManifestException("CONTENT_MANIFEST_REQUIRED_FIELD_MISSING", "/tables", $"Foreign key target contract is invalid in {table.FileName}.");
                    }
                }
            }

            return new ContentManifest(contentVersion, minimumGameVersion, root.Value<string>("channel"), tables);
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
                report.AddError("CONTENT_MANIFEST_HEADER_MISMATCH", contract.FileName, "1", "Header and order must match the manifest contract exactly.");
                return null;
            }

            if (records.Count - 1 != contract.RowCount)
            {
                report.AddError("CONTENT_MANIFEST_ROW_COUNT_MISMATCH", contract.FileName, "/", $"Expected {contract.RowCount}, found {records.Count - 1}.");
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

                if (row.TryGetValue("status", out string status) &&
                    row.TryGetValue("enabled", out string enabled) &&
                    (status is "DEFERRED" or "OPS_LATER" or "REFERENCE_ONLY" or "REJECTED" or "UNRESOLVED" or "TEMPLATE") &&
                    enabled != "FALSE")
                {
                    report.AddError("CSV_STATUS_ENABLED_INVALID", contract.FileName, location, "Non-runtime lifecycle statuses require enabled=FALSE.");
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

            if (value.Contains('|'))
            {
                report.AddError("CSV_PIPE_LIST_FORBIDDEN", fileName, location, "Canonical cells cannot contain pipe lists.");
                return;
            }

            if (field.Name != "text_value" &&
                (value.StartsWith("{", StringComparison.Ordinal) || value.StartsWith("[", StringComparison.Ordinal)))
            {
                report.AddError("CSV_JSON_CELL_FORBIDDEN", fileName, location, "Canonical cells cannot contain embedded JSON.");
                return;
            }

            bool valid = field.Domain switch
            {
                ContentFieldDomain.STRING => Encoding.UTF8.GetByteCount(value) <= 4096 &&
                    !value.Contains('\0') &&
                    value.All(character => !char.IsControl(character) ||
                        field.Name == "text_value" && (character is '\r' or '\n')),
                ContentFieldDomain.STABLE_ID => StableIdPattern.IsMatch(value),
                ContentFieldDomain.BOOL => value is "TRUE" or "FALSE",
                ContentFieldDomain.INT32 => IntegerPattern.IsMatch(value) && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
                ContentFieldDomain.INT64 => IntegerPattern.IsMatch(value) && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
                ContentFieldDomain.SAFE_INT => IntegerPattern.IsMatch(value) && long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long parsed) && parsed >= -SafeIntegerMaximum && parsed <= SafeIntegerMaximum,
                ContentFieldDomain.POSITIVE_INT => IntegerPattern.IsMatch(value) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long positive) && positive > 0 && positive <= SafeIntegerMaximum,
                ContentFieldDomain.DECIMAL => DecimalPattern.IsMatch(value) && decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _),
                ContentFieldDomain.UTC_INSTANT => DateTimeOffset.TryParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _),
                ContentFieldDomain.DATE => DatePattern.IsMatch(value) && DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                ContentFieldDomain.LOCALE => LocalePattern.IsMatch(value),
                ContentFieldDomain.SEMVER => SemVerPattern.IsMatch(value),
                ContentFieldDomain.HEX64 => HashPattern.IsMatch(value),
                ContentFieldDomain.SEED64 => IntegerPattern.IsMatch(value) && BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger seed) && seed >= BigInteger.Zero && seed <= Seed64Maximum,
                ContentFieldDomain.STATUS => value is "CONFIRMED" or "TUNABLE" or "DEFERRED" or "OPS_LATER" or "REFERENCE_ONLY" or "REJECTED" or "UNRESOLVED" or "TEMPLATE",
                ContentFieldDomain.ENUM => field.EnumValues.Contains(value, StringComparer.Ordinal),
                _ => false
            };
            if (!valid)
            {
                report.AddError("CSV_DOMAIN_INVALID", fileName, location, $"Value does not match {field.Domain} canonical lexical form.", value);
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
                            if (foreignKey.IsHard || IsEnabled(row))
                            {
                                report.AddError("CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET", table.FileName, (index + 2).ToString(CultureInfo.InvariantCulture), $"Foreign key to {foreignKey.TargetFile} is missing.", key);
                            }
                        }
                        else if (IsEnabled(row) && !IsEnabled(targetRow))
                        {
                            report.AddError("CSV_ENABLED_ROW_REFERENCES_MISSING_TARGET", table.FileName, (index + 2).ToString(CultureInfo.InvariantCulture), $"Enabled row references disabled row in {foreignKey.TargetFile}.", key);
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
                        report.AddError("CSV_CONDITION_GROUP_INVALID", members.FileName, (index + 2).ToString(CultureInfo.InvariantCulture), "Exactly one member reference must match member_type.");
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
                    report.AddError("CSV_REWARD_DISCRIMINATOR_MISMATCH", rewards.FileName, location, "Generated mercenaries are recruitment results, not reward entries.");
                    continue;
                }

                if (sentinelByType.TryGetValue(rewardType, out string sentinel))
                {
                    if (rewardId != sentinel)
                    {
                        report.AddError("CSV_REWARD_DISCRIMINATOR_MISMATCH", rewards.FileName, location, $"{rewardType} requires {sentinel}.", rewardId);
                    }

                    continue;
                }

                if (!registryByType.TryGetValue(rewardType, out string registryFile) || !tables.TryGetValue(registryFile, out ContentTable registry))
                {
                    report.AddError("CSV_REWARD_DISCRIMINATOR_MISMATCH", rewards.FileName, location, $"Reward type {rewardType} has no loaded registry.", rewardId);
                    continue;
                }

                string registryKey = registry.Contract.PrimaryKey.Count == 1 ? registry.Contract.PrimaryKey[0] : null;
                if (registryKey == null || !registry.Rows.Any(registryRow => registryRow[registryKey] == rewardId && IsEnabled(registryRow)))
                {
                    report.AddError("CSV_REWARD_DISCRIMINATOR_MISMATCH", rewards.FileName, location, $"Reward ID is not enabled in {registryFile}.", rewardId);
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
                report.AddError("CONTENT_MANIFEST_HASH_MISMATCH", contract.FileName, "/", "CSV SHA-256 does not match manifest.");
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

        private static void RequireExactProperties(JObject value, string location, params string[] expected)
        {
            string[] actual = value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] sortedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(sortedExpected, StringComparer.Ordinal))
            {
                string code = actual.Except(sortedExpected, StringComparer.Ordinal).Any()
                    ? "CONTENT_MANIFEST_UNKNOWN_FIELD"
                    : "CONTENT_MANIFEST_REQUIRED_FIELD_MISSING";
                throw new ContentManifestException(code, location, "Manifest object has missing or unknown properties.");
            }
        }

        private sealed class ContentManifest
        {
            public ContentManifest(string contentVersion, string minimumGameVersion, string channel, IReadOnlyList<TableContract> tables)
            {
                ContentVersion = contentVersion;
                MinimumGameVersion = minimumGameVersion;
                Channel = channel;
                Tables = tables;
            }

            public string ContentVersion { get; }

            public string MinimumGameVersion { get; }

            public string Channel { get; }

            public IReadOnlyList<TableContract> Tables { get; }
        }
    }

    internal sealed class ContentManifestException : FormatException
    {
        public ContentManifestException(string code, string location, string message)
            : base(message)
        {
            Code = code;
            Location = location;
        }

        public string Code { get; }

        public string Location { get; }
    }

    internal enum ContentFieldDomain
    {
        STRING,
        STABLE_ID,
        BOOL,
        INT32,
        INT64,
        SAFE_INT,
        POSITIVE_INT,
        DECIMAL,
        UTC_INSTANT,
        DATE,
        LOCALE,
        SEMVER,
        HEX64,
        SEED64,
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
        public ForeignKeyContract(IReadOnlyList<string> fields, string targetFile, IReadOnlyList<string> targetFields, bool isHard)
        {
            Fields = fields;
            TargetFile = targetFile;
            TargetFields = targetFields;
            IsHard = isHard;
        }

        public IReadOnlyList<string> Fields { get; }

        public string TargetFile { get; }

        public IReadOnlyList<string> TargetFields { get; }

        public bool IsHard { get; }
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
