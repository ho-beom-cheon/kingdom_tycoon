using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KingdomTycoon.Infrastructure;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Save
{
    public sealed class SaveValidationResult
    {
        public SaveValidationResult(JObject document, ValidationReport report)
        {
            Document = document;
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public JObject Document { get; }

        public ValidationReport Report { get; }

        public bool IsValid => Document != null && Report.IsValid;
    }

    public sealed class SaveDocumentValidator
    {
        private static readonly HashSet<string> FacilityIds = new(StringComparer.Ordinal)
        {
            "FAC_TAVERN",
            "FAC_LODGE",
            "FAC_GUILD",
            "FAC_STORE",
            "FAC_BLACKSMITH",
            "FAC_ALCHEMY",
            "FAC_WAREHOUSE",
            "FAC_INFIRMARY"
        };

        private static readonly HashSet<string> EquipmentRewardTypes = new(StringComparer.Ordinal)
        {
            "EQUIPMENT_TEMPLATE",
            "RANDOM_EQUIPMENT_TIER"
        };

        private readonly JObject schema;
        private readonly IReadOnlyDictionary<string, JObject> versionSchemas;
        private readonly JsonSchemaValidator schemaValidator = new();

        public SaveDocumentValidator(string schemaJson)
        {
            schema = StrictJson.ParseObject(schemaJson ?? throw new ArgumentNullException(nameof(schemaJson)));
            versionSchemas = new Dictionary<string, JObject>(StringComparer.Ordinal);
        }

        public SaveDocumentValidator(string legacySchemaJson, string content5SchemaJson)
            : this(legacySchemaJson, content5SchemaJson, null)
        {
        }

        public SaveDocumentValidator(string legacySchemaJson, string content5SchemaJson, string content6SchemaJson)
            : this(legacySchemaJson, content5SchemaJson, content6SchemaJson, null)
        {
        }

        public SaveDocumentValidator(string legacySchemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson)
            : this(legacySchemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, null)
        {
        }

        public SaveDocumentValidator(string legacySchemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson, string content8SchemaJson)
            : this(legacySchemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, content8SchemaJson, null)
        {
        }

        public SaveDocumentValidator(string legacySchemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson, string content8SchemaJson, string content9SchemaJson)
            : this(legacySchemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, content8SchemaJson, content9SchemaJson, null)
        {
        }

        public SaveDocumentValidator(string legacySchemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson, string content8SchemaJson, string content9SchemaJson, string content10SchemaJson)
            : this(legacySchemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, content8SchemaJson, content9SchemaJson, content10SchemaJson, null)
        {
        }

        public SaveDocumentValidator(string legacySchemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson, string content8SchemaJson, string content9SchemaJson, string content10SchemaJson, string content11SchemaJson, string content12SchemaJson = null)
        {
            schema = StrictJson.ParseObject(legacySchemaJson ?? throw new ArgumentNullException(nameof(legacySchemaJson)));
            var content5 = StrictJson.ParseObject(content5SchemaJson ?? throw new ArgumentNullException(nameof(content5SchemaJson)));
            var schemas = new Dictionary<string, JObject>(StringComparer.Ordinal)
            {
                ["1.0.0-content.5"] = content5
            };
            if (content6SchemaJson != null)
            {
                schemas["1.0.0-content.6"] = StrictJson.ParseObject(content6SchemaJson);
            }
            if (content7SchemaJson != null)
            {
                schemas["1.0.0-content.7"] = StrictJson.ParseObject(content7SchemaJson);
            }
            if (content8SchemaJson != null)
            {
                schemas["1.0.0-content.8"] = StrictJson.ParseObject(content8SchemaJson);
            }
            if (content9SchemaJson != null)
            {
                schemas["1.0.0-content.9"] = StrictJson.ParseObject(content9SchemaJson);
            }
            if (content10SchemaJson != null)
            {
                schemas["1.0.0-content.10"] = StrictJson.ParseObject(content10SchemaJson);
            }
            if (content11SchemaJson != null)
            {
                schemas["1.0.0-content.11"] = StrictJson.ParseObject(content11SchemaJson);
            }
            if (content12SchemaJson != null)
            {
                schemas["1.0.0-content.12"] = StrictJson.ParseObject(content12SchemaJson);
            }
            versionSchemas = schemas;
        }

        public SaveValidationResult ParseAndValidate(string json, string source)
        {
            var report = new ValidationReport();
            JObject document;
            try
            {
                document = StrictJson.ParseObject(json);
            }
            catch (Exception exception)
            {
                report.AddError("SAVE_JSON_INVALID", source, "/", exception.Message);
                return new SaveValidationResult(null, report);
            }

            report.Merge(Validate(document, source));
            return new SaveValidationResult(document, report);
        }

        public ValidationReport Validate(JObject document, string source)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            JObject selected = versionSchemas.TryGetValue(document.Value<string>("contentVersion") ?? string.Empty, out JObject versionSchema)
                ? versionSchema
                : schema;
            var report = schemaValidator.Validate(document, selected, source);
            if (!report.IsValid)
            {
                return report;
            }

            ValidateIntegrity(document, source, report);
            ValidateTimestamps(document, source, report);
            ValidateFacilities(document, source, report);
            ValidateCollections(document, source, report);
            ValidateEquipmentLinks(document, source, report);
            ValidateRecruitment(document, source, report);
            ValidateP13Recruitment(document, source, report);
            ValidateJournal(document, source, report);
            ValidateRewardSnapshots(document, source, report);
            ValidateEconomy(document, source, report);
            ValidateProduction(document, source, report);
            ValidateEquipmentGrowth(document, source, report);
            ValidateProgression(document, source, report);
            ValidateRegions(document, source, report);
            ValidateP14Raids(document, source, report);
            return report;
        }

        public JObject PrepareForCommit(JObject document, long expectedRevision, DateTimeOffset savedAtUtc)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            long revision = document.Value<long>("revision");
            if (revision != expectedRevision)
            {
                throw new InvalidOperationException(
                    $"SAVE_REVISION_CONFLICT: expected {expectedRevision.ToString(CultureInfo.InvariantCulture)}, " +
                    $"but document revision is {revision.ToString(CultureInfo.InvariantCulture)}.");
            }

            var prepared = (JObject)document.DeepClone();
            prepared["revision"] = checked(expectedRevision + 1);
            prepared["savedAtUtc"] = savedAtUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

            JObject integrity = (JObject)prepared["integrity"];
            integrity["payloadSha256"] = Rfc8785Canonicalizer.ComputeSha256(prepared["payload"]);
            integrity["fileSha256"] = Rfc8785Canonicalizer.ComputeSha256(BuildEnvelopeDigestInput(prepared));
            return prepared;
        }

        public static JObject BuildEnvelopeDigestInput(JObject document)
        {
            JObject integrity = (JObject)document["integrity"];
            return new JObject
            {
                ["schemaId"] = document["schemaId"]?.DeepClone(),
                ["saveVersion"] = document["saveVersion"]?.DeepClone(),
                ["gameVersion"] = document["gameVersion"]?.DeepClone(),
                ["contentVersion"] = document["contentVersion"]?.DeepClone(),
                ["saveId"] = document["saveId"]?.DeepClone(),
                ["profileId"] = document["profileId"]?.DeepClone(),
                ["revision"] = document["revision"]?.DeepClone(),
                ["createdAtUtc"] = document["createdAtUtc"]?.DeepClone(),
                ["savedAtUtc"] = document["savedAtUtc"]?.DeepClone(),
                ["integrityVersion"] = integrity?["integrityVersion"]?.DeepClone(),
                ["algorithm"] = integrity?["algorithm"]?.DeepClone(),
                ["canonicalization"] = integrity?["canonicalization"]?.DeepClone(),
                ["payloadSha256"] = integrity?["payloadSha256"]?.DeepClone()
            };
        }

        private static void ValidateIntegrity(JObject document, string source, ValidationReport report)
        {
            JObject integrity = (JObject)document["integrity"];
            string actualPayloadHash = Rfc8785Canonicalizer.ComputeSha256(document["payload"]);
            if (!string.Equals(actualPayloadHash, integrity.Value<string>("payloadSha256"), StringComparison.Ordinal))
            {
                report.AddError("SAVE_PAYLOAD_HASH_MISMATCH", source, "/integrity/payloadSha256", "Payload digest does not match JCS(payload).");
            }

            string actualFileHash = Rfc8785Canonicalizer.ComputeSha256(BuildEnvelopeDigestInput(document));
            if (!string.Equals(actualFileHash, integrity.Value<string>("fileSha256"), StringComparison.Ordinal))
            {
                report.AddError("SAVE_FILE_HASH_MISMATCH", source, "/integrity/fileSha256", "Envelope digest does not match the logical save envelope.");
            }
        }

        private static void ValidateTimestamps(JObject document, string source, ValidationReport report)
        {
            DateTimeOffset createdAt = ParseUtc(document.Value<string>("createdAtUtc"));
            DateTimeOffset savedAt = ParseUtc(document.Value<string>("savedAtUtc"));
            if (savedAt < createdAt)
            {
                report.AddError("SAVE_TIMESTAMP_ORDER_INVALID", source, "/savedAtUtc", "savedAtUtc must be at or after createdAtUtc.");
            }
        }

        private static void ValidateFacilities(JObject document, string source, ValidationReport report)
        {
            JArray facilities = (JArray)document["payload"]["facilities"];
            JArray managementNpcs = (JArray)document["payload"]["managementNpcs"];
            ILookup<string, JObject> journalByOperationId = document["payload"]["operationJournal"]
                .Children<JObject>()
                .ToLookup(entry => entry.Value<string>("operationId"), StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            long levelDelta = 0;
            for (int index = 0; index < facilities.Count; index++)
            {
                JObject facility = (JObject)facilities[index];
                string location = $"/payload/facilities/{index.ToString(CultureInfo.InvariantCulture)}";
                string facilityId = facility.Value<string>("facilityId");
                if (!FacilityIds.Contains(facilityId) || !seen.Add(facilityId))
                {
                    report.AddError("SAVE_FACILITY_SET_INVALID", source, location + "/facilityId", "Each core facility must occur exactly once.", facilityId);
                }

                levelDelta += facility.Value<long>("level") - 1;
                ValidateFacilityJobMatrix(facility, journalByOperationId, source, location, report);
                ValidateFacilityNpcLinks(facility, managementNpcs, source, location, report);
            }

            long persistedCount = document["payload"]["kingdom"].Value<long>("facilityUpgradeCount");
            if (persistedCount != levelDelta)
            {
                report.AddError(
                    "SAVE_FACILITY_UPGRADE_COUNT_MISMATCH",
                    source,
                    "/payload/kingdom/facilityUpgradeCount",
                    $"Expected {levelDelta.ToString(CultureInfo.InvariantCulture)} from facility levels.");
            }
        }

        private static void ValidateFacilityNpcLinks(JObject facility, JArray managementNpcs, string source, string location, ValidationReport report)
        {
            string facilityId = facility.Value<string>("facilityId");
            bool managed = facilityId is "FAC_STORE" or "FAC_BLACKSMITH" or "FAC_ALCHEMY" or "FAC_INFIRMARY";
            string state = facility.Value<string>("state");
            string assignedId = facility["assignedNpcInstanceId"].Type == JTokenType.Null ? null : facility.Value<string>("assignedNpcInstanceId");
            JObject[] linked = managementNpcs.Children<JObject>()
                .Where(npc => string.Equals(npc.Value<string>("assignedFacilityId"), facilityId, StringComparison.Ordinal))
                .ToArray();

            if (!managed && (assignedId != null || linked.Length != 0))
            {
                report.AddError("SAVE_FACILITY_NPC_LINK_INVALID", source, location + "/assignedNpcInstanceId", "SYSTEM facilities cannot have management NPC links.");
                return;
            }

            if (assignedId == null)
            {
                if (linked.Length != 0 || (managed && state == "ACTIVE"))
                {
                    report.AddError("SAVE_FACILITY_NPC_LINK_INVALID", source, location + "/assignedNpcInstanceId", "Facility and NPC links must be bidirectional.");
                }
                if (managed && state == "STOPPED" && linked.Any(npc => npc.Value<bool>("working")))
                {
                    report.AddError("SAVE_FACILITY_STOPPED_INVALID", source, location, "STOPPED facilities cannot have a working NPC.");
                }
                return;
            }

            JObject npc = managementNpcs.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == assignedId);
            if (npc == null || linked.Length != 1 || linked[0] != npc ||
                (state == "ACTIVE" && !npc.Value<bool>("working")) ||
                (state != "ACTIVE" && npc.Value<bool>("working")))
            {
                report.AddError("SAVE_FACILITY_NPC_LINK_INVALID", source, location + "/assignedNpcInstanceId", "Facility and NPC links/working state must be bidirectional.", assignedId);
            }
        }

        private static void ValidateFacilityJobMatrix(
            JObject facility,
            ILookup<string, JObject> journalByOperationId,
            string source,
            string location,
            ValidationReport report)
        {
            string state = facility.Value<string>("state");
            if (facility["job"].Type == JTokenType.Null)
            {
                if (state is "BUILDING" or "UPGRADING")
                {
                    report.AddError("SAVE_FACILITY_STATE_JOB_MATRIX_INVALID", source, location + "/job", $"Facility state {state} requires a job.");
                }

                return;
            }

            JObject job = (JObject)facility["job"];
            string jobType = job.Value<string>("jobType");
            string status = job.Value<string>("status");
            JObject[] journalEntries = journalByOperationId[job.Value<string>("operationId")].ToArray();
            if (journalEntries.Length != 1 ||
                journalEntries[0].Value<string>("operationType") != "FACILITY_JOB" ||
                journalEntries[0].Value<string>("facilityJobType") != jobType)
            {
                report.AddError(
                    "SAVE_OPERATION_FACILITY_SUBTYPE_INVALID",
                    source,
                    location + "/job/operationId",
                    "Facility job must reference exactly one matching FACILITY_JOB journal entry.",
                    job.Value<string>("operationId"));
            }
            bool allowed = state switch
            {
                "BUILDABLE" => jobType == "BUILD" && status == "CANCELLED",
                "BUILDING" => jobType == "BUILD" && status is "RUNNING" or "READY",
                "UPGRADING" => jobType == "UPGRADE" && status is "RUNNING" or "READY",
                "ACTIVE" => (jobType == "BUILD" && status == "CLAIMED") ||
                            (jobType == "UPGRADE" && status is "CLAIMED" or "CANCELLED") ||
                            (jobType is "PRODUCTION" or "CRAFT" or "TREATMENT"),
                "STOPPED" => (jobType == "BUILD" && status == "CLAIMED") ||
                             (jobType == "UPGRADE" && status is "CLAIMED" or "CANCELLED"),
                _ => false
            };
            if (!allowed)
            {
                report.AddError("SAVE_FACILITY_STATE_JOB_MATRIX_INVALID", source, location + "/job", $"Combination {state}/{jobType}/{status} is forbidden.");
            }

            bool recipeExpected = jobType is "PRODUCTION" or "CRAFT";
            bool levelExpected = jobType is "BUILD" or "UPGRADE";
            bool treatmentExpected = jobType == "TREATMENT";
            if ((job["recipeId"].Type != JTokenType.Null) != recipeExpected ||
                (job["targetLevel"].Type != JTokenType.Null) != levelExpected ||
                (job["treatmentTargetInstanceId"].Type != JTokenType.Null) != treatmentExpected)
            {
                report.AddError("SAVE_FACILITY_JOB_UNION_INVALID", source, location + "/job", "Facility job discriminator fields do not match jobType.");
            }

            DateTimeOffset started = ParseUtc(job.Value<string>("startedAtUtc"));
            DateTimeOffset finishes = ParseUtc(job.Value<string>("finishesAtUtc"));
            if (finishes < started)
            {
                report.AddError("SAVE_FACILITY_JOB_TIME_INVALID", source, location + "/job/finishesAtUtc", "finishesAtUtc must be at or after startedAtUtc.");
            }

            bool hasClaimed = job["claimedAtUtc"].Type != JTokenType.Null;
            bool hasCancelled = job["cancelledAtUtc"].Type != JTokenType.Null;
            bool terminalTimesValid = status switch
            {
                "RUNNING" or "READY" => !hasClaimed && !hasCancelled,
                "CLAIMED" => hasClaimed && !hasCancelled && ParseUtc(job.Value<string>("claimedAtUtc")) >= finishes,
                "CANCELLED" => !hasClaimed && hasCancelled && ParseUtc(job.Value<string>("cancelledAtUtc")) >= started,
                _ => false
            };
            if (!terminalTimesValid)
            {
                report.AddError("SAVE_FACILITY_JOB_TERMINAL_TIME_INVALID", source, location + "/job", "Terminal timestamps do not match job status.");
            }

            if (status is "CLAIMED" or "CANCELLED")
            {
                string requiredJournalStatus = status == "CLAIMED" ? "COMMITTED" : "FAILED_PERMANENT";
                if (journalEntries.Length != 1 || journalEntries[0].Value<string>("status") != requiredJournalStatus)
                {
                    report.AddError("SAVE_FACILITY_TERMINAL_JOURNAL_MISMATCH", source, location + "/job/operationId", $"{status} requires journal status {requiredJournalStatus}.");
                }
            }
        }

        private static void ValidateCollections(JObject document, string source, ValidationReport report)
        {
            JToken payload = document["payload"];
            JObject profile = (JObject)payload["profile"];
            ValidateDisplayString(profile.Value<string>("nickname"), source, "/payload/profile/nickname", report);
            ValidateUnique(payload["mercenaries"], "instanceId", source, "/payload/mercenaries", "SAVE_MERCENARY_ID_DUPLICATE", report);
            ValidateUnique(payload["managementNpcs"], "instanceId", source, "/payload/managementNpcs", "SAVE_NPC_ID_DUPLICATE", report);
            ValidateUnique(payload["inventory"]["itemStacks"], "itemId", source, "/payload/inventory/itemStacks", "SAVE_ITEM_STACK_DUPLICATE", report);
            ValidateUnique(payload["inventory"]["equipment"], "instanceId", source, "/payload/inventory/equipment", "SAVE_EQUIPMENT_ID_DUPLICATE", report);
            ValidateUnique(payload["inventory"]["warehousePotions"], "potionId", source, "/payload/inventory/warehousePotions", "SAVE_POTION_STACK_DUPLICATE", report);
            ValidateUnique(payload["regions"]["progress"], "regionId", source, "/payload/regions/progress", "SAVE_REGION_PROGRESS_DUPLICATE", report);
            ValidateUniqueComposite(payload["regions"]["raids"], new[] { "raidId", "difficulty" }, source, "/payload/regions/raids", "SAVE_RAID_PROGRESS_KEY_DUPLICATE", report);
            ValidateUnique(payload["operationJournal"], "operationId", source, "/payload/operationJournal", "SAVE_OPERATION_ID_DUPLICATE", report);

            int activeCount = payload["mercenaries"].Count(item => item.Value<bool>("active"));
            if (activeCount > payload["kingdom"].Value<int>("activeMercenaryLimit"))
            {
                report.AddError("SAVE_ACTIVE_MERCENARY_LIMIT_EXCEEDED", source, "/payload/mercenaries", "Active mercenary count exceeds the persisted limit.");
            }

            int mercenaryIndex = 0;
            foreach (JObject mercenary in payload["mercenaries"].Children<JObject>())
            {
                ValidateDisplayString(
                    mercenary.Value<string>("displayName"),
                    source,
                    $"/payload/mercenaries/{mercenaryIndex.ToString(CultureInfo.InvariantCulture)}/displayName",
                    report);
                foreach (string field in new[] { "nameSeed", "appearanceSeed", "growthSeed" })
                {
                    if (!ulong.TryParse(mercenary.Value<string>(field), NumberStyles.None, CultureInfo.InvariantCulture, out _))
                    {
                        report.AddError(
                            "SAVE_SEED64_LEXICAL_INVALID",
                            source,
                            $"/payload/mercenaries/{mercenaryIndex.ToString(CultureInfo.InvariantCulture)}/{field}",
                            "Seed64 must be the canonical unsigned 64-bit decimal form.");
                    }
                }

                mercenaryIndex++;
            }

            foreach (JObject region in payload["regions"]["progress"].Children<JObject>())
            {
                if (region.Value<int>("progressPercent") > 0 && !region.Value<bool>("unlocked"))
                {
                    report.AddError("SAVE_REGION_PROGRESS_LOCKED", source, "/payload/regions/progress", "A locked region cannot contain progress.", region.Value<string>("regionId"));
                }

                bool hasUnlockTime = region["firstUnlockedAtUtc"].Type != JTokenType.Null;
                if (hasUnlockTime != region.Value<bool>("unlocked"))
                {
                    report.AddError("SAVE_REGION_UNLOCK_TIME_INVALID", source, "/payload/regions/progress", "firstUnlockedAtUtc must match unlocked state.", region.Value<string>("regionId"));
                }
            }
        }

        private static void ValidateEquipmentLinks(JObject document, string source, ValidationReport report)
        {
            JToken payload = document["payload"];
            var equipment = payload["inventory"]["equipment"].Children<JObject>()
                .ToDictionary(item => item.Value<string>("instanceId"), item => item, StringComparer.Ordinal);
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject mercenary in payload["mercenaries"].Children<JObject>())
            {
                string mercenaryId = mercenary.Value<string>("instanceId");
                foreach (JProperty slot in ((JObject)mercenary["equipmentSlots"]).Properties())
                {
                    if (slot.Value.Type == JTokenType.Null)
                    {
                        continue;
                    }

                    string equipmentId = slot.Value.Value<string>();
                    if (!equipment.TryGetValue(equipmentId, out JObject item) ||
                        !string.Equals(item.Value<string>("equippedByMercenaryInstanceId"), mercenaryId, StringComparison.Ordinal) ||
                        !occupied.Add(equipmentId))
                    {
                        report.AddError("SAVE_EQUIPMENT_LINK_INVALID", source, "/payload/mercenaries", "Equipment links must be bidirectional and exclusive.", equipmentId);
                    }
                }
            }

            foreach (JObject item in equipment.Values)
            {
                if (item["equippedByMercenaryInstanceId"].Type != JTokenType.Null && !occupied.Contains(item.Value<string>("instanceId")))
                {
                    report.AddError("SAVE_EQUIPMENT_LINK_INVALID", source, "/payload/inventory/equipment", "Equipped equipment must appear in exactly one mercenary slot.", item.Value<string>("instanceId"));
                }
            }
        }

        private static void ValidateRecruitment(JObject document, string source, ValidationReport report)
        {
            JArray requests = (JArray)document["payload"]["recruitmentMockState"]["pendingRequests"];
            ValidateUnique(requests, "operationId", source, "/payload/recruitmentMockState/pendingRequests", "SAVE_RECRUITMENT_OPERATION_DUPLICATE", report);
            ValidateUnique(requests, "requestHash", source, "/payload/recruitmentMockState/pendingRequests", "SAVE_RECRUITMENT_REQUEST_DUPLICATE", report);
            foreach (JObject request in requests.Children<JObject>())
            {
                string status = request.Value<string>("status");
                bool hasReceipt = request["serverReceiptId"].Type != JTokenType.Null;
                if ((status == "RECEIVED") != hasReceipt)
                {
                    report.AddError("SAVE_RECRUITMENT_PENDING_STATE_INVALID", source, "/payload/recruitmentMockState/pendingRequests", "Only RECEIVED requests must contain a server receipt.", request.Value<string>("operationId"));
                }
            }
        }

        private static void ValidateP13Recruitment(JObject document, string source, ValidationReport report)
        {
            if (document.Value<string>("contentVersion") is not ("1.0.0-content.11" or "1.0.0-content.12")) return;
            JObject state = (JObject)document["payload"]!["recruitmentMockState"]!;
            JArray candidates = (JArray)state["tavern"]!["candidates"]!;
            ValidateUnique(candidates, "candidateId", source, "/payload/recruitmentMockState/tavern/candidates", "P13_CANDIDATE_ID_DUPLICATE", report);
            if (candidates.Children<JObject>().Count(value => value.Value<bool>("locked")) > 2)
                report.AddError("P13_CANDIDATE_LOCK_LIMIT", source, "/payload/recruitmentMockState/tavern/candidates", "At most two tavern candidates may be locked.");
            JArray history = (JArray)state["history"]!;
            ValidateUnique(history, "operationId", source, "/payload/recruitmentMockState/history", "P13_HISTORY_OPERATION_DUPLICATE", report);
            long historySequence = Math.Max(1, state.Value<long>("nextHistorySequence") - history.Count);
            foreach (JObject value in history.Children<JObject>())
                if (value.Value<long>("sequence") != historySequence++)
                {
                    report.AddError("P13_HISTORY_SEQUENCE_INVALID", source, "/payload/recruitmentMockState/history", "Recruitment history must be contiguous and sorted.");
                    break;
                }
            if (historySequence != state.Value<long>("nextHistorySequence"))
                report.AddError("P13_HISTORY_SEQUENCE_INVALID", source, "/payload/recruitmentMockState/nextHistorySequence", "Recruitment nextHistorySequence must follow retained history.");
            JArray events = (JArray)state["events"]!;
            long eventSequence = Math.Max(1, state.Value<long>("nextEventSequence") - events.Count);
            foreach (JObject value in events.Children<JObject>())
                if (value.Value<long>("sequence") != eventSequence++)
                {
                    report.AddError("P13_EVENT_SEQUENCE_INVALID", source, "/payload/recruitmentMockState/events", "Recruitment events must be contiguous and sorted.");
                    break;
                }
            if (eventSequence != state.Value<long>("nextEventSequence"))
                report.AddError("P13_EVENT_SEQUENCE_INVALID", source, "/payload/recruitmentMockState/nextEventSequence", "Recruitment nextEventSequence must follow retained events.");
        }

        private static void ValidateJournal(JObject document, string source, ValidationReport report)
        {
            foreach (JObject entry in document["payload"]["operationJournal"].Children<JObject>())
            {
                string operationType = entry.Value<string>("operationType");
                bool hasFacilityType = entry["facilityJobType"].Type != JTokenType.Null;
                if ((operationType == "FACILITY_JOB") != hasFacilityType)
                {
                    report.AddError("SAVE_OPERATION_FACILITY_SUBTYPE_INVALID", source, "/payload/operationJournal", "facilityJobType must exist only for FACILITY_JOB.", entry.Value<string>("operationId"));
                }

                string status = entry.Value<string>("status");
                bool terminal = status is "COMMITTED" or "ACKNOWLEDGED" or "FAILED_PERMANENT";
                if (terminal != (entry["completedAtUtc"].Type != JTokenType.Null))
                {
                    report.AddError("SAVE_OPERATION_TERMINAL_TIME_INVALID", source, "/payload/operationJournal", "completedAtUtc must match terminal status.", entry.Value<string>("operationId"));
                }

                DateTimeOffset createdAt = ParseUtc(entry.Value<string>("createdAtUtc"));
                DateTimeOffset updatedAt = ParseUtc(entry.Value<string>("updatedAtUtc"));
                if (updatedAt < createdAt ||
                    (terminal && ParseUtc(entry.Value<string>("completedAtUtc")) < createdAt))
                {
                    report.AddError("SAVE_OPERATION_TIME_INVALID", source, "/payload/operationJournal", "Journal timestamps are out of order.", entry.Value<string>("operationId"));
                }

                bool hasResultDigest = entry["resultDigest"].Type != JTokenType.Null;
                if ((status is "COMMITTED" or "ACKNOWLEDGED") != hasResultDigest)
                {
                    report.AddError("SAVE_OPERATION_RESULT_DIGEST_INVALID", source, "/payload/operationJournal", "Only committed operations require resultDigest.", entry.Value<string>("operationId"));
                }

                bool hasResultPayload = entry.TryGetValue("resultPayload", out JToken resultPayload) && resultPayload.Type != JTokenType.Null;
                bool requiresResultPayload = (status is "COMMITTED" or "ACKNOWLEDGED") &&
                    (operationType is "STORE_TRANSACTION" or "EQUIPMENT_GROWTH_COMMAND" or "PROMOTION" or
                        "TAVERN_REFRESH" or "TAVERN_LOCK" or "TAVERN_HIRE" or "SPECIAL_RECRUIT" or "RAID_RESOLVE");
                if (hasResultPayload != requiresResultPayload)
                {
                    report.AddError("SAVE_OPERATION_RESULT_PAYLOAD_INVALID", source, "/payload/operationJournal", "Committed replayable commands require resultPayload.", entry.Value<string>("operationId"));
                }

                bool hasErrorCode = entry["errorCode"].Type != JTokenType.Null;
                if ((status == "FAILED_PERMANENT") != hasErrorCode)
                {
                    report.AddError("SAVE_OPERATION_ERROR_CODE_INVALID", source, "/payload/operationJournal", "Only FAILED_PERMANENT requires errorCode.", entry.Value<string>("operationId"));
                }

                bool hasResolution = entry["failureResolution"].Type != JTokenType.Null;
                bool hasResolvedAt = entry["resolvedAtUtc"].Type != JTokenType.Null;
                if (hasResolution != hasResolvedAt || (hasResolution && status != "FAILED_PERMANENT"))
                {
                    report.AddError("SAVE_OFFLINE_FAILURE_RESOLUTION_INVALID", source, "/payload/operationJournal", "Failure resolution fields must occur together on FAILED_PERMANENT entries.", entry.Value<string>("operationId"));
                }
            }
        }

        private static void ValidateEconomy(JObject document, string source, ValidationReport report)
        {
            if (document.Value<string>("contentVersion") is not ("1.0.0-content.6" or "1.0.0-content.7" or "1.0.0-content.8" or "1.0.0-content.9" or "1.0.0-content.10" or "1.0.0-content.11" or "1.0.0-content.12")) return;
            JObject economy = (JObject)document["payload"]?["economy"];
            if (economy == null) return;
            JObject store = (JObject)economy["store"];
            var stockIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject line in store["stackLines"].Children<JObject>())
            {
                string expected = $"STACK:{line.Value<string>("productKind")}:{line.Value<string>("productId")}:{line.Value<string>("sourceType")}";
                string actual = line.Value<string>("stockLineId");
                if (!string.Equals(expected, actual, StringComparison.Ordinal) || !stockIds.Add(actual))
                {
                    report.AddError("P08_STOCK_INVALID", source, "/payload/economy/store/stackLines", "Stock line identity is invalid or duplicated.", actual);
                }
            }

            var ownedEquipment = new HashSet<string>(document["payload"]!["inventory"]!["equipment"]!.Children<JObject>()
                .Select(value => value.Value<string>("instanceId")), StringComparer.Ordinal);
            foreach (JObject equipment in store["equipment"].Children<JObject>())
            {
                string instanceId = equipment.Value<string>("instanceId");
                if (!ownedEquipment.Add(instanceId))
                {
                    report.AddError("P08_EQUIPMENT_OWNERSHIP_CONFLICT", source, "/payload/economy/store/equipment", "Equipment must have exactly one owner.", instanceId);
                }
            }

            JObject facility = document["payload"]!["facilities"]!.Children<JObject>()
                .Single(value => value.Value<string>("facilityId") == "FAC_STORE");
            if (facility["storage"]!.Any())
            {
                report.AddError("P08_STORE_STORAGE_CONFLICT", source, "/payload/facilities", "FAC_STORE.storage must remain empty in P08.");
            }

            JObject ledger = (JObject)store["ledger"];
            long expectedSequence = ledger.Value<long>("prunedThroughSequence") + 1;
            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject entry in ledger["entries"].Children<JObject>())
            {
                if (entry.Value<long>("sequence") != expectedSequence++ || !operationIds.Add(entry.Value<string>("operationId")))
                {
                    report.AddError("P08_LEDGER_CORRUPT", source, "/payload/economy/store/ledger", "Ledger sequence or operation identity is invalid.");
                    break;
                }
                JObject journal = document["payload"]!["operationJournal"]!.Children<JObject>()
                    .SingleOrDefault(value => value.Value<string>("operationId") == entry.Value<string>("operationId"));
                string transactionType = entry.Value<string>("transactionType");
                string expectedOperationType = transactionType switch
                {
                    "ENHANCE_EQUIPMENT" or "REFINE_EQUIPMENT" => "EQUIPMENT_GROWTH_COMMAND",
                    "PROMOTION_START" => "PROMOTION",
                    "TAVERN_REFRESH" => "TAVERN_REFRESH",
                    "TAVERN_HIRE" => "TAVERN_HIRE",
                    _ => "STORE_TRANSACTION"
                };
                if (journal == null || journal.Value<string>("operationType") != expectedOperationType ||
                    journal.Value<string>("requestHash") != entry.Value<string>("requestHash") ||
                    journal.Value<string>("resultDigest") != entry.Value<string>("resultDigest"))
                {
                    report.AddError("P08_LEDGER_CORRUPT", source, "/payload/economy/store/ledger", "Ledger entry does not reconcile with the operation journal.", entry.Value<string>("operationId"));
                }
            }
            if (ledger.Value<long>("nextSequence") != expectedSequence)
            {
                report.AddError("P08_LEDGER_CORRUPT", source, "/payload/economy/store/ledger/nextSequence", "Ledger nextSequence is not contiguous.");
            }
        }

        private static void ValidateProduction(JObject document, string source, ValidationReport report)
        {
            if (document.Value<string>("contentVersion") is not ("1.0.0-content.7" or "1.0.0-content.8" or "1.0.0-content.9" or "1.0.0-content.10" or "1.0.0-content.11" or "1.0.0-content.12")) return;
            JObject production = document["payload"]?["production"] as JObject;
            if (production == null) return;
            JObject[] queues = production["facilityQueues"]!.Children<JObject>().ToArray();
            string[] expected = { "FAC_ALCHEMY", "FAC_BLACKSMITH", "FAC_INFIRMARY" };
            if (!queues.Select(value => value.Value<string>("facilityId")).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected))
                report.AddError("P09_FACILITY_QUEUE_SET_INVALID", source, "/payload/production/facilityQueues", "Production queues must cover exactly the three P09 facilities.");
            var jobIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject queue in queues.OrderBy(value => value.Value<string>("facilityId"), StringComparer.Ordinal))
            {
                long previousQueueNo = 0;
                foreach (JObject job in queue["jobs"]!.Children<JObject>())
                {
                    long queueNo = job.Value<long>("queueNo"); string jobId = job.Value<string>("jobId");
                    if (queueNo <= previousQueueNo || !jobIds.Add(jobId) || job.Value<long>("ticksRemaining") > job.Value<long>("ticksTotal"))
                        report.AddError("P09_QUEUE_INVALID", source, "/payload/production/facilityQueues", "Production job identity, order or tick range is invalid.", jobId);
                    previousQueueNo = queueNo;
                    bool craft = job.Value<string>("jobKind") == "CRAFT";
                    if ((job["recipeId"]!.Type != JTokenType.Null) != craft || (job["targetMercenaryInstanceId"]!.Type != JTokenType.Null) == craft)
                        report.AddError("P09_JOB_UNION_INVALID", source, "/payload/production/facilityQueues", "Production job discriminator fields are inconsistent.", jobId);
                }
            }
            var targetIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject target in production["stockTargets"]!.Children<JObject>())
                if (!targetIds.Add(target.Value<string>("targetId")) || target.Value<int>("targetQuantity") < target.Value<int>("minTarget") || target.Value<int>("targetQuantity") > target.Value<int>("maxTarget"))
                    report.AddError("P09_TARGET_INVALID", source, "/payload/production/stockTargets", "Production stock target is duplicated or out of range.", target.Value<string>("targetId"));
        }

        private static void ValidateEquipmentGrowth(JObject document, string source, ValidationReport report)
        {
            if (document.Value<string>("contentVersion") is not ("1.0.0-content.8" or "1.0.0-content.9" or "1.0.0-content.10" or "1.0.0-content.11" or "1.0.0-content.12")) return;
            JObject growth = document["payload"]?["equipmentGrowth"] as JObject;
            if (growth == null) return;
            long expectedSequence = Math.Max(1, growth.Value<long>("nextEventSequence") - growth["events"]!.Count());
            foreach (JObject value in growth["events"]!.Children<JObject>())
            {
                if (value.Value<long>("sequence") != expectedSequence++)
                {
                    report.AddError("P10_EVENT_SEQUENCE_INVALID", source, "/payload/equipmentGrowth/events", "Equipment growth events must be contiguous and sorted.");
                    break;
                }
            }
            if (expectedSequence != growth.Value<long>("nextEventSequence"))
                report.AddError("P10_EVENT_SEQUENCE_INVALID", source, "/payload/equipmentGrowth/nextEventSequence", "Equipment growth nextEventSequence must follow the final retained event.");

            foreach (JObject equipment in document["payload"]!["inventory"]!["equipment"]!.Children<JObject>()
                         .Concat(document["payload"]!["economy"]!["store"]!["equipment"]!.Children<JObject>()))
            {
                string[] investments = equipment["enhancementMaterialInvested"]!.Children<JObject>().Select(value => value.Value<string>("itemId")).ToArray();
                if (!investments.SequenceEqual(investments.OrderBy(value => value, StringComparer.Ordinal)) || investments.Distinct(StringComparer.Ordinal).Count() != investments.Length)
                    report.AddError("P10_INVESTMENT_INVALID", source, "/payload/inventory/equipment", "Enhancement material investments must be unique and sorted.", equipment.Value<string>("instanceId"));
                if (equipment["pendingRefineOption"]!.Type != JTokenType.Null && equipment["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null)
                    report.AddError("P10_PENDING_REFINE_EQUIPPED", source, "/payload/inventory/equipment", "Equipment with a pending refine candidate cannot be equipped.", equipment.Value<string>("instanceId"));
            }
        }

        private static void ValidateProgression(JObject document, string source, ValidationReport report)
        {
            if (document.Value<string>("contentVersion") is not ("1.0.0-content.9" or "1.0.0-content.10" or "1.0.0-content.11" or "1.0.0-content.12")) return;
            JObject progression = document["payload"]?["progression"] as JObject;
            if (progression == null) return;
            long expectedSequence = Math.Max(1, progression.Value<long>("nextEventSequence") - progression["events"]!.Count());
            foreach (JObject value in progression["events"]!.Children<JObject>())
            {
                if (value.Value<long>("sequence") != expectedSequence++)
                {
                    report.AddError("P11_EVENT_SEQUENCE_INVALID", source, "/payload/progression/events", "Progression events must be contiguous and sorted.");
                    break;
                }
            }
            if (expectedSequence != progression.Value<long>("nextEventSequence"))
                report.AddError("P11_EVENT_SEQUENCE_INVALID", source, "/payload/progression/nextEventSequence", "Progression nextEventSequence must follow the final retained event.");
            string[] keys = progression["issuedSupplyKeys"]!.Values<string>().ToArray();
            if (!keys.SequenceEqual(keys.OrderBy(value => value, StringComparer.Ordinal)) || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
                report.AddError("P11_SUPPLY_KEY_INVALID", source, "/payload/progression/issuedSupplyKeys", "Promotion supply keys must be unique and sorted.");
        }

        private static void ValidateRegions(JObject document, string source, ValidationReport report)
        {
            if (document.Value<string>("contentVersion") is not ("1.0.0-content.10" or "1.0.0-content.11" or "1.0.0-content.12")) return;
            JObject regions = document["payload"]?["regions"] as JObject;
            if (regions == null) return;
            string[] expected = Enumerable.Range(1, 5).Select(value => $"REGION_R0{value}").ToArray();
            string[] progressIds = regions["progress"]!.Children<JObject>().Select(value => value.Value<string>("regionId")).ToArray();
            string[] policyIds = document["payload"]!["kingdom"]!["regionAccessPolicies"]!.Children<JObject>().Select(value => value.Value<string>("regionId")).ToArray();
            if (!progressIds.SequenceEqual(expected))
                report.AddError("P12_REGION_SET_INVALID", source, "/payload/regions/progress", "Region progress must cover R01 through R05 in canonical order.");
            if (!policyIds.SequenceEqual(expected))
                report.AddError("P12_REGION_POLICY_SET_INVALID", source, "/payload/kingdom/regionAccessPolicies", "Region policies must cover R01 through R05 in canonical order.");
            long expectedSequence = Math.Max(1, regions.Value<long>("nextEventSequence") - regions["events"]!.Count());
            foreach (JObject value in regions["events"]!.Children<JObject>())
            {
                if (value.Value<long>("sequence") != expectedSequence++)
                {
                    report.AddError("P12_EVENT_SEQUENCE_INVALID", source, "/payload/regions/events", "Region events must be contiguous and sorted.");
                    break;
                }
                if (!expected.Contains(value.Value<string>("regionId"), StringComparer.Ordinal))
                    report.AddError("P12_EVENT_REGION_INVALID", source, "/payload/regions/events", "Region event references an unknown region.", value.Value<string>("regionId"));
            }
            if (expectedSequence != regions.Value<long>("nextEventSequence"))
                report.AddError("P12_EVENT_SEQUENCE_INVALID", source, "/payload/regions/nextEventSequence", "Region nextEventSequence must follow the final retained event.");
        }

        private static void ValidateP14Raids(JObject document, string source, ValidationReport report)
        {
            if (document.Value<string>("contentVersion") != "1.0.0-content.12") return;
            JObject regions = (JObject)document["payload"]!["regions"]!;
            string[] expectedKeys =
            {
                "RAID_HYDRA|NORMAL", "RAID_HYDRA|HARD", "RAID_HYDRA|CORRUPTED",
                "RAID_DRAGON|NORMAL", "RAID_DRAGON|HARD", "RAID_DRAGON|CORRUPTED"
            };
            JObject[] raids = regions["raids"]!.Children<JObject>().ToArray();
            string[] keys = raids.Select(value => value.Value<string>("raidId") + "|" + value.Value<string>("difficulty")).ToArray();
            if (!keys.SequenceEqual(expectedKeys))
                report.AddError("P14_RAID_SET_INVALID", source, "/payload/regions/raids", "Raid progress must cover two raids and three difficulties in canonical order.");
            foreach (JObject raid in raids)
            {
                string[] partIds = raid["partStates"]!.Children<JObject>().Select(value => value.Value<string>("partId")).ToArray();
                if (partIds.Distinct(StringComparer.Ordinal).Count() != partIds.Length)
                    report.AddError("P14_RAID_PART_DUPLICATE", source, "/payload/regions/raids/partStates", "Raid part progress must be unique.");
                bool hasFirstReward = raid["firstClearRewardOperationId"]!.Type != JTokenType.Null;
                if (hasFirstReward && (raid.Value<string>("difficulty") != "NORMAL" || raid.Value<long>("clearCount") == 0))
                    report.AddError("P14_FIRST_CLEAR_REWARD_INVALID", source, "/payload/regions/raids", "Only a cleared NORMAL raid may carry the official first-clear reward operation.");
                if (raid.Value<long>("attemptCount") < raid.Value<long>("clearCount"))
                    report.AddError("P14_ATTEMPT_COUNT_INVALID", source, "/payload/regions/raids", "Raid attempts cannot be lower than clears.");
            }
            JArray history = (JArray)regions["raidHistory"]!;
            ValidateUnique(history, "operationId", source, "/payload/regions/raidHistory", "P14_HISTORY_OPERATION_DUPLICATE", report);
            long expectedSequence = Math.Max(1, regions.Value<long>("nextRaidHistorySequence") - history.Count);
            foreach (JObject entry in history.Children<JObject>())
            {
                if (entry.Value<long>("sequence") != expectedSequence++)
                {
                    report.AddError("P14_HISTORY_SEQUENCE_INVALID", source, "/payload/regions/raidHistory", "Raid history must be contiguous and sorted.");
                    break;
                }
                if (!entry.Value<bool>("success") && entry["rewards"]!.Any())
                    report.AddError("P14_FAILURE_REWARD_INVALID", source, "/payload/regions/raidHistory", "Failed raids cannot grant rewards.", entry.Value<string>("operationId"));
                JObject journal = document["payload"]!["operationJournal"]!.Children<JObject>()
                    .SingleOrDefault(value => value.Value<string>("operationId") == entry.Value<string>("operationId"));
                if (journal == null || journal.Value<string>("operationType") != "RAID_RESOLVE" ||
                    journal.Value<string>("resultDigest") != entry.Value<string>("resultDigest"))
                    report.AddError("P14_HISTORY_JOURNAL_MISMATCH", source, "/payload/regions/raidHistory", "Raid history must reconcile with its operation journal.", entry.Value<string>("operationId"));
            }
            if (expectedSequence != regions.Value<long>("nextRaidHistorySequence"))
                report.AddError("P14_HISTORY_SEQUENCE_INVALID", source, "/payload/regions/nextRaidHistorySequence", "Raid next history sequence must follow retained history.");
        }

        private static void ValidateRewardSnapshots(JObject document, string source, ValidationReport report)
        {
            foreach (JObject reward in document.SelectTokens("$..outputSnapshot[*]").OfType<JObject>()
                         .Concat(document.SelectTokens("$..rewards[*]").OfType<JObject>()))
            {
                string rewardType = reward.Value<string>("rewardType");
                bool hasSnapshot = reward.TryGetValue("generatedEquipmentSnapshot", out JToken snapshotToken) && snapshotToken.Type != JTokenType.Null;
                if (EquipmentRewardTypes.Contains(rewardType) != hasSnapshot)
                {
                    report.AddError(
                        hasSnapshot ? "SAVE_EQUIPMENT_SNAPSHOT_FORBIDDEN" : "SAVE_EQUIPMENT_SNAPSHOT_REQUIRED",
                        source,
                        "/payload",
                        "Generated equipment snapshot presence must match rewardType.",
                        reward.Value<string>("rewardId"));
                    continue;
                }

                if (!hasSnapshot)
                {
                    continue;
                }

                JObject snapshot = (JObject)reward["generatedEquipmentSnapshot"];
                if (reward.Value<int>("quantity") != 1 ||
                    reward.Value<string>("destination") != "INVENTORY" ||
                    reward["targetId"].Type != JTokenType.Null ||
                    (rewardType == "EQUIPMENT_TEMPLATE" && reward.Value<string>("rewardId") != snapshot.Value<string>("equipmentTemplateId")))
                {
                    report.AddError("SAVE_REWARD_SNAPSHOT_UNION_INVALID", source, "/payload", "Equipment reward discriminator fields are inconsistent.", reward.Value<string>("rewardId"));
                }
            }
        }

        private static void ValidateUnique(
            JToken array,
            string field,
            string source,
            string location,
            string code,
            ValidationReport report)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject value in array.Children<JObject>())
            {
                string key = value[field]?.ToString(Newtonsoft.Json.Formatting.None) ?? "null";
                if (!seen.Add(key))
                {
                    report.AddError(code, source, location, $"Duplicate {field}.", key);
                }
            }
        }

        private static void ValidateDisplayString(
            string value,
            string source,
            string location,
            ValidationReport report)
        {
            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) || value.Any(char.IsControl))
            {
                report.AddError("SAVE_DISPLAY_STRING_INVALID", source, location, "Display text must be trimmed and contain no control characters.");
            }
        }

        private static void ValidateUniqueComposite(
            JToken array,
            IReadOnlyList<string> fields,
            string source,
            string location,
            string code,
            ValidationReport report)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject value in array.Children<JObject>())
            {
                string key = string.Join("\u001f", fields.Select(field => value[field]?.ToString(Newtonsoft.Json.Formatting.None) ?? "null"));
                if (!seen.Add(key))
                {
                    report.AddError(code, source, location, "Duplicate composite key.", key);
                }
            }
        }

        private static DateTimeOffset ParseUtc(string value)
        {
            return DateTimeOffset.ParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }
}
