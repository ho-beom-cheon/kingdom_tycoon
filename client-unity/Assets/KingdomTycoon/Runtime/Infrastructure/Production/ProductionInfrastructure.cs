using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Production;
using KingdomTycoon.Domain.Production;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Production
{
    public sealed class ProductionRequestHasher
    {
        public string Compute(ProductionCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class P09ProductionCatalog
    {
        public sealed class FacilityRule { public int Level; public int QueueCapacity; public int SpeedBps; }
        public sealed class ProficiencyRule { public string Id; public int Order; public long RequiredXp; public int SpeedBps; public int EfficiencyBps; public int QualityBonusBps; }
        public sealed class MaterialRule { public string ItemId; public long Quantity; }
        public sealed class RecipeRule
        {
            public string Id; public string FacilityId; public int FacilityLevel; public string ProficiencyId; public long BaseTicks;
            public IReadOnlyList<MaterialRule> Materials; public string OutputKind; public string OutputId; public long OutputQuantity;
        }
        public sealed class TargetRule
        {
            public string Id; public int Priority; public string ProductKind; public string ProductId; public string RecipeId;
            public int Target; public int Minimum; public int Maximum; public int EmergencyFloor;
        }
        public sealed class TreatmentRule { public long BaseTicks; public long Xp; public string FromState; public string ToState; }

        private readonly Dictionary<int, FacilityRule> facilities;
        private readonly Dictionary<string, ProficiencyRule> proficiency;
        private readonly Dictionary<string, RecipeRule> recipes;

        public P09ProductionCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion != CompileTimeActiveContentVersionProvider.P09ContentVersion) throw new ProductionDomainException("P09_CONTENT_MISSING");
            facilities = catalog.GetTable("production_facility_rules.csv").Rows.Where(Enabled).Select(row => new FacilityRule
            {
                Level = Int(row, "facility_level"), QueueCapacity = Int(row, "queue_capacity"), SpeedBps = Int(row, "speed_bps")
            }).ToDictionary(value => value.Level);
            proficiency = catalog.GetTable("npc_proficiency_levels.csv").Rows.Where(Enabled).Select(row => new ProficiencyRule
            {
                Id = row["proficiency_id"], Order = Int(row, "order"), RequiredXp = Long(row, "xp_required"),
                SpeedBps = DecimalBps(row, "speed_multiplier"), EfficiencyBps = DecimalBps(row, "material_efficiency"),
                QualityBonusBps = DecimalBps(row, "quality_bonus")
            }).ToDictionary(value => value.Id, StringComparer.Ordinal);
            var materialMap = catalog.GetTable("recipe_materials.csv").Rows.Where(Enabled).GroupBy(row => row["recipe_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<MaterialRule>)group.OrderBy(row => Int(row, "material_no"))
                    .Select(row => new MaterialRule { ItemId = row["item_id"], Quantity = Long(row, "quantity") }).ToArray(), StringComparer.Ordinal);
            var outputMap = catalog.GetTable("recipe_outputs.csv").Rows.Where(Enabled).GroupBy(row => row["recipe_id"], StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => Int(row, "output_no")).First(), StringComparer.Ordinal);
            recipes = catalog.GetTable("recipes.csv").Rows.Where(Enabled).Select(row =>
            {
                IReadOnlyDictionary<string, string> output = outputMap[row["recipe_id"]];
                return new RecipeRule
                {
                    Id = row["recipe_id"], FacilityId = row["facility_id"], FacilityLevel = Int(row, "facility_level"),
                    ProficiencyId = row["npc_proficiency_id"], BaseTicks = Long(row, "craft_seconds"),
                    Materials = materialMap.TryGetValue(row["recipe_id"], out IReadOnlyList<MaterialRule> values) ? values : Array.Empty<MaterialRule>(),
                    OutputKind = output["reward_type"] == "EQUIPMENT_TEMPLATE" ? "EQUIPMENT" : output["reward_type"],
                    OutputId = output["reward_id"], OutputQuantity = Long(output, "quantity")
                };
            }).ToDictionary(value => value.Id, StringComparer.Ordinal);
            Targets = catalog.GetTable("production_stock_targets.csv").Rows.Where(Enabled).Select(row => new TargetRule
            {
                Id = row["target_id"], Priority = Int(row, "priority"), ProductKind = row["product_kind"], ProductId = row["product_id"],
                RecipeId = row["recipe_id"], Target = Int(row, "target_quantity"), Minimum = Int(row, "min_target"),
                Maximum = Int(row, "max_target"), EmergencyFloor = Int(row, "emergency_floor")
            }).OrderBy(value => value.Priority).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
            IReadOnlyDictionary<string, string> treatment = catalog.GetTable("treatment_rules.csv").Rows.Single(Enabled);
            Treatment = new TreatmentRule { BaseTicks = Long(treatment, "base_ticks"), Xp = Long(treatment, "xp_reward"), FromState = treatment["from_state"], ToState = treatment["to_state"] };
        }

        public IReadOnlyList<TargetRule> Targets { get; }
        public TreatmentRule Treatment { get; }
        public FacilityRule Facility(int level) => facilities.TryGetValue(level, out FacilityRule value) ? value : throw new ProductionDomainException("P09_CONTENT_INVALID");
        public ProficiencyRule Proficiency(string id) => proficiency.TryGetValue(id, out ProficiencyRule value) ? value : throw new ProductionDomainException("P09_CONTENT_INVALID");
        public RecipeRule Recipe(string id) => recipes.TryGetValue(id ?? string.Empty, out RecipeRule value) ? value : throw new ProductionDomainException("P09_RECIPE_NOT_FOUND");
        public ProficiencyRule ProficiencyForXp(long xp) => proficiency.Values.Where(value => value.RequiredXp <= xp).OrderByDescending(value => value.Order).First();
        public JArray DefaultTargets() => new(Targets.Select(value => new JObject
        {
            ["targetId"] = value.Id, ["priority"] = value.Priority, ["productKind"] = value.ProductKind,
            ["productId"] = value.ProductId, ["recipeId"] = value.RecipeId, ["targetQuantity"] = value.Target,
            ["minTarget"] = value.Minimum, ["maxTarget"] = value.Maximum, ["emergencyFloor"] = value.EmergencyFloor,
            ["enabled"] = true, ["lastStopReason"] = "NONE"
        }));
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string name) => int.Parse(row[name], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string name) => long.Parse(row[name], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static int DecimalBps(IReadOnlyDictionary<string, string> row, string name) => checked((int)Math.Round(decimal.Parse(row[name], NumberStyles.Number, CultureInfo.InvariantCulture) * 10000m, MidpointRounding.AwayFromZero));
    }

    public sealed class P09StoreStockSink : IStoreStockSink
    {
        private readonly Dictionary<int, (int Stack, int Equipment)> capacities;
        private readonly Dictionary<string, int> tiers;
        public P09StoreStockSink(ContentCatalog catalog)
        {
            capacities = catalog.GetTable("store_level_rules.csv").Rows.Where(row => row["enabled"] == "TRUE")
                .ToDictionary(row => Int(row, "store_level"), row => (Int(row, "max_stack_lines"), Int(row, "max_equipment_lines")));
            tiers = catalog.GetTable("equipment_templates.csv").Rows.Where(row => row["enabled"] == "TRUE")
                .ToDictionary(row => row["equipment_template_id"], row => Int(row, "tier"), StringComparer.Ordinal);
        }

        public long Count(JObject document, string kind, string productId)
        {
            JObject store = Store(document);
            return kind == "EQUIPMENT"
                ? store["equipment"]!.Children<JObject>().LongCount(value => value.Value<string>("equipmentTemplateId") == productId)
                : store["stackLines"]!.Children<JObject>().Where(value => value.Value<string>("productKind") == kind && value.Value<string>("productId") == productId).Sum(value => value.Value<long>("quantity"));
        }

        public void AddStack(JObject document, string productId, long quantity, Guid operationId)
        {
            JObject store = Store(document); JArray lines = (JArray)store["stackLines"]!; int level = StoreLevel(document); int capacity = capacities[level].Stack;
            const string kind = "POTION", source = "PRODUCTION"; string id = $"STACK:{kind}:{productId}:{source}";
            JObject line = lines.Children<JObject>().SingleOrDefault(value => value.Value<string>("stockLineId") == id);
            if (line == null)
            {
                if (lines.Count >= capacity) throw new ProductionDomainException("P09_OUTPUT_CAPACITY");
                lines.Add(new JObject { ["stockLineId"] = id, ["productKind"] = kind, ["productId"] = productId, ["quantity"] = quantity, ["sourceType"] = source, ["firstAcquiredOperationId"] = operationId.ToString("D"), ["lastAcquiredOperationId"] = operationId.ToString("D") });
            }
            else { line["quantity"] = checked(line.Value<long>("quantity") + quantity); line["lastAcquiredOperationId"] = operationId.ToString("D"); }
            store["stackLines"] = new JArray(lines.Children<JObject>().OrderBy(value => value.Value<string>("productKind"), StringComparer.Ordinal).ThenBy(value => value.Value<string>("productId"), StringComparer.Ordinal).ThenBy(value => value.Value<string>("sourceType"), StringComparer.Ordinal));
            store["stockVersion"] = checked(store.Value<long>("stockVersion") + 1);
        }

        public void AddEquipment(JObject document, string templateId, string instanceId, string qualityId, Guid operationId, string contentVersion)
        {
            JObject store = Store(document); JArray equipment = (JArray)store["equipment"]!; int level = StoreLevel(document);
            if (equipment.Count >= capacities[level].Equipment) throw new ProductionDomainException("P09_OUTPUT_CAPACITY");
            equipment.Add(new JObject
            {
                ["instanceId"] = instanceId, ["equipmentTemplateId"] = templateId, ["tier"] = tiers[templateId], ["qualityId"] = qualityId,
                ["enhancementLevel"] = 0, ["refineOption"] = null, ["locked"] = false, ["equippedByMercenaryInstanceId"] = null,
                ["sourceContentVersion"] = contentVersion, ["generationOperationId"] = operationId.ToString("D"),
                ["stockAcquiredAtUtc"] = "1970-01-01T00:00:00.000Z", ["stockAcquiredOperationId"] = operationId.ToString("D"), ["sourceType"] = "PRODUCTION"
            });
            store["equipment"] = new JArray(equipment.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal));
            store["stockVersion"] = checked(store.Value<long>("stockVersion") + 1);
        }

        private static JObject Store(JObject document) => (JObject)document["payload"]!["economy"]!["store"]!;
        private static int StoreLevel(JObject document) => document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_STORE").Value<int>("level");
        private static int Int(IReadOnlyDictionary<string, string> row, string name) => int.Parse(row[name], CultureInfo.InvariantCulture);
    }

    public sealed class ProductionGameService : IAppService
    {
        private readonly ITrustedUtcClock clock;
        private readonly SemaphoreSlim gate = new(1, 1);
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private P09ProductionCatalog catalog;
        private IStoreStockSink stock;

        public ProductionGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 68;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<ProductionOverviewDto> Changed;

        public void Initialize(ServiceRegistry services)
        { save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>(); }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || content.Catalog?.ContentVersion != CompileTimeActiveContentVersionProvider.P09ContentVersion) throw new ProductionDomainException("P09_CONTENT_MISSING");
            catalog = new P09ProductionCatalog(content.Catalog); stock = new P09StoreStockSink(content.Catalog); IsBootstrapped = true;
        }

        public ProductionOverviewDto GetOverview()
        {
            EnsureReady(); JObject document = game.Snapshot(); JObject production = Production(document); var facilities = new List<ProductionFacilityDto>();
            foreach (JObject queue in production["facilityQueues"]!.Children<JObject>().OrderBy(value => value.Value<string>("facilityId"), StringComparer.Ordinal))
            {
                string facilityId = queue.Value<string>("facilityId"); JObject facility = Facility(document, facilityId); JObject npc = AssignedNpc(document, facility);
                P09ProductionCatalog.FacilityRule rule = catalog.Facility(facility.Value<int>("level")); JArray jobs = (JArray)queue["jobs"]!;
                string reason = EvaluateReady(document, facilityId, null, false);
                facilities.Add(new ProductionFacilityDto(facilityId, facility.Value<string>("state"), facility.Value<int>("level"), npc?.Value<string>("professionId"), npc?.Value<string>("proficiencyId"), npc?.Value<long>("proficiencyExp") ?? 0,
                    reason, jobs.Count, rule.QueueCapacity, jobs.Count == 0 ? 0 : jobs[0]!.Value<long>("ticksRemaining")));
            }
            var targets = production["stockTargets"]!.Children<JObject>().OrderBy(value => value.Value<int>("priority")).ThenBy(value => value.Value<string>("targetId"), StringComparer.Ordinal)
                .Select(target => new StockTargetDto(target.Value<string>("targetId"), target.Value<int>("priority"), target.Value<string>("productKind"), target.Value<string>("productId"), target.Value<string>("recipeId"),
                    checked((int)stock.Count(document, target.Value<string>("productKind"), target.Value<string>("productId"))), QueuedOutput(production, target.Value<string>("productKind"), target.Value<string>("productId")), target.Value<int>("targetQuantity"), target.Value<bool>("enabled"), target.Value<string>("lastStopReason"))).ToArray();
            return new ProductionOverviewDto(document.Value<long>("revision"), production.Value<long>("currentTick"), facilities, targets, production["events"]!.Count());
        }

        public ProductionOperationResult SetStockTarget(SetStockTargetCommand command) => Execute(command, draft =>
        {
            JObject target = Production(draft)["stockTargets"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("targetId") == command.TargetId) ?? throw new ProductionDomainException("P09_TARGET_NOT_FOUND");
            if (command.Priority is < 1 or > 9999) throw new ProductionDomainException("P09_TARGET_INVALID");
            int value = ProductionMath.ClampTarget(target.Value<string>("productKind"), command.TargetQuantity, target.Value<int>("emergencyFloor"), target.Value<int>("maxTarget"));
            target["targetQuantity"] = value; target["priority"] = command.Priority; target["enabled"] = command.Enabled; target["lastStopReason"] = "NONE"; return 0;
        });

        public ProductionOperationResult Enqueue(EnqueueProductionCommand command) => Execute(command, draft => { EnqueueCraft(draft, command.RecipeId, command.Quantity, command.OperationId); return 0; });
        public ProductionOperationResult EnqueueTreatment(EnqueueTreatmentCommand command) => Execute(command, draft => { EnqueueTreatmentInternal(draft, command.MercenaryInstanceId, command.OperationId); return 0; });

        public ProductionOperationResult RunAutomation(RunProductionAutomationCommand command) => Execute(command, draft =>
        {
            JObject production = Production(draft); var used = new HashSet<string>(StringComparer.Ordinal); int queued = 0;
            foreach (JObject target in production["stockTargets"]!.Children<JObject>().Where(value => value.Value<bool>("enabled")).OrderBy(value => value.Value<int>("priority")).ThenBy(value => value.Value<string>("targetId"), StringComparer.Ordinal))
            {
                if (queued >= 2) break; string kind = target.Value<string>("productKind"), id = target.Value<string>("productId");
                long available = stock.Count(draft, kind, id) + QueuedOutput(production, kind, id);
                if (available >= target.Value<int>("targetQuantity")) { target["lastStopReason"] = "P09_TARGET_REACHED"; continue; }
                P09ProductionCatalog.RecipeRule recipe = catalog.Recipe(target.Value<string>("recipeId"));
                if (!used.Add(recipe.FacilityId)) continue;
                try { EnqueueCraft(draft, recipe.Id, 1, command.OperationId); target["lastStopReason"] = "NONE"; queued++; }
                catch (ProductionDomainException error) { target["lastStopReason"] = error.ErrorCode; Queue(draft, recipe.FacilityId)["stoppedReason"] = error.ErrorCode; AppendEvent(draft, "FacilityProductionStopped", recipe.FacilityId, null, error.ErrorCode); }
            }
            return 0;
        });

        public ProductionOperationResult Advance(AdvanceProductionTicksCommand command) => Execute(command, draft =>
        {
            if (command.Ticks is < 1 or > 3600) throw new ProductionDomainException("P09_TICK_INVALID");
            JObject production = Production(draft); production["currentTick"] = checked(production.Value<long>("currentTick") + command.Ticks); int completed = 0;
            foreach (JObject queue in production["facilityQueues"]!.Children<JObject>().OrderBy(value => value.Value<string>("facilityId"), StringComparer.Ordinal))
            {
                string ready = EvaluateReady(draft, queue.Value<string>("facilityId"), null, false);
                if (ready != "NONE") { queue["stoppedReason"] = ready; AppendEvent(draft, "FacilityProductionStopped", queue.Value<string>("facilityId"), null, ready); continue; }
                long budget = command.Ticks; JArray jobs = (JArray)queue["jobs"]!;
                while (budget > 0 && jobs.Count > 0 && completed < 64)
                {
                    JObject job = (JObject)jobs[0]; if (job.Value<string>("status") == "QUEUED") { job["status"] = "RUNNING"; job["startedAtTick"] = production.Value<long>("currentTick") - budget; }
                    long remaining = job.Value<long>("ticksRemaining"), used = Math.Min(remaining, budget); job["ticksRemaining"] = remaining - used; budget -= used;
                    if (job.Value<long>("ticksRemaining") > 0) break;
                    Complete(draft, queue, job, command.OperationId); job.Remove(); completed++;
                }
                queue["stoppedReason"] = jobs.Count == 0 ? "NONE" : EvaluateReady(draft, queue.Value<string>("facilityId"), null, false);
            }
            return completed;
        });

        public void Shutdown() { Changed = null; IsBootstrapped = false; stock = null; catalog = null; game = null; content = null; save = null; gate.Dispose(); }

        private ProductionOperationResult Execute(ProductionCommand command, Func<JObject, int> mutation)
        {
            EnsureReady(); if (command == null) throw new ArgumentNullException(nameof(command)); VerifyHash(command.RequestHash, new ProductionRequestHasher().Compute(command)); gate.Wait();
            try
            {
                JObject current = game.Snapshot(); JObject replay = current["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == command.OperationId.ToString("D"));
                if (replay != null) { VerifyHash(command.RequestHash, replay.Value<string>("requestHash")); return new ProductionOperationResult(command.OperationId, game.Revision, game.Revision, true, 0, replay.Value<string>("resultDigest")); }
                if (command.ExpectedRevision != game.Revision) throw new ProductionDomainException("P09_SAVE_REVISION_CONFLICT");
                long before = game.Revision; JObject draft = (JObject)current.DeepClone(); int completed = mutation(draft);
                JObject result = new() { ["operationId"] = command.OperationId.ToString("D"), ["commandType"] = command.CommandType, ["revisionBefore"] = before, ["revisionAfter"] = before + 1, ["completed"] = completed };
                string digest = Rfc8785Canonicalizer.ComputeSha256(result); AppendJournal(draft, command, digest);
                SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, before, clock.UtcNow);
                if (!written.Success) throw new ProductionDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P09_SAVE_REVISION_CONFLICT" : "P09_TRANSACTION_INVARIANT_FAILED");
                game.SynchronizeCommittedDocument(written.Document); Changed?.Invoke(this, GetOverview());
                return new ProductionOperationResult(command.OperationId, before, game.Revision, false, completed, digest);
            }
            finally { gate.Release(); }
        }

        private void EnqueueCraft(JObject document, string recipeId, int quantity, Guid operationId)
        {
            if (quantity is < 1 or > 99) throw new ProductionDomainException("P09_QUANTITY_INVALID"); P09ProductionCatalog.RecipeRule recipe = catalog.Recipe(recipeId);
            string reason = EvaluateReady(document, recipe.FacilityId, recipe, true); if (reason != "NONE") throw new ProductionDomainException(reason);
            JObject facility = Facility(document, recipe.FacilityId); JObject npc = AssignedNpc(document, facility); JObject queue = Queue(document, recipe.FacilityId); JArray jobs = (JArray)queue["jobs"]!;
            P09ProductionCatalog.FacilityRule facilityRule = catalog.Facility(facility.Value<int>("level")); if (jobs.Count >= facilityRule.QueueCapacity) throw new ProductionDomainException("P09_QUEUE_FULL");
            P09ProductionCatalog.ProficiencyRule npcRule = catalog.Proficiency(npc.Value<string>("proficiencyId")); var reserved = new JArray();
            var requirements = recipe.Materials.OrderBy(value => value.ItemId, StringComparer.Ordinal)
                .Select(input => (input.ItemId, Quantity: ProductionMath.EfficientQuantity(checked(input.Quantity * quantity), npcRule.EfficiencyBps))).ToArray();
            foreach ((string itemId, long required) in requirements)
            {
                JObject line = document["payload"]!["inventory"]!["itemStacks"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == itemId);
                if (line == null || line.Value<long>("quantity") < required) throw new ProductionDomainException("P09_MATERIAL_INSUFFICIENT");
            }
            foreach ((string itemId, long required) in requirements)
            {
                DebitMaterial(document, itemId, required); reserved.Add(new JObject { ["itemId"] = itemId, ["quantity"] = required });
            }
            long queueNo = Production(document).Value<long>("nextQueueNo"); Production(document)["nextQueueNo"] = checked(queueNo + 1);
            jobs.Add(new JObject
            {
                ["jobId"] = DeterministicUuid(operationId + ":" + queueNo), ["queueNo"] = queueNo, ["jobKind"] = "CRAFT", ["recipeId"] = recipe.Id,
                ["targetMercenaryInstanceId"] = null, ["quantity"] = quantity,
                ["ticksTotal"] = ProductionMath.DurationTicks(checked(recipe.BaseTicks * quantity), facilityRule.SpeedBps, npcRule.SpeedBps),
                ["ticksRemaining"] = ProductionMath.DurationTicks(checked(recipe.BaseTicks * quantity), facilityRule.SpeedBps, npcRule.SpeedBps), ["status"] = "QUEUED",
                ["reservedMaterials"] = reserved, ["outputKind"] = recipe.OutputKind, ["outputId"] = recipe.OutputId,
                ["outputQuantity"] = checked(recipe.OutputQuantity * quantity), ["enqueuedOperationId"] = operationId.ToString("D"), ["startedAtTick"] = null
            }); queue["stoppedReason"] = "NONE"; AppendEvent(document, "ProductionQueueChanged", recipe.FacilityId, jobs.Last.Value<string>("jobId"), "NONE");
        }

        private void EnqueueTreatmentInternal(JObject document, string mercenaryId, Guid operationId)
        {
            string reason = EvaluateReady(document, "FAC_INFIRMARY", null, true); if (reason != "NONE") throw new ProductionDomainException(reason);
            JObject mercenary = document["payload"]!["mercenaries"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == mercenaryId);
            if (mercenary == null || mercenary["autonomy"]!.Value<string>("state") != catalog.Treatment.FromState) throw new ProductionDomainException("P09_TREATMENT_TARGET_INVALID");
            JObject queue = Queue(document, "FAC_INFIRMARY"); JArray jobs = (JArray)queue["jobs"]!;
            if (jobs.Children<JObject>().Any(value => value.Value<string>("targetMercenaryInstanceId") == mercenaryId)) throw new ProductionDomainException("P09_TREATMENT_TARGET_INVALID");
            JObject facility = Facility(document, "FAC_INFIRMARY"); P09ProductionCatalog.FacilityRule facilityRule = catalog.Facility(facility.Value<int>("level")); if (jobs.Count >= facilityRule.QueueCapacity) throw new ProductionDomainException("P09_QUEUE_FULL");
            JObject npc = AssignedNpc(document, facility); P09ProductionCatalog.ProficiencyRule npcRule = catalog.Proficiency(npc.Value<string>("proficiencyId")); long queueNo = Production(document).Value<long>("nextQueueNo"); Production(document)["nextQueueNo"] = queueNo + 1;
            long ticks = ProductionMath.DurationTicks(catalog.Treatment.BaseTicks, facilityRule.SpeedBps, npcRule.SpeedBps); string jobId = DeterministicUuid(operationId + ":" + queueNo);
            jobs.Add(new JObject { ["jobId"] = jobId, ["queueNo"] = queueNo, ["jobKind"] = "TREATMENT", ["recipeId"] = null, ["targetMercenaryInstanceId"] = mercenaryId, ["quantity"] = 1,
                ["ticksTotal"] = ticks, ["ticksRemaining"] = ticks, ["status"] = "QUEUED", ["reservedMaterials"] = new JArray(), ["outputKind"] = "TREATMENT", ["outputId"] = "TREAT_INJURY_BASIC", ["outputQuantity"] = 1,
                ["enqueuedOperationId"] = operationId.ToString("D"), ["startedAtTick"] = null }); queue["stoppedReason"] = "NONE"; AppendEvent(document, "ProductionQueueChanged", "FAC_INFIRMARY", jobId, "NONE");
        }

        private void Complete(JObject document, JObject queue, JObject job, Guid operationId)
        {
            string facilityId = queue.Value<string>("facilityId"), kind = job.Value<string>("jobKind"), outputKind = job.Value<string>("outputKind"), outputId = job.Value<string>("outputId"); long quantity = job.Value<long>("outputQuantity");
            if (kind == "CRAFT")
            {
                if (outputKind == "POTION") stock.AddStack(document, outputId, quantity, operationId);
                else for (int index = 0; index < quantity; index++) stock.AddEquipment(document, outputId, DeterministicUuid(job.Value<string>("jobId") + ":" + index), RollQuality(job.Value<string>("jobId"), index, AssignedNpc(document, Facility(document, facilityId))), operationId, CompileTimeActiveContentVersionProvider.P09ContentVersion);
                P09ProductionCatalog.RecipeRule recipe = catalog.Recipe(job.Value<string>("recipeId")); AddXp(document, facilityId, checked(recipe.BaseTicks * job.Value<long>("quantity")));
                AddMerchantXp(document, quantity);
            }
            else
            {
                JObject mercenary = document["payload"]!["mercenaries"]!.Children<JObject>().Single(value => value.Value<string>("instanceId") == job.Value<string>("targetMercenaryInstanceId"));
                JObject autonomy = (JObject)mercenary["autonomy"]!; autonomy["state"] = catalog.Treatment.ToState; autonomy["reasonCode"] = "TREATMENT_COMPLETE"; autonomy["currentRegionId"] = null; autonomy["targetInstanceId"] = null;
                AddXp(document, facilityId, catalog.Treatment.Xp); AppendEvent(document, "TreatmentCompleted", facilityId, job.Value<string>("jobId"), "NONE");
            }
            queue["lastCompletion"] = new JObject { ["jobId"] = job.Value<string>("jobId"), ["jobKind"] = kind, ["outputKind"] = outputKind, ["outputId"] = outputId, ["quantity"] = quantity, ["completedAtTick"] = Production(document).Value<long>("currentTick") };
            AppendEvent(document, "ProductionCompleted", facilityId, job.Value<string>("jobId"), "NONE");
        }

        private string EvaluateReady(JObject document, string facilityId, P09ProductionCatalog.RecipeRule recipe, bool enforceRecipe)
        {
            JObject facility = Facility(document, facilityId); string state = facility.Value<string>("state");
            if (state is "LOCKED" or "BUILDABLE" or "BUILDING") return "P09_FACILITY_LOCKED";
            if (state != "ACTIVE") return "P09_FACILITY_INACTIVE"; JObject npc = AssignedNpc(document, facility); if (npc == null || !npc.Value<bool>("working")) return "P09_NPC_REQUIRED";
            if (enforceRecipe && recipe != null)
            {
                if (facility.Value<int>("level") < recipe.FacilityLevel) return "P09_RECIPE_LOCKED";
                if (catalog.Proficiency(npc.Value<string>("proficiencyId")).Order < catalog.Proficiency(recipe.ProficiencyId).Order) return "P09_PROFICIENCY_REQUIRED";
            }
            return "NONE";
        }

        private void AddXp(JObject document, string facilityId, long xp)
        {
            JObject npc = AssignedNpc(document, Facility(document, facilityId)); long before = npc.Value<long>("proficiencyExp"), after = checked(before + xp); string old = npc.Value<string>("proficiencyId"), next = catalog.ProficiencyForXp(after).Id;
            npc["proficiencyExp"] = after; npc["proficiencyId"] = next; if (old != next) AppendEvent(document, "NpcProficiencyChanged", facilityId, null, next);
        }

        private void AddMerchantXp(JObject document, long xp)
        {
            JObject facility = Facility(document, "FAC_STORE"), npc = AssignedNpc(document, facility); if (npc == null || !npc.Value<bool>("working")) return;
            long after = checked(npc.Value<long>("proficiencyExp") + xp); string old = npc.Value<string>("proficiencyId"), next = catalog.ProficiencyForXp(after).Id;
            npc["proficiencyExp"] = after; npc["proficiencyId"] = next; if (old != next) AppendEvent(document, "NpcProficiencyChanged", "FAC_STORE", null, next);
        }

        private void DebitMaterial(JObject document, string itemId, long quantity)
        {
            JObject line = document["payload"]!["inventory"]!["itemStacks"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == itemId);
            if (line == null || line.Value<long>("quantity") < quantity) throw new ProductionDomainException("P09_MATERIAL_INSUFFICIENT");
            long remaining = line.Value<long>("quantity") - quantity; if (remaining == 0) line.Remove(); else line["quantity"] = remaining;
        }

        private static int QueuedOutput(JObject production, string kind, string id) => checked((int)production["facilityQueues"]!.Children<JObject>().SelectMany(queue => queue["jobs"]!.Children<JObject>()).Where(job => job.Value<string>("outputKind") == kind && job.Value<string>("outputId") == id).Sum(job => job.Value<long>("outputQuantity")));
        private static JObject Production(JObject document) => (JObject)document["payload"]!["production"]!;
        private static JObject Facility(JObject document, string id) => document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == id);
        private static JObject Queue(JObject document, string id) => Production(document)["facilityQueues"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == id);
        private static JObject AssignedNpc(JObject document, JObject facility)
        {
            if (facility["assignedNpcInstanceId"]!.Type == JTokenType.Null) return null; string id = facility.Value<string>("assignedNpcInstanceId");
            return document["payload"]!["managementNpcs"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id && value.Value<string>("assignedFacilityId") == facility.Value<string>("facilityId"));
        }
        private string RollQuality(string jobId, int index, JObject npc)
        {
            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(jobId + ":QUALITY:" + index)); int roll = ((bytes[0] << 8) | bytes[1]) % 10000;
            int bonus = npc == null ? 0 : catalog.Proficiency(npc.Value<string>("proficiencyId")).QualityBonusBps;
            if (roll < 200 + bonus / 5) return "QUALITY_RARE"; if (roll < 2000 + bonus) return "QUALITY_FINE"; return "QUALITY_COMMON";
        }
        private static string DeterministicUuid(string seed)
        {
            byte[] bytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(seed)).Take(16).ToArray(); bytes[6] = (byte)((bytes[6] & 0x0f) | 0x70); bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            var hex = new StringBuilder(36);
            for (int index = 0; index < bytes.Length; index++) { if (index is 4 or 6 or 8 or 10) hex.Append('-'); hex.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture)); }
            return hex.ToString();
        }
        private static void VerifyHash(string expected, string actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Length || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual))) throw new ProductionDomainException("P09_OPERATION_REPLAY_MISMATCH");
        }
        private void AppendEvent(JObject document, string type, string facilityId, string jobId, string reason)
        {
            JObject production = Production(document); JArray events = (JArray)production["events"]!; if (events.Count >= 100) events.RemoveAt(0); long sequence = production.Value<long>("nextEventSequence"); production["nextEventSequence"] = sequence + 1;
            events.Add(new JObject { ["sequence"] = sequence, ["eventType"] = type, ["facilityId"] = facilityId, ["jobId"] = jobId == null ? JValue.CreateNull() : jobId, ["reasonCode"] = reason, ["tick"] = production.Value<long>("currentTick") });
        }
        private void AppendJournal(JObject document, ProductionCommand command, string digest)
        {
            string now = clock.UtcNow.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject { ["operationId"] = command.OperationId.ToString("D"), ["operationType"] = "PRODUCTION_COMMAND", ["facilityJobType"] = null, ["requestHash"] = command.RequestHash,
                ["status"] = "COMMITTED", ["createdAtUtc"] = now, ["updatedAtUtc"] = now, ["completedAtUtc"] = now, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest,
                ["failureResolution"] = null, ["resolvedAtUtc"] = null, ["resultPayload"] = null });
        }
        private void EnsureReady() { if (!IsBootstrapped || catalog == null || stock == null) throw new ProductionDomainException("P09_CONTENT_MISSING"); }
    }
}
