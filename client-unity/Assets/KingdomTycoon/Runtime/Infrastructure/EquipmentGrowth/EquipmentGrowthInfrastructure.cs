using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.EquipmentGrowth;
using KingdomTycoon.Domain.EquipmentGrowth;
using KingdomTycoon.Domain.Inventory;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.EquipmentGrowth
{
    public sealed class EquipmentGrowthRequestHasher
    {
        public string Compute(EquipmentGrowthCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class P10EquipmentGrowthCatalog
    {
        public sealed class FacilityRule { public int Level; public int MaxEnhancement; public bool Refine; public bool Dismantle; public int BatchLimit; }
        public sealed class EnhanceRule { public int TargetLevel; public int ChanceBps; public string StoneId; public long StoneQuantity; public long Gold; public int PityIncrementBps; }
        public sealed class RefineRule { public string Id; public int MinimumBps; public int MaximumBps; public string MaterialId; public long MaterialQuantity; public long Gold; }
        public sealed class DismantleRule { public int Tier; public string StoneId; public long BaseQuantity; public int RefundBps; }
        public sealed class EquipmentRule { public string Id; public int Tier; public long BasePower; public string Source; }

        private readonly Dictionary<int, FacilityRule> facilities;
        private readonly Dictionary<int, EnhanceRule> enhance;
        private readonly Dictionary<string, RefineRule> refine;
        private readonly Dictionary<int, DismantleRule> dismantle;
        private readonly Dictionary<string, EquipmentRule> equipment;
        private readonly Dictionary<string, int> qualityBps;
        private readonly Dictionary<int, int> inventoryCapacity;

        public P10EquipmentGrowthCatalog(ContentCatalog catalog)
        {
            if (catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion)) throw new EquipmentGrowthDomainException("P10_CONTENT_MISSING");
            facilities = catalog.GetTable("equipment_growth_facility_rules.csv").Rows.Where(Enabled).Select(row => new FacilityRule
            {
                Level = Int(row, "facility_level"), MaxEnhancement = Int(row, "max_enhancement_level"), Refine = Bool(row, "refine_enabled"),
                Dismantle = Bool(row, "dismantle_enabled"), BatchLimit = Int(row, "batch_dismantle_limit")
            }).ToDictionary(value => value.Level);
            enhance = catalog.GetTable("enhancement_rules.csv").Rows.Where(Enabled).Select(row => new EnhanceRule
            {
                TargetLevel = Int(row, "target_level"), ChanceBps = DecimalBps(row, "success_chance"), StoneId = row["stone_item_id"],
                StoneQuantity = Long(row, "stone_quantity"), Gold = Long(row, "personal_gold_cost"), PityIncrementBps = DecimalBps(row, "fail_pity_increment")
            }).ToDictionary(value => value.TargetLevel);
            refine = catalog.GetTable("refine_options.csv").Rows.Where(Enabled).Select(row => new RefineRule
            {
                Id = row["refine_option_id"], MinimumBps = DecimalBps(row, "min_value"), MaximumBps = DecimalBps(row, "max_value"),
                MaterialId = row["material_item_id"], MaterialQuantity = Long(row, "material_quantity"), Gold = Long(row, "personal_gold_cost")
            }).ToDictionary(value => value.Id, StringComparer.Ordinal);
            dismantle = catalog.GetTable("dismantle_rules.csv").Rows.Where(Enabled).Select(row => new DismantleRule
            {
                Tier = Int(row, "equipment_tier"), StoneId = row["stone_item_id"], BaseQuantity = Long(row, "base_stone_quantity"), RefundBps = Int(row, "enhancement_refund_bps")
            }).ToDictionary(value => value.Tier);
            equipment = catalog.GetTable("equipment_templates.csv").Rows.Where(Enabled).Select(row => new EquipmentRule
            {
                Id = row["equipment_template_id"], Tier = Int(row, "tier"), BasePower = Long(row, "base_power"), Source = row["source"]
            }).ToDictionary(value => value.Id, StringComparer.Ordinal);
            qualityBps = catalog.GetTable("equipment_qualities.csv").Rows.Where(Enabled).ToDictionary(row => row["quality_id"], row => DecimalBps(row, "stat_multiplier"), StringComparer.Ordinal);
            inventoryCapacity = catalog.GetTable("inventory_capacity_rules.csv").Rows.Where(Enabled).ToDictionary(row => Int(row, "warehouse_level"), row => Int(row, "item_stack_slots"));
            if (facilities.Count != 4 || enhance.Count != 10 || refine.Count != 11 || dismantle.Count != 5) throw new EquipmentGrowthDomainException("P10_CONTENT_MISSING");
        }

        public FacilityRule Facility(int level) => facilities.TryGetValue(level, out FacilityRule value) ? value : throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
        public EnhanceRule Enhance(int level) => enhance.TryGetValue(level, out EnhanceRule value) ? value : throw new EquipmentGrowthDomainException("P10_MAX_LEVEL_REACHED");
        public RefineRule Refine(string id) => refine.TryGetValue(id ?? string.Empty, out RefineRule value) ? value : throw new EquipmentGrowthDomainException("P10_REFINE_OPTION_INVALID");
        public DismantleRule Dismantle(int tier) => dismantle.TryGetValue(tier, out DismantleRule value) ? value : throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
        public EquipmentRule Equipment(string id) => equipment.TryGetValue(id ?? string.Empty, out EquipmentRule value) ? value : throw new EquipmentGrowthDomainException("P10_EQUIPMENT_NOT_FOUND");
        public int QualityBps(string id) => qualityBps.TryGetValue(id ?? string.Empty, out int value) ? value : throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
        public int ItemStackCapacity(int warehouseLevel) => inventoryCapacity.TryGetValue(warehouseLevel, out int value) ? value : throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static bool Bool(IReadOnlyDictionary<string, string> row, string key) => row[key] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static long Long(IReadOnlyDictionary<string, string> row, string key) => long.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
        private static int DecimalBps(IReadOnlyDictionary<string, string> row, string key) => checked((int)Math.Round(decimal.Parse(row[key], NumberStyles.Number, CultureInfo.InvariantCulture) * 10_000m, MidpointRounding.AwayFromZero));
    }

    public sealed class EquipmentGrowthGameService : IAppService
    {
        private readonly ITrustedUtcClock clock;
        private readonly SemaphoreSlim gate = new(1, 1);
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private P10EquipmentGrowthCatalog catalog;

        public EquipmentGrowthGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 70;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<EquipmentGrowthOverviewDto> Changed;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>(); content = services.Get<ContentCatalogService>(); game = services.Get<FacilityGameService>();
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || game.CurrentDocument.Value<string>("contentVersion") is not (CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion) || content.Catalog?.ContentVersion is not (CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion))
                throw new EquipmentGrowthDomainException("P10_CONTENT_MISSING");
            catalog = new P10EquipmentGrowthCatalog(content.Catalog);
            IsBootstrapped = true;
        }

        public EquipmentGrowthOverviewDto GetOverview(string actorId = null)
        {
            EnsureReady(); JObject document = game.Snapshot(); JObject facility = Facility(document); JObject actor = SelectActor(document, actorId);
            var rows = new List<GrowthEquipmentDto>();
            foreach (JObject value in Inventory(document)["equipment"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal))
            {
                P10EquipmentGrowthCatalog.EquipmentRule definition = catalog.Equipment(value.Value<string>("equipmentTemplateId"));
                JObject refine = value["refineOption"] as JObject; JObject pending = value["pendingRefineOption"] as JObject;
                long power = checked(definition.BasePower * catalog.QualityBps(value.Value<string>("qualityId")) / 10_000 * (10_000 + value.Value<int>("enhancementLevel") * 500L) / 10_000);
                rows.Add(new GrowthEquipmentDto(value.Value<string>("instanceId"), definition.Id, definition.Tier, value.Value<string>("qualityId"), value.Value<int>("enhancementLevel"),
                    value.Value<int>("enhancementPityBps"), refine?.Value<string>("optionId"), refine?.Value<int>("value") ?? 0, pending?.Value<string>("optionId"), pending?.Value<int>("value") ?? 0,
                    value.Value<bool>("locked"), value["equippedByMercenaryInstanceId"]!.Type != JTokenType.Null, power));
            }
            string last = Growth(document)["events"]!.Children<JObject>().OrderByDescending(value => value.Value<long>("sequence")).FirstOrDefault()?.Value<string>("resultCode") ?? "NONE";
            return new EquipmentGrowthOverviewDto(document.Value<long>("revision"), facility.Value<string>("state"), facility.Value<int>("level"), actor?.Value<string>("instanceId"), actor?.Value<long>("personalGold") ?? 0, rows, last);
        }

        public EquipmentGrowthOperationResult Enhance(EnhanceEquipmentCommand command) => Execute(command, draft =>
        {
            JObject actor = RequireActor(draft, command.ActorMercenaryInstanceId); P10EquipmentGrowthCatalog.FacilityRule facility = RequireFacility(draft);
            JObject equipment = RequireMutableEquipment(draft, command.EquipmentInstanceId, false); int before = equipment.Value<int>("enhancementLevel");
            P10EquipmentGrowthCatalog.EnhanceRule rule = catalog.Enhance(before + 1);
            Require(before + 1 <= facility.MaxEnhancement, "P10_FACILITY_LEVEL_REQUIRED");
            Debit(actor, Inventory(draft), rule.StoneId, rule.StoneQuantity, rule.Gold);
            AddInvestment(equipment, rule.StoneId, rule.StoneQuantity); equipment["enhancementAttemptCount"] = checked(equipment.Value<long>("enhancementAttemptCount") + 1);
            int chance = EquipmentGrowthMath.EffectiveChanceBps(rule.ChanceBps, equipment.Value<int>("enhancementPityBps"));
            bool success = EquipmentGrowthMath.IsSuccess(Random(command.OperationId, equipment.Value<long>("enhancementAttemptCount"), "ENHANCE"), chance);
            if (success) { equipment["enhancementLevel"] = before + 1; equipment["enhancementPityBps"] = 0; }
            else equipment["enhancementPityBps"] = Math.Min(10_000, checked(equipment.Value<int>("enhancementPityBps") + rule.PityIncrementBps));
            string code = success ? "P10_ENHANCE_SUCCESS" : "P10_ENHANCE_FAILED";
            AppendEvent(draft, "EquipmentEnhancementAttempted", equipment.Value<string>("instanceId"), actor.Value<string>("instanceId"), code, before, equipment.Value<int>("enhancementLevel"), null);
            return Mutation.Economy(code, equipment.Value<string>("instanceId"), actor.Value<string>("instanceId"), before, equipment.Value<int>("enhancementLevel"), null, 0, -rule.Gold, "ENHANCE_EQUIPMENT", rule.StoneId, rule.StoneQuantity);
        });

        public EquipmentGrowthOperationResult RollRefine(RollRefineOptionCommand command) => Execute(command, draft =>
        {
            JObject actor = RequireActor(draft, command.ActorMercenaryInstanceId); P10EquipmentGrowthCatalog.FacilityRule facility = RequireFacility(draft);
            Require(facility.Refine, "P10_REFINE_FACILITY_REQUIRED"); JObject equipment = RequireMutableEquipment(draft, command.EquipmentInstanceId, false);
            Require(equipment["pendingRefineOption"]!.Type == JTokenType.Null, "P10_REFINE_PENDING_EXISTS"); P10EquipmentGrowthCatalog.RefineRule rule = catalog.Refine(command.RefineOptionId);
            Debit(actor, Inventory(draft), rule.MaterialId, rule.MaterialQuantity, rule.Gold);
            long rollNo = checked(equipment.Value<long>("refineRollCount") + 1); equipment["refineRollCount"] = rollNo;
            int value = EquipmentGrowthMath.InclusiveBps(Random(command.OperationId, rollNo, rule.Id), rule.MinimumBps, rule.MaximumBps);
            equipment["pendingRefineOption"] = new JObject { ["optionId"] = rule.Id, ["value"] = value };
            AppendEvent(draft, "EquipmentRefineRolled", equipment.Value<string>("instanceId"), actor.Value<string>("instanceId"), "P10_REFINE_ROLLED", null, null, rule.Id);
            return Mutation.Economy("P10_REFINE_ROLLED", equipment.Value<string>("instanceId"), actor.Value<string>("instanceId"), null, null, rule.Id, value, -rule.Gold, "REFINE_EQUIPMENT", rule.MaterialId, rule.MaterialQuantity);
        });

        public EquipmentGrowthOperationResult ResolveRefine(ResolveRefineOptionCommand command) => Execute(command, draft =>
        {
            JObject actor = RequireActor(draft, command.ActorMercenaryInstanceId); JObject equipment = RequireMutableEquipment(draft, command.EquipmentInstanceId, true);
            JObject pending = equipment["pendingRefineOption"] as JObject ?? throw new EquipmentGrowthDomainException("P10_REFINE_PENDING_MISSING");
            if (command.AcceptCandidate) equipment["refineOption"] = pending.DeepClone();
            equipment["pendingRefineOption"] = null; string code = command.AcceptCandidate ? "P10_REFINE_ACCEPTED" : "P10_REFINE_KEPT_CURRENT";
            AppendEvent(draft, "EquipmentRefineResolved", equipment.Value<string>("instanceId"), actor.Value<string>("instanceId"), code, null, null, pending.Value<string>("optionId"));
            return Mutation.Basic(code, equipment.Value<string>("instanceId"), actor.Value<string>("instanceId"), pending.Value<string>("optionId"), pending.Value<int>("value"));
        });

        public EquipmentGrowthOperationResult Dismantle(DismantleEquipmentCommand command) => Execute(command, draft =>
        {
            JObject actor = RequireActor(draft, command.ActorMercenaryInstanceId); P10EquipmentGrowthCatalog.FacilityRule facility = RequireFacility(draft);
            Require(facility.Dismantle, "P10_DISMANTLE_FACILITY_REQUIRED"); string[] ids = command.EquipmentInstanceIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Require(ids.Length is > 0 && ids.Length <= facility.BatchLimit && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "P10_DISMANTLE_BATCH_INVALID");
            var selected = ids.Select(id => RequireMutableEquipment(draft, id, false)).ToArray(); JObject policy = (JObject)draft["payload"]!["kingdom"]!["inventoryPolicies"]!;
            foreach (JObject value in selected)
            {
                P10EquipmentGrowthCatalog.EquipmentRule definition = catalog.Equipment(value.Value<string>("equipmentTemplateId"));
                bool protectedItem = policy["protectQualityIds"]!.Values<string>().Contains(value.Value<string>("qualityId"), StringComparer.Ordinal) ||
                    (policy.Value<bool>("protectBossEquipment") && definition.Source == "BOSS") || (policy.Value<bool>("protectFirstDiscovery") && !policy["discoveredEquipmentTemplateIds"]!.Values<string>().Contains(definition.Id, StringComparer.Ordinal));
                Require(!protectedItem || command.ConfirmProtected, "P10_PROTECTED_CONFIRMATION_REQUIRED");
            }
            var returns = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (JObject value in selected)
            {
                P10EquipmentGrowthCatalog.EquipmentRule definition = catalog.Equipment(value.Value<string>("equipmentTemplateId")); P10EquipmentGrowthCatalog.DismantleRule rule = catalog.Dismantle(definition.Tier);
                AddReturn(returns, rule.StoneId, EquipmentGrowthMath.DismantleBase(rule.BaseQuantity, catalog.QualityBps(value.Value<string>("qualityId"))));
                foreach (JObject investment in value["enhancementMaterialInvested"]!.Children<JObject>()) AddReturn(returns, investment.Value<string>("itemId"), EquipmentGrowthMath.Refund(investment.Value<long>("quantity"), rule.RefundBps));
                AppendEvent(draft, "EquipmentDismantled", value.Value<string>("instanceId"), actor.Value<string>("instanceId"), "P10_DISMANTLED", value.Value<int>("enhancementLevel"), null, null);
                value.Remove();
            }
            AddStacks(draft, returns); return Mutation.Dismantle(ids, actor.Value<string>("instanceId"), returns);
        });

        public void Shutdown() { Changed = null; catalog = null; game = null; content = null; save = null; IsBootstrapped = false; gate.Dispose(); }

        private EquipmentGrowthOperationResult Execute(EquipmentGrowthCommand command, Func<JObject, Mutation> mutate)
        {
            EnsureReady(); if (command == null) throw new ArgumentNullException(nameof(command)); VerifyHash(command.RequestHash, new EquipmentGrowthRequestHasher().Compute(command)); gate.Wait();
            try
            {
                JObject current = game.Snapshot(); JObject replay = current["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == command.OperationId.ToString("D"));
                if (replay != null)
                {
                    VerifyHash(command.RequestHash, replay.Value<string>("requestHash")); JObject payload = replay["resultPayload"] as JObject;
                    return new EquipmentGrowthOperationResult(command.OperationId, payload?.Value<long>("revisionBefore") ?? game.Revision, payload?.Value<long>("revisionAfter") ?? game.Revision, true,
                        payload?.Value<string>("resultCode") ?? "P10_REPLAYED", replay.Value<string>("resultDigest"), payload == null ? new JObject() : (JObject)payload.DeepClone());
                }
                if (command.ExpectedRevision != game.Revision) throw new EquipmentGrowthDomainException("P10_SAVE_REVISION_CONFLICT");
                long before = game.Revision; JObject draft = (JObject)current.DeepClone(); Mutation change = mutate(draft); JObject result = change.Result(command.OperationId, before, before + 1);
                string digest = Rfc8785Canonicalizer.ComputeSha256(result); result["resultDigest"] = digest;
                if (change.TransactionType != null) AppendLedger(draft, command, change, digest);
                AppendJournal(draft, command, result, digest); SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, before, clock.UtcNow);
                if (!written.Success) throw new EquipmentGrowthDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P10_SAVE_REVISION_CONFLICT" : "P10_TRANSACTION_INVARIANT_FAILED");
                game.SynchronizeCommittedDocument(written.Document); Changed?.Invoke(this, GetOverview(change.ActorId));
                return new EquipmentGrowthOperationResult(command.OperationId, before, game.Revision, false, change.Code, digest, result);
            }
            finally { gate.Release(); }
        }

        private void AppendLedger(JObject document, EquipmentGrowthCommand command, Mutation change, string digest)
        {
            JObject store = (JObject)document["payload"]!["economy"]!["store"]!; JObject ledger = (JObject)store["ledger"]!; JArray entries = (JArray)ledger["entries"]!;
            if (entries.Count >= 200)
            {
                JObject removed = (JObject)entries[0]; string previous = ledger["prunedDigest"]!.Type == JTokenType.Null ? new string('0', 64) : ledger.Value<string>("prunedDigest");
                ledger["prunedDigest"] = Sha256(Encoding.UTF8.GetBytes(previous).Concat(Rfc8785Canonicalizer.Canonicalize(removed)).ToArray()); ledger["prunedThroughSequence"] = removed.Value<long>("sequence"); entries.RemoveAt(0);
            }
            long sequence = ledger.Value<long>("nextSequence"); entries.Add(new JObject
            {
                ["sequence"] = sequence, ["operationId"] = command.OperationId.ToString("D"), ["transactionType"] = change.TransactionType,
                ["actorMercenaryInstanceId"] = change.ActorId, ["reasonCode"] = change.Code, ["personalGoldDelta"] = change.GoldDelta, ["kingdomGoldDelta"] = 0,
                ["stockVersionAfter"] = store.Value<long>("stockVersion"), ["committedAtUtc"] = FormatUtc(clock.UtcNow), ["requestHash"] = command.RequestHash,
                ["resultDigest"] = digest, ["lines"] = new JArray(new JObject { ["lineNo"] = 1, ["productKind"] = "ITEM", ["productId"] = change.MaterialId, ["quantity"] = change.MaterialQuantity, ["unitPrice"] = 1, ["lineTotal"] = change.MaterialQuantity, ["stockDelta"] = -change.MaterialQuantity })
            }); ledger["nextSequence"] = checked(sequence + 1);
        }

        private void AppendJournal(JObject document, EquipmentGrowthCommand command, JObject result, string digest)
        {
            string now = FormatUtc(clock.UtcNow); ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject
            {
                ["operationId"] = command.OperationId.ToString("D"), ["operationType"] = "EQUIPMENT_GROWTH_COMMAND", ["facilityJobType"] = null,
                ["requestHash"] = command.RequestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = now, ["updatedAtUtc"] = now, ["completedAtUtc"] = now,
                ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest, ["failureResolution"] = null, ["resolvedAtUtc"] = null, ["resultPayload"] = result.DeepClone()
            });
        }

        private void AppendEvent(JObject document, string type, string equipmentId, string actorId, string code, int? before, int? after, string optionId)
        {
            JObject growth = Growth(document); JArray events = (JArray)growth["events"]!; if (events.Count >= 100) events.RemoveAt(0); long sequence = growth.Value<long>("nextEventSequence");
            events.Add(new JObject { ["sequence"] = sequence, ["eventType"] = type, ["equipmentInstanceId"] = equipmentId, ["actorMercenaryInstanceId"] = actorId,
                ["resultCode"] = code, ["levelBefore"] = before.HasValue ? before.Value : JValue.CreateNull(), ["levelAfter"] = after.HasValue ? after.Value : JValue.CreateNull(),
                ["optionId"] = optionId == null ? JValue.CreateNull() : optionId, ["createdAtUtc"] = FormatUtc(clock.UtcNow) }); growth["nextEventSequence"] = checked(sequence + 1);
        }

        private P10EquipmentGrowthCatalog.FacilityRule RequireFacility(JObject document)
        {
            JObject facility = Facility(document); Require(facility.Value<string>("state") == "ACTIVE", "P10_BLACKSMITH_INACTIVE"); return catalog.Facility(facility.Value<int>("level"));
        }
        private static JObject Facility(JObject document) => document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_BLACKSMITH");
        private static JObject Inventory(JObject document) => (JObject)document["payload"]!["inventory"]!;
        private static JObject Growth(JObject document) => (JObject)document["payload"]!["equipmentGrowth"]!;
        private static JObject SelectActor(JObject document, string id) => id == null ? document["payload"]!["mercenaries"]!.Children<JObject>().OrderBy(value => value.Value<string>("instanceId"), StringComparer.Ordinal).FirstOrDefault() : document["payload"]!["mercenaries"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id);
        private static JObject RequireActor(JObject document, string id)
        {
            JObject actor = SelectActor(document, id) ?? throw new EquipmentGrowthDomainException("P10_ACTOR_NOT_FOUND"); Require(actor["autonomy"]!.Value<string>("state") == "IDLE_TOWN", "P10_ACTOR_NOT_IDLE_TOWN"); return actor;
        }
        private static JObject RequireMutableEquipment(JObject document, string id, bool allowPending)
        {
            JObject value = Inventory(document)["equipment"]!.Children<JObject>().SingleOrDefault(item => item.Value<string>("instanceId") == id) ?? throw new EquipmentGrowthDomainException("P10_EQUIPMENT_NOT_FOUND");
            Require(!value.Value<bool>("locked"), "P10_EQUIPMENT_LOCKED"); Require(value["equippedByMercenaryInstanceId"]!.Type == JTokenType.Null, "P10_EQUIPMENT_EQUIPPED");
            if (!allowPending) Require(value["pendingRefineOption"]!.Type == JTokenType.Null, "P10_REFINE_PENDING_EXISTS"); return value;
        }
        private static void Debit(JObject actor, JObject inventory, string itemId, long quantity, long gold)
        {
            Require(actor.Value<long>("personalGold") >= gold, "P10_PERSONAL_GOLD_INSUFFICIENT"); JObject stack = inventory["itemStacks"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == itemId);
            Require(stack != null && stack.Value<long>("quantity") >= quantity, "P10_MATERIAL_INSUFFICIENT"); actor["personalGold"] = actor.Value<long>("personalGold") - gold; long remaining = stack.Value<long>("quantity") - quantity; if (remaining == 0) stack.Remove(); else stack["quantity"] = remaining;
        }
        private static void AddInvestment(JObject equipment, string itemId, long quantity)
        {
            JArray values = (JArray)equipment["enhancementMaterialInvested"]!; JObject line = values.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == itemId);
            if (line == null) values.Add(new JObject { ["itemId"] = itemId, ["quantity"] = quantity }); else line["quantity"] = checked(line.Value<long>("quantity") + quantity);
            equipment["enhancementMaterialInvested"] = new JArray(values.Children<JObject>().OrderBy(value => value.Value<string>("itemId"), StringComparer.Ordinal));
        }
        private void AddStacks(JObject document, IReadOnlyDictionary<string, long> returns)
        {
            JArray stacks = (JArray)Inventory(document)["itemStacks"]!; int warehouseLevel = document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_WAREHOUSE").Value<int>("level");
            int additions = returns.Count(pair => pair.Value > 0 && !stacks.Children<JObject>().Any(value => value.Value<string>("itemId") == pair.Key)); Require(stacks.Count + additions <= catalog.ItemStackCapacity(warehouseLevel), "P10_INVENTORY_CAPACITY");
            foreach (KeyValuePair<string, long> pair in returns.Where(pair => pair.Value > 0)) { JObject line = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == pair.Key); if (line == null) stacks.Add(new JObject { ["itemId"] = pair.Key, ["quantity"] = pair.Value }); else line["quantity"] = checked(line.Value<long>("quantity") + pair.Value); }
            Inventory(document)["itemStacks"] = new JArray(stacks.Children<JObject>().OrderBy(value => value.Value<string>("itemId"), StringComparer.Ordinal));
        }
        private static void AddReturn(IDictionary<string, long> values, string id, long quantity) { if (quantity <= 0) return; values[id] = checked((values.TryGetValue(id, out long current) ? current : 0) + quantity); }
        private static uint Random(Guid operationId, long ordinal, string purpose)
        {
            using SHA256 sha = SHA256.Create(); byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"KT|P10_GROWTH_V1|{operationId:D}|{ordinal.ToString(CultureInfo.InvariantCulture)}|{purpose}")); return BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(0, 4));
        }
        private static void VerifyHash(string expected, string actual) { Require(FixedTimeEquals(expected, actual), "P10_OPERATION_HASH_MISMATCH"); }
        private static bool FixedTimeEquals(string left, string right) => left != null && right != null && left.Length == right.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
        private static void Require(bool condition, string code) { if (!condition) throw new EquipmentGrowthDomainException(code); }
        private void EnsureReady() { if (!IsBootstrapped || catalog == null || game == null) throw new EquipmentGrowthDomainException("P10_CONTENT_MISSING"); }
        private static string FormatUtc(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        private static string Sha256(byte[] value) { using SHA256 sha = SHA256.Create(); return string.Concat(sha.ComputeHash(value).Select(item => item.ToString("x2", CultureInfo.InvariantCulture))); }

        private sealed class Mutation
        {
            public string Code; public string EquipmentId; public string ActorId; public int? LevelBefore; public int? LevelAfter; public string OptionId; public int OptionValue;
            public string[] EquipmentIds; public IReadOnlyDictionary<string, long> Returns; public string TransactionType; public long GoldDelta; public string MaterialId; public long MaterialQuantity;
            public static Mutation Basic(string code, string equipment, string actor, string option, int value) => new() { Code = code, EquipmentId = equipment, ActorId = actor, OptionId = option, OptionValue = value };
            public static Mutation Economy(string code, string equipment, string actor, int? before, int? after, string option, int value, long gold, string tx, string material, long quantity) => new() { Code = code, EquipmentId = equipment, ActorId = actor, LevelBefore = before, LevelAfter = after, OptionId = option, OptionValue = value, GoldDelta = gold, TransactionType = tx, MaterialId = material, MaterialQuantity = quantity };
            public static Mutation Dismantle(string[] equipment, string actor, IReadOnlyDictionary<string, long> returns) => new() { Code = "P10_DISMANTLED", EquipmentIds = equipment, ActorId = actor, Returns = returns };
            public JObject Result(Guid operationId, long before, long after) => new()
            {
                ["operationId"] = operationId.ToString("D"), ["revisionBefore"] = before, ["revisionAfter"] = after, ["resultCode"] = Code,
                ["equipmentInstanceId"] = EquipmentId == null ? JValue.CreateNull() : EquipmentId, ["equipmentInstanceIds"] = EquipmentIds == null ? new JArray() : new JArray(EquipmentIds),
                ["actorMercenaryInstanceId"] = ActorId, ["levelBefore"] = LevelBefore.HasValue ? LevelBefore.Value : JValue.CreateNull(), ["levelAfter"] = LevelAfter.HasValue ? LevelAfter.Value : JValue.CreateNull(),
                ["optionId"] = OptionId == null ? JValue.CreateNull() : OptionId, ["optionValueBps"] = OptionValue,
                ["personalGoldDelta"] = GoldDelta, ["materialReturns"] = Returns == null ? new JArray() : new JArray(Returns.Select(pair => new JObject { ["itemId"] = pair.Key, ["quantity"] = pair.Value }))
            };
        }
    }
}
