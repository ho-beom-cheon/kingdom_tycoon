using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Facilities;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Application.Profiles;
using KingdomTycoon.Domain.Facilities;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Content.Migrations;
using KingdomTycoon.Infrastructure.Economy;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Infrastructure.Production;
using KingdomTycoon.Infrastructure.EquipmentGrowth;
using KingdomTycoon.Infrastructure.Progression;
using KingdomTycoon.Infrastructure.Regions;
using KingdomTycoon.Infrastructure.Recruitment;
using KingdomTycoon.Infrastructure.Raids;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Facilities
{
    public sealed class FacilityGameService : IAppService
    {
        private readonly ITrustedUtcClock clock;
        private readonly string newGameTemplateJson;
        private readonly string p04MigrationTemplateJson;
        private readonly bool p05Enabled;
        private readonly bool p06Enabled;
        private readonly bool p07Enabled;
        private readonly bool p08Enabled;
        private readonly bool p09Enabled;
        private readonly bool p10Enabled;
        private readonly bool p11Enabled;
        private readonly bool p12Enabled;
        private readonly bool p13Enabled;
        private readonly bool p14Enabled;
        private readonly SemaphoreSlim commitGate = new(1, 1);
        private SaveService saveService;
        private ContentCatalogService contentService;
        private CanonicalFacilityCatalog catalog;
        private DateTimeOffset lastTrustedUtc;

        public FacilityGameService(ITrustedUtcClock clock, string newGameTemplateJson, string p04MigrationTemplateJson = null)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.newGameTemplateJson = newGameTemplateJson ?? throw new ArgumentNullException(nameof(newGameTemplateJson));
            p05Enabled = p04MigrationTemplateJson != null;
            p06Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P06ContentVersion;
            p07Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P07ContentVersion;
            p08Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P08ContentVersion;
            p09Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P09ContentVersion;
            p10Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P10ContentVersion;
            p11Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P11ContentVersion;
            p12Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P12ContentVersion;
            p13Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P13ContentVersion;
            p14Enabled = StrictJson.ParseObject(newGameTemplateJson).Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P14ContentVersion;
            this.p04MigrationTemplateJson = p04MigrationTemplateJson ?? newGameTemplateJson;
        }

        public int InitializationOrder => 50;
        public bool IsBootstrapped { get; private set; }
        public bool WasRecovered { get; private set; }
        public string ActiveProfileId { get; private set; }
        public long Revision => CurrentDocument?.Value<long>("revision") ?? 0;
        public JObject CurrentDocument { get; private set; }
        public CanonicalFacilityCatalog Catalog => catalog;

        public void Initialize(ServiceRegistry services)
        {
            saveService = services.Get<SaveService>();
            contentService = services.Get<ContentCatalogService>();
        }

        public async Task BootstrapAsync(CancellationToken cancellationToken)
        {
            if (contentService.Catalog == null) throw new InvalidOperationException("CONTENT_ACTIVE_PACKAGE_NOT_LOADED");
            catalog = new CanonicalFacilityCatalog(contentService.Catalog);
            var locator = new LocalProfileLocator(saveService.PersistentDataPath, saveService.Repository);
            ProfileLocateResult located = await locator.LocateAsync(cancellationToken);
            if (located.Kind == ProfileLocateKind.AMBIGUOUS)
            {
                throw new InvalidOperationException("SAVE_PROFILE_SELECTION_REQUIRED");
            }

            if (located.Kind == ProfileLocateKind.NONE)
            {
                if (saveService.Repository is not AtomicSaveRepository atomicRepository)
                    throw new InvalidOperationException("SAVE_CREATE_REPOSITORY_UNSUPPORTED");
                INewGameFactory factory = p14Enabled ? new P14NewGameFactory(newGameTemplateJson) : p13Enabled ? new P13NewGameFactory(newGameTemplateJson) : p12Enabled ? new P12NewGameFactory(newGameTemplateJson) : p11Enabled ? new P11NewGameFactory(newGameTemplateJson) : p10Enabled ? new P10NewGameFactory(newGameTemplateJson) : p09Enabled ? new P09NewGameFactory(newGameTemplateJson) : p08Enabled ? new P08NewGameFactory(newGameTemplateJson) : p07Enabled ? new P07NewGameFactory(newGameTemplateJson) : p06Enabled ? new P06NewGameFactory(newGameTemplateJson) : p05Enabled ? new P05NewGameFactory(newGameTemplateJson) : new P04NewGameFactory(newGameTemplateJson);
                SaveLoadResult created = new SingleProfileCreator(saveService.PersistentDataPath, atomicRepository, saveService.Validator)
                    .CreateOrResume(factory, clock.UtcNow);
                if (!created.Success) throw new InvalidOperationException(created.ErrorCode ?? "SAVE_CREATE_FAILED");
                ActiveProfileId = created.Document.Value<string>("profileId");
                CurrentDocument = created.Document;
            }
            else
            {
                ActiveProfileId = located.ProfileId;
                SaveLoadResult loaded = saveService.Repository.Load(ActiveProfileId);
                if (!loaded.Success) throw new InvalidOperationException(loaded.ErrorCode ?? "SAVE_LOAD_FAILED");
                CurrentDocument = loaded.Document;
                WasRecovered = loaded.Recovered;
            }

            if (CurrentDocument.Value<string>("contentVersion") == "1.0.0-content.1")
            {
                var migration = new P03ToP04ContentMigration(p04MigrationTemplateJson);
                JObject migrated = migration.CanApply(CurrentDocument)
                    ? migration.Apply(CurrentDocument)
                    : (JObject)CurrentDocument.DeepClone();
                migrated["contentVersion"] = CompileTimeActiveContentVersionProvider.P04ContentVersion;
                Commit(migrated, Revision, clock.UtcNow);
            }
            else if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P06ContentVersion && p07Enabled)
            {
                JObject migrated = new P06ToP07ContentMigration(clock).Apply(CurrentDocument);
                Commit(migrated, Revision, clock.UtcNow);
            }
            else if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P07ContentVersion && p08Enabled)
            {
                JObject migrated = new P07ToP08ContentMigration().Apply(CurrentDocument);
                Commit(migrated, Revision, clock.UtcNow);
            }
            else if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P08ContentVersion && (p09Enabled || p10Enabled || p11Enabled || p12Enabled || p13Enabled || p14Enabled))
            {
                JObject migrated = new P08ToP09ContentMigration().Apply(CurrentDocument, new P09ProductionCatalog(contentService.Catalog).DefaultTargets());
                Commit(migrated, Revision, clock.UtcNow);
            }
            if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P09ContentVersion && (p10Enabled || p11Enabled || p12Enabled || p13Enabled || p14Enabled))
            {
                JObject migrated = new P09ToP10ContentMigration().Apply(CurrentDocument);
                Commit(migrated, Revision, clock.UtcNow);
            }
            if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P10ContentVersion && (p11Enabled || p12Enabled || p13Enabled || p14Enabled))
            {
                JObject migrated = new P10ToP11ContentMigration().Apply(CurrentDocument);
                Commit(migrated, Revision, clock.UtcNow);
            }
            if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P11ContentVersion && (p12Enabled || p13Enabled || p14Enabled))
            {
                JObject migrated = new P11ToP12ContentMigration().Apply(CurrentDocument);
                Commit(migrated, Revision, clock.UtcNow);
            }
            if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P12ContentVersion && (p13Enabled || p14Enabled))
            {
                JObject migrated = new P12ToP13ContentMigration().Apply(CurrentDocument);
                Commit(migrated, Revision, clock.UtcNow);
            }
            if (CurrentDocument.Value<string>("contentVersion") == CompileTimeActiveContentVersionProvider.P13ContentVersion && p14Enabled)
            {
                JObject migrated = new P13ToP14ContentMigration().Apply(CurrentDocument);
                Commit(migrated, Revision, clock.UtcNow);
            }
            else if (CurrentDocument.Value<string>("contentVersion") is not (CompileTimeActiveContentVersionProvider.P04ContentVersion or CompileTimeActiveContentVersionProvider.P05ContentVersion or CompileTimeActiveContentVersionProvider.P06ContentVersion or CompileTimeActiveContentVersionProvider.P07ContentVersion or CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion or CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion))
            {
                throw new InvalidOperationException("SAVE_CONTENT_VERSION_UNSUPPORTED");
            }

            lastTrustedUtc = clock.UtcNow;
            NormalizeAndCleanup(clock.UtcNow, false);
            IsBootstrapped = true;
        }

        public JObject Snapshot() => CurrentDocument == null ? null : (JObject)CurrentDocument.DeepClone();

        public void SynchronizeCommittedDocument(JObject document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.Value<string>("profileId") != ActiveProfileId) throw new InvalidOperationException("SAVE_PROFILE_ID_MISMATCH");
            CurrentDocument = (JObject)document.DeepClone();
        }

        public FacilityOperationResult StartBuild(StartFacilityBuildCommand command) => Mutate(command, now =>
        {
            JObject facility = FindFacility(command.FacilityId);
            Require(facility.Value<string>("state") == "BUILDABLE", "FACILITY_STATE_INVALID");
            Require(command.TargetLevel == 1, "FACILITY_STATE_INVALID");
            FacilityLevelDefinition level = catalog.GetLevel(command.FacilityId, 1);
            Require(catalog.IsStageAvailable(CurrentStageId(), level.StageId), "FACILITY_STAGE_REQUIRED");
            JArray input = Debit(level);
            DateTimeOffset finishes = now.AddSeconds(level.DurationSeconds);
            facility["state"] = "BUILDING";
            facility["assignedNpcInstanceId"] = null;
            facility["job"] = NewJob(command.OperationId, "BUILD", 1, now, finishes, input);
            string digest = ResultDigest("START_FACILITY_BUILD", command.FacilityId, command.OperationId, 1, "BUILDING", input);
            AppendJournal(command.OperationId, command.RequestHash, "BUILD", now, digest);
            return new MutationResult(digest, true);
        });

        public FacilityOperationResult StartUpgrade(StartFacilityUpgradeCommand command) => Mutate(command, now =>
        {
            JObject facility = FindFacility(command.FacilityId);
            string state = facility.Value<string>("state");
            Require(state is "ACTIVE" or "STOPPED", "FACILITY_STATE_INVALID");
            int currentLevel = facility.Value<int>("level");
            Require(currentLevel < 4, "FACILITY_LEVEL_MAX");
            Require(command.TargetLevel == currentLevel + 1, "FACILITY_STATE_INVALID");
            FacilityLevelDefinition level = catalog.GetLevel(command.FacilityId, command.TargetLevel);
            Require(catalog.IsStageAvailable(CurrentStageId(), level.StageId), "FACILITY_STAGE_REQUIRED");
            JArray input = Debit(level);
            DateTimeOffset finishes = now.AddSeconds(level.DurationSeconds);
            facility["state"] = "UPGRADING";
            SetNpcWorking(facility, false);
            facility["job"] = NewJob(command.OperationId, "UPGRADE", command.TargetLevel, now, finishes, input);
            string digest = ResultDigest("START_FACILITY_UPGRADE", command.FacilityId, command.OperationId, command.TargetLevel, "UPGRADING", input);
            AppendJournal(command.OperationId, command.RequestHash, "UPGRADE", now, digest);
            return new MutationResult(digest, true);
        });

        public FacilityOperationResult Claim(ClaimFacilityJobCommand command) => Mutate(command, now =>
        {
            JObject facility = FindFacility(command.FacilityId);
            if (facility["job"].Type == JTokenType.Null) throw new FacilityCommandException("FACILITY_JOB_NOT_FOUND");
            JObject job = (JObject)facility["job"];
            Require(job.Value<string>("operationId") == command.FacilityJobOperationId.ToString("D"), "FACILITY_JOB_NOT_FOUND");
            Require(job.Value<string>("status") == "READY", "FACILITY_JOB_NOT_READY");
            string jobType = job.Value<string>("jobType");
            Require(jobType is "BUILD" or "UPGRADE", "FACILITY_STATE_INVALID");
            int targetLevel = job.Value<int>("targetLevel");
            job["status"] = "CLAIMED";
            job["claimedAtUtc"] = FormatUtc(now);
            facility["level"] = targetLevel;
            FacilityDefinition definition = catalog.GetFacility(command.FacilityId);
            bool canActivate = !definition.IsManaged || HasMatchingNpc(facility, definition);
            facility["state"] = canActivate ? "ACTIVE" : "STOPPED";
            SetNpcWorking(facility, canActivate && definition.IsManaged);
            if (jobType == "UPGRADE")
            {
                JObject kingdom = (JObject)CurrentDocument["payload"]["kingdom"];
                kingdom["facilityUpgradeCount"] = kingdom.Value<long>("facilityUpgradeCount") + 1;
            }
            ApplyLodgeLimits(facility);
            string digest = ResultDigest("CLAIM_FACILITY_JOB", command.FacilityId, command.FacilityJobOperationId, targetLevel, facility.Value<string>("state"), (JArray)job["inputSnapshot"]);
            JObject journal = FindJournal(command.FacilityJobOperationId);
            journal["status"] = "COMMITTED";
            journal["updatedAtUtc"] = FormatUtc(now);
            journal["completedAtUtc"] = FormatUtc(now);
            journal["resultDigest"] = digest;
            return new MutationResult(digest, true);
        });

        public FacilityOperationResult Assign(AssignManagementNpcCommand command) => Mutate(command, now =>
        {
            JObject facility = FindFacility(command.FacilityId);
            FacilityDefinition definition = catalog.GetFacility(command.FacilityId);
            Require(definition.IsManaged, "FACILITY_NPC_FORBIDDEN");
            Require(facility.Value<string>("state") != "BUILDABLE" && facility.Value<string>("state") != "LOCKED", "FACILITY_NOT_BUILT");
            Require(facility.Value<string>("state") != "BUILDING" && facility.Value<string>("state") != "UPGRADING", "FACILITY_JOB_ALREADY_RUNNING");
            JObject npc = FindNpc(command.NpcInstanceId);
            Require(npc.Value<string>("professionId") == definition.RequiredProfessionId, "FACILITY_NPC_PROFESSION_MISMATCH");
            string currentFacility = npc["assignedFacilityId"].Type == JTokenType.Null ? null : npc.Value<string>("assignedFacilityId");
            string assigned = facility["assignedNpcInstanceId"].Type == JTokenType.Null ? null : facility.Value<string>("assignedNpcInstanceId");
            if (currentFacility == command.FacilityId && assigned == command.NpcInstanceId.ToString("D") && facility.Value<string>("state") == "ACTIVE")
            {
                return new MutationResult(null, false);
            }
            Require(currentFacility == null && assigned == null, "FACILITY_NPC_ALREADY_ASSIGNED");
            facility["assignedNpcInstanceId"] = command.NpcInstanceId.ToString("D");
            facility["state"] = "ACTIVE";
            npc["assignedFacilityId"] = command.FacilityId;
            npc["working"] = true;
            return new MutationResult(null, true);
        });

        public FacilityOperationResult Unassign(UnassignManagementNpcCommand command) => Mutate(command, now =>
        {
            JObject facility = FindFacility(command.FacilityId);
            FacilityDefinition definition = catalog.GetFacility(command.FacilityId);
            Require(definition.IsManaged, "FACILITY_NPC_FORBIDDEN");
            Require(facility.Value<string>("state") != "BUILDABLE" && facility.Value<string>("state") != "LOCKED", "FACILITY_NOT_BUILT");
            Require(facility.Value<string>("state") != "BUILDING" && facility.Value<string>("state") != "UPGRADING", "FACILITY_JOB_ALREADY_RUNNING");
            if (facility["assignedNpcInstanceId"].Type == JTokenType.Null)
            {
                return new MutationResult(null, false);
            }
            Require(facility.Value<string>("assignedNpcInstanceId") == command.NpcInstanceId.ToString("D"), "FACILITY_NPC_ALREADY_ASSIGNED");
            JObject npc = FindNpc(command.NpcInstanceId);
            facility["assignedNpcInstanceId"] = null;
            facility["state"] = "STOPPED";
            npc["assignedFacilityId"] = null;
            npc["working"] = false;
            return new MutationResult(null, true);
        });

        public bool NormalizeExpiredJobs()
        {
            EnsureReady();
            return NormalizeAndCleanup(ReadTrustedNow(), false);
        }

        public void Shutdown()
        {
            commitGate.Dispose();
            CurrentDocument = null;
            catalog = null;
            IsBootstrapped = false;
        }

        private FacilityOperationResult Mutate(FacilityCommand command, Func<DateTimeOffset, MutationResult> mutation)
        {
            EnsureReady();
            commitGate.Wait();
            try
            {
                DateTimeOffset now = ReadTrustedNow();
                VerifyCommand(command);
                JObject replay = FindJournalOrNull(command.OperationId);
                if (replay != null)
                {
                    Require(FixedTimeEquals(replay.Value<string>("requestHash"), command.RequestHash), "FACILITY_OPERATION_HASH_MISMATCH");
                    return new FacilityOperationResult(command.OperationId, Revision, command.FacilityId, replay.Value<string>("resultDigest"), true);
                }

                long expectedRevision = Revision;
                JObject before = CurrentDocument;
                CurrentDocument = (JObject)before.DeepClone();
                try
                {
                    CleanupTerminalJobs();
                    MutationResult result = mutation(now);
                    if (result.Changed || !JToken.DeepEquals(before, CurrentDocument))
                    {
                        Commit(CurrentDocument, expectedRevision, now);
                    }
                    else
                    {
                        CurrentDocument = before;
                    }
                    return new FacilityOperationResult(command.OperationId, Revision, command.FacilityId, result.Digest, false);
                }
                catch
                {
                    CurrentDocument = before;
                    throw;
                }
            }
            finally
            {
                commitGate.Release();
            }
        }

        private bool NormalizeAndCleanup(DateTimeOffset now, bool cleanup)
        {
            JObject before = CurrentDocument;
            var draft = (JObject)before.DeepClone();
            CurrentDocument = draft;
            bool changed = cleanup && CleanupTerminalJobs();
            foreach (JObject facility in CurrentDocument["payload"]["facilities"].Children<JObject>())
            {
                if (facility["job"].Type == JTokenType.Null) continue;
                JObject job = (JObject)facility["job"];
                if (job.Value<string>("status") == "RUNNING" && ParseUtc(job.Value<string>("finishesAtUtc")) <= now)
                {
                    job["status"] = "READY";
                    changed = true;
                }
            }
            if (changed)
            {
                Commit(CurrentDocument, before.Value<long>("revision"), now);
                return true;
            }
            CurrentDocument = before;
            return false;
        }

        private bool CleanupTerminalJobs()
        {
            bool changed = false;
            foreach (JObject facility in CurrentDocument["payload"]["facilities"].Children<JObject>())
            {
                if (facility["job"].Type == JTokenType.Null) continue;
                string status = facility["job"].Value<string>("status");
                if (status is "CLAIMED" or "CANCELLED")
                {
                    facility["job"] = null;
                    changed = true;
                }
            }
            return changed;
        }

        private JArray Debit(FacilityLevelDefinition level)
        {
            JObject kingdom = (JObject)CurrentDocument["payload"]["kingdom"];
            Require(kingdom.Value<long>("kingdomGold") >= level.Gold, "FACILITY_GOLD_INSUFFICIENT");
            var stacks = CurrentDocument["payload"]["inventory"]["itemStacks"].Children<JObject>()
                .ToDictionary(value => value.Value<string>("itemId"), value => value, StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> material in level.Materials)
            {
                Require(stacks.TryGetValue(material.Key, out JObject stack) && stack.Value<long>("quantity") >= material.Value, "FACILITY_MATERIAL_INSUFFICIENT");
            }

            var snapshot = new JArray();
            if (level.Gold > 0)
            {
                kingdom["kingdomGold"] = kingdom.Value<long>("kingdomGold") - level.Gold;
                snapshot.Add(AssetAmount("KINGDOM_GOLD", "SYSTEM_KINGDOM_GOLD", level.Gold));
            }
            foreach (KeyValuePair<string, long> material in level.Materials.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                JObject stack = stacks[material.Key];
                long remaining = stack.Value<long>("quantity") - material.Value;
                if (remaining == 0) stack.Remove(); else stack["quantity"] = remaining;
                snapshot.Add(AssetAmount("ITEM", material.Key, material.Value));
            }
            return snapshot;
        }

        private void AppendJournal(Guid operationId, string requestHash, string jobType, DateTimeOffset now, string resultDigest)
        {
            ((JArray)CurrentDocument["payload"]["operationJournal"]).Add(new JObject
            {
                ["operationId"] = operationId.ToString("D"),
                ["operationType"] = "FACILITY_JOB",
                ["facilityJobType"] = jobType,
                ["requestHash"] = requestHash,
                ["status"] = "COMMITTED",
                ["createdAtUtc"] = FormatUtc(now),
                ["updatedAtUtc"] = FormatUtc(now),
                ["completedAtUtc"] = FormatUtc(now),
                ["serverReceiptId"] = null,
                ["errorCode"] = null,
                ["resultDigest"] = resultDigest,
                ["failureResolution"] = null,
                ["resolvedAtUtc"] = null
            });
        }

        private string ResultDigest(string command, string facilityId, Guid facilityJobOperationId, int targetLevel, string nextState, JArray inputSnapshot)
        {
            return Rfc8785Canonicalizer.ComputeSha256(new JObject
            {
                ["command"] = command,
                ["facilityId"] = facilityId,
                ["facilityJobOperationId"] = facilityJobOperationId.ToString("D"),
                ["targetLevel"] = targetLevel,
                ["nextState"] = nextState,
                ["inputSnapshot"] = inputSnapshot.DeepClone(),
                ["facilityUpgradeCountAfter"] = CurrentDocument["payload"]["kingdom"].Value<long>("facilityUpgradeCount")
            });
        }

        private static JObject NewJob(Guid operationId, string jobType, int level, DateTimeOffset started, DateTimeOffset finishes, JArray input) => new()
        {
            ["operationId"] = operationId.ToString("D"), ["jobType"] = jobType, ["status"] = "RUNNING",
            ["recipeId"] = null, ["targetLevel"] = level, ["treatmentTargetInstanceId"] = null,
            ["contentVersion"] = CompileTimeActiveContentVersionProvider.P06ContentVersion,
            ["startedAtUtc"] = FormatUtc(started), ["finishesAtUtc"] = FormatUtc(finishes),
            ["claimedAtUtc"] = null, ["cancelledAtUtc"] = null, ["cycleCount"] = 1,
            ["inputSnapshot"] = input, ["outputSnapshot"] = new JArray()
        };

        private static JObject AssetAmount(string type, string id, long quantity) => new()
        {
            ["assetType"] = type, ["assetId"] = id, ["quantity"] = quantity, ["targetMercenaryInstanceId"] = null
        };

        private void VerifyCommand(FacilityCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            Require(command.ExpectedRevision == Revision, "FACILITY_SAVE_REVISION_CONFLICT");
            string computed = new FacilityRequestHasher().ComputeHash(command);
            Require(FixedTimeEquals(computed, command.RequestHash), "FACILITY_OPERATION_HASH_MISMATCH");
            _ = catalog.GetFacility(command.FacilityId);
        }

        private void Commit(JObject draft, long expectedRevision, DateTimeOffset now)
        {
            SaveWriteResult result = saveService.Repository.Save(ActiveProfileId, draft, expectedRevision, now);
            RequireWrite(result, "SAVE_WRITE_FAILED");
            CurrentDocument = result.Document;
        }

        private static void RequireWrite(SaveWriteResult result, string fallback)
        {
            if (!result.Success) throw new InvalidOperationException(result.ErrorCode ?? fallback);
        }

        private JObject FindFacility(string facilityId)
        {
            JObject facility = CurrentDocument["payload"]["facilities"].Children<JObject>().SingleOrDefault(value => value.Value<string>("facilityId") == facilityId);
            if (facility == null) throw new FacilityCommandException("FACILITY_NOT_FOUND");
            return facility;
        }

        private JObject FindNpc(Guid id)
        {
            JObject npc = CurrentDocument["payload"]["managementNpcs"].Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id.ToString("D"));
            if (npc == null) throw new FacilityCommandException("FACILITY_NPC_REQUIRED");
            return npc;
        }

        private JObject FindJournal(Guid operationId) => FindJournalOrNull(operationId) ?? throw new FacilityCommandException("FACILITY_JOB_NOT_FOUND");
        private JObject FindJournalOrNull(Guid operationId) => CurrentDocument["payload"]["operationJournal"].Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == operationId.ToString("D"));

        private bool HasMatchingNpc(JObject facility, FacilityDefinition definition)
        {
            if (facility["assignedNpcInstanceId"].Type == JTokenType.Null) return false;
            string id = facility.Value<string>("assignedNpcInstanceId");
            JObject npc = CurrentDocument["payload"]["managementNpcs"].Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id);
            return npc != null && npc.Value<string>("professionId") == definition.RequiredProfessionId && npc.Value<string>("assignedFacilityId") == definition.Id;
        }

        private void SetNpcWorking(JObject facility, bool working)
        {
            if (facility["assignedNpcInstanceId"].Type == JTokenType.Null) return;
            string id = facility.Value<string>("assignedNpcInstanceId");
            JObject npc = CurrentDocument["payload"]["managementNpcs"].Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id);
            if (npc != null) npc["working"] = working;
        }

        private void ApplyLodgeLimits(JObject facility)
        {
            if (facility.Value<string>("facilityId") != "FAC_LODGE") return;
            int level = facility.Value<int>("level");
            JObject kingdom = (JObject)CurrentDocument["payload"]["kingdom"];
            kingdom["activeMercenaryLimit"] = level switch { 1 => 4, 2 => 8, 3 => 12, 4 => 16, _ => throw new FacilityCommandException("SAVE_MERCENARY_LODGE_LIMIT_MISMATCH") };
            kingdom["ownedMercenaryLimit"] = level switch { 1 => 8, 2 => 12, 3 => 18, 4 => 24, _ => throw new FacilityCommandException("SAVE_MERCENARY_LODGE_LIMIT_MISMATCH") };
        }

        private string CurrentStageId() => CurrentDocument["payload"]["kingdom"].Value<string>("kingdomStageId");

        private DateTimeOffset ReadTrustedNow()
        {
            DateTimeOffset now = clock.UtcNow.ToUniversalTime();
            Require(lastTrustedUtc == default || now >= lastTrustedUtc.AddSeconds(-2), "FACILITY_CLOCK_ROLLBACK_DETECTED");
            if (now > lastTrustedUtc) lastTrustedUtc = now;
            return now;
        }

        private void EnsureReady()
        {
            if (!IsBootstrapped || CurrentDocument == null || catalog == null) throw new InvalidOperationException("P04_GAME_NOT_BOOTSTRAPPED");
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static void Require(bool condition, string errorCode) => FacilityTransitionPolicy.Require(condition, errorCode);
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static DateTimeOffset ParseUtc(string value) => DateTimeOffset.ParseExact(value, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

        private readonly struct MutationResult
        {
            public MutationResult(string digest, bool changed) { Digest = digest; Changed = changed; }
            public string Digest { get; }
            public bool Changed { get; }
        }
    }
}
