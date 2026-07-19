using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using KingdomTycoon.Application.Abstractions;
using KingdomTycoon.Application.Inventory;
using KingdomTycoon.Domain.Inventory;
using KingdomTycoon.Infrastructure.Content;
using KingdomTycoon.Infrastructure.Facilities;
using KingdomTycoon.Infrastructure.Save;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Inventory
{
    public sealed class CanonicalInventoryCatalog
    {
        private readonly Dictionary<string, EquipmentDefinition> equipment;
        private readonly Dictionary<string, int> qualityBps;
        private readonly Dictionary<string, int> itemPrices;
        private readonly Dictionary<string, InventoryCapacity> capacities;
        private readonly Dictionary<string, IReadOnlyDictionary<string, int>> scoreWeights;

        public CanonicalInventoryCatalog(ContentCatalog catalog)
        {
            if (catalog == null || catalog.ContentVersion is not (CompileTimeActiveContentVersionProvider.P07ContentVersion or CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion or CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion or CompileTimeActiveContentVersionProvider.P15ContentVersion))
                throw new InventoryDomainException("P07_CONTENT_MISSING");
            equipment = catalog.GetTable("equipment_templates.csv").Rows.Where(Enabled).Select(row => new EquipmentDefinition(
                row["equipment_template_id"], Int(row, "tier"), row["slot"], row["profile"], Int(row, "base_power"), row["source"]))
                .ToDictionary(value => value.Id, StringComparer.Ordinal);
            qualityBps = catalog.GetTable("equipment_qualities.csv").Rows.Where(Enabled).ToDictionary(
                row => row["quality_id"], row => checked((int)(decimal.Parse(row["stat_multiplier"], CultureInfo.InvariantCulture) * 10_000m)), StringComparer.Ordinal);
            itemPrices = catalog.GetTable("items.csv").Rows.Where(Enabled).ToDictionary(row => row["item_id"], row => Int(row, "sell_price"), StringComparer.Ordinal);
            capacities = catalog.GetTable("inventory_capacity_rules.csv").Rows.Where(Enabled).ToDictionary(row => row["warehouse_level"], row => new InventoryCapacity(
                Int(row, "item_stack_slots"), Int(row, "equipment_slots"), Int(row, "potion_stack_slots"), Int(row, "hunt_buffer_slots")), StringComparer.Ordinal);
            scoreWeights = catalog.GetTable("equipment_score_weights.csv").Rows.Where(Enabled).ToDictionary(row => row["job_id"], row =>
                (IReadOnlyDictionary<string, int>)row.Where(pair => pair.Key.EndsWith("_bps", StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => int.Parse(pair.Value, CultureInfo.InvariantCulture), StringComparer.Ordinal), StringComparer.Ordinal);
            if (equipment.Count != 80 || qualityBps.Count != 5 || capacities.Count != 4 || scoreWeights.Count != 5)
                throw new InventoryDomainException("P07_CONTENT_MISSING");
        }

        public EquipmentDefinition Equipment(string id) => equipment.TryGetValue(id, out EquipmentDefinition value) ? value : throw new InventoryDomainException("P07_CONTENT_MISSING");
        public int QualityBps(string id) => qualityBps.TryGetValue(id, out int value) ? value : throw new InventoryDomainException("P07_CONTENT_MISSING");
        public int ItemPrice(string id) => itemPrices.TryGetValue(id, out int value) ? value : throw new InventoryDomainException("P07_CONTENT_MISSING");
        public InventoryCapacity Capacity(int level) => capacities.TryGetValue(level.ToString(CultureInfo.InvariantCulture), out InventoryCapacity value) ? value : throw new InventoryDomainException("P07_CONTENT_MISSING");
        public IReadOnlyDictionary<string, int> Weights(string job) => scoreWeights.TryGetValue(job, out IReadOnlyDictionary<string, int> value) ? value : throw new InventoryDomainException("P07_CONTENT_MISSING");
        public EquipmentInstance Instance(JObject value)
        {
            (string optionId, int optionValue) = ParseRefine(value["refineOption"]);
            return new EquipmentInstance(value.Value<string>("instanceId"), Equipment(value.Value<string>("equipmentTemplateId")), value.Value<string>("qualityId"),
                QualityBps(value.Value<string>("qualityId")), value.Value<int>("enhancementLevel"), optionId, optionValue, value.Value<bool>("locked"));
        }
        private static (string, int) ParseRefine(JToken token) => token == null || token.Type == JTokenType.Null
            ? (null, 0)
            : token.Type == JTokenType.Object
                ? (token.Value<string>("optionId"), token.Value<int>("value"))
                : (token.Value<string>(), 1000);
        private static bool Enabled(IReadOnlyDictionary<string, string> row) => row["enabled"] == "TRUE";
        private static int Int(IReadOnlyDictionary<string, string> row, string key) => int.Parse(row[key], NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    public sealed class InventoryRequestHasher
    {
        public string Compute(InventoryCommand command) => Rfc8785Canonicalizer.ComputeSha256(command?.ToHashJson() ?? throw new ArgumentNullException(nameof(command)));
    }

    public sealed class InventorySettlementMutation
    {
        public int RetainedItems { get; set; }
        public int RetainedEquipment { get; set; }
        public int AutoSold { get; set; }
        public string EquippedMercenaryId { get; set; }
        public string EquipmentInstanceId { get; set; }
    }

    public sealed class InventoryGameService : IAppService
    {
        private readonly ITrustedUtcClock clock;
        private SaveService save;
        private ContentCatalogService content;
        private FacilityGameService game;
        private CanonicalInventoryCatalog catalog;

        public InventoryGameService(ITrustedUtcClock clock) => this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        public int InitializationOrder => 65;
        public bool IsBootstrapped { get; private set; }
        public event EventHandler<InventorySnapshotDto> Changed;

        public void Initialize(ServiceRegistry services)
        {
            save = services.Get<SaveService>();
            content = services.Get<ContentCatalogService>();
            game = services.Get<FacilityGameService>();
        }

        public void Bootstrap()
        {
            if (!game.IsBootstrapped || content.Catalog == null || game.CurrentDocument.Value<string>("contentVersion") is not (CompileTimeActiveContentVersionProvider.P07ContentVersion or CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion or CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion or CompileTimeActiveContentVersionProvider.P15ContentVersion))
                throw new InventoryDomainException("P07_CONTENT_MISSING");
            catalog = new CanonicalInventoryCatalog(content.Catalog);
            IsBootstrapped = true;
        }

        public InventorySnapshotDto GetSnapshot(string jobId = "JOB_WARRIOR")
        {
            EnsureReady();
            JObject document = game.Snapshot();
            JToken inventory = document["payload"]!["inventory"]!;
            int level = WarehouseLevel(document);
            InventoryCapacity capacity = catalog.Capacity(level);
            var rows = new List<InventoryItemDto>();
            rows.AddRange(inventory["itemStacks"]!.Children<JObject>().Select(value => new InventoryItemDto(
                "ITEM", value.Value<string>("itemId"), value.Value<string>("itemId"), value.Value<long>("quantity"), 0, null, false, false, 0)));
            rows.AddRange(inventory["warehousePotions"]!.Children<JObject>().Select(value => new InventoryItemDto(
                "POTION", value.Value<string>("potionId"), value.Value<string>("potionId"), value.Value<long>("quantity"), 0, null, false, false, 0)));
            foreach (JObject value in inventory["equipment"]!.Children<JObject>())
            {
                EquipmentInstance item = catalog.Instance(value);
                long score;
                try { score = EquipmentMath.Score(jobId, item, catalog.Weights(jobId)); }
                catch (InventoryDomainException) { score = -1; }
                rows.Add(new InventoryItemDto("EQUIPMENT", item.InstanceId, item.Definition.Id, 1, item.Definition.Tier, item.QualityId,
                    item.Locked, value["equippedByMercenaryInstanceId"].Type != JTokenType.Null, score));
            }
            return new InventorySnapshotDto(game.Revision, capacity.ItemStackSlots, capacity.EquipmentSlots, capacity.PotionStackSlots,
                rows.OrderBy(value => value.Kind, StringComparer.Ordinal).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray());
        }

        public EquipmentStatBlock EquipmentModifier(JObject document, JObject mercenary)
        {
            EnsureReady();
            var total = new EquipmentStatBlock();
            var byId = document["payload"]!["inventory"]!["equipment"]!.Children<JObject>()
                .ToDictionary(value => value.Value<string>("instanceId"), StringComparer.Ordinal);
            foreach (JProperty slot in ((JObject)mercenary["equipmentSlots"]!).Properties().OrderBy(value => SlotOrder(value.Name)))
            {
                if (slot.Value.Type == JTokenType.Null) continue;
                if (!byId.TryGetValue(slot.Value.Value<string>(), out JObject value)) throw new InventoryDomainException("P07_EQUIPMENT_LINK_CONFLICT");
                EquipmentInstance item = catalog.Instance(value);
                if (item.Definition.Slot != slot.Name) throw new InventoryDomainException("P07_EQUIPMENT_SLOT_WRONG");
                total.Add(EquipmentMath.FlatModifier(mercenary.Value<string>("jobId"), item));
            }
            return total;
        }

        public InventoryOperationResult TransferPotion(PotionTransferCommand command) => Mutate(command, draft =>
        {
            bool toMercenary = command.CommandType == "TRANSFER_POTION_TO_MERCENARY";
            if (!toMercenary && command.CommandType != "RETURN_POTION_TO_WAREHOUSE") throw new InventoryDomainException("P07_POTION_TRANSFER_INVALID");
            if (command.Quantity <= 0) throw new InventoryDomainException("P07_POTION_TRANSFER_INVALID");
            JObject mercenary = FindMercenary(draft, command.MercenaryId);
            RequireTown(mercenary);
            JArray warehouse = (JArray)draft["payload"]!["inventory"]!["warehousePotions"]!;
            JArray carried = (JArray)mercenary["potions"]!;
            MovePotion(toMercenary ? warehouse : carried, toMercenary ? carried : warehouse, command.PotionId, command.Quantity,
                toMercenary ? 4 : catalog.Capacity(WarehouseLevel(draft)).PotionStackSlots);
            return new JObject { ["potionId"] = command.PotionId, ["quantity"] = command.Quantity, ["direction"] = command.CommandType };
        });

        public InventoryOperationResult Equip(EquipItemCommand command) => Mutate(command, draft =>
        {
            JObject mercenary = FindMercenary(draft, command.MercenaryId); RequireTown(mercenary);
            JObject stored = FindEquipment(draft, command.EquipmentInstanceId);
            EquipmentInstance item = catalog.Instance(stored);
            EquipmentMath.FlatModifier(mercenary.Value<string>("jobId"), item);
            string currentOwner = stored["equippedByMercenaryInstanceId"].Type == JTokenType.Null ? null : stored.Value<string>("equippedByMercenaryInstanceId");
            if (currentOwner != null && currentOwner != command.MercenaryId) throw new InventoryDomainException("P07_EQUIPMENT_LINK_CONFLICT");
            JObject slots = (JObject)mercenary["equipmentSlots"]!;
            if (slots[item.Definition.Slot].Type != JTokenType.Null)
                FindEquipment(draft, slots.Value<string>(item.Definition.Slot))["equippedByMercenaryInstanceId"] = null;
            slots[item.Definition.Slot] = item.InstanceId;
            stored["equippedByMercenaryInstanceId"] = command.MercenaryId;
            return new JObject { ["mercenaryInstanceId"] = command.MercenaryId, ["equipmentInstanceId"] = item.InstanceId, ["slot"] = item.Definition.Slot };
        });

        public InventoryOperationResult Unequip(UnequipItemCommand command) => Mutate(command, draft =>
        {
            JObject mercenary = FindMercenary(draft, command.MercenaryId); RequireTown(mercenary);
            JObject slots = (JObject)mercenary["equipmentSlots"]!;
            if (!slots.ContainsKey(command.Slot) || slots[command.Slot].Type == JTokenType.Null) throw new InventoryDomainException("P07_EQUIPMENT_CANDIDATE_EMPTY");
            string id = slots.Value<string>(command.Slot);
            FindEquipment(draft, id)["equippedByMercenaryInstanceId"] = null;
            slots[command.Slot] = null;
            return new JObject { ["mercenaryInstanceId"] = command.MercenaryId, ["equipmentInstanceId"] = id, ["slot"] = command.Slot };
        });

        public InventoryOperationResult Sell(SellInventoryCommand command) => Mutate(command, draft =>
        {
            if (draft.Value<string>("contentVersion") is CompileTimeActiveContentVersionProvider.P08ContentVersion or CompileTimeActiveContentVersionProvider.P09ContentVersion or CompileTimeActiveContentVersionProvider.P10ContentVersion or CompileTimeActiveContentVersionProvider.P11ContentVersion or CompileTimeActiveContentVersionProvider.P12ContentVersion or CompileTimeActiveContentVersionProvider.P13ContentVersion or CompileTimeActiveContentVersionProvider.P14ContentVersion or CompileTimeActiveContentVersionProvider.P15ContentVersion)
                throw new InventoryDomainException("P08_LEGACY_COMMAND_RETIRED");
            if (command.Quantity <= 0) throw new InventoryDomainException("P07_POTION_TRANSFER_INVALID");
            JObject mercenary = FindMercenary(draft, command.MercenaryId); RequireTown(mercenary);
            long gold;
            if (command.Kind == "ITEM")
            {
                JArray stacks = (JArray)draft["payload"]!["inventory"]!["itemStacks"]!;
                JObject line = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == command.Id)
                    ?? throw new InventoryDomainException("P07_CONTENT_MISSING");
                if (line.Value<long>("quantity") < command.Quantity) throw new InventoryDomainException("P07_STACK_OVERFLOW");
                line["quantity"] = line.Value<long>("quantity") - command.Quantity;
                if (line.Value<long>("quantity") == 0) line.Remove();
                gold = checked(catalog.ItemPrice(command.Id) * command.Quantity);
            }
            else if (command.Kind == "EQUIPMENT")
            {
                JObject stored = FindEquipment(draft, command.Id);
                EquipmentInstance item = catalog.Instance(stored);
                JObject policy = (JObject)draft["payload"]!["kingdom"]!["inventoryPolicies"]!;
                bool protectedItem = item.Locked || stored["equippedByMercenaryInstanceId"].Type != JTokenType.Null ||
                    policy["protectQualityIds"]!.Values<string>().Contains(item.QualityId, StringComparer.Ordinal) ||
                    (policy.Value<bool>("protectBossEquipment") && item.Definition.Source == "BOSS");
                if (protectedItem) throw new InventoryDomainException("P07_ITEM_PROTECTED");
                gold = Math.Max(1, EquipmentMath.EffectivePower(item) * 2500L / 10_000L);
                stored.Remove();
            }
            else throw new InventoryDomainException("P07_REWARD_UNION_INVALID");
            mercenary["personalGold"] = checked(mercenary.Value<long>("personalGold") + gold);
            return new JObject { ["kind"] = command.Kind, ["id"] = command.Id, ["quantity"] = command.Quantity, ["personalGoldDelta"] = gold };
        }, value => value.Value<long>("personalGoldDelta"));

        public InventoryOperationResult SetPolicies(SetInventoryPoliciesCommand command) => Mutate(command, draft =>
        {
            JObject target = (JObject)draft["payload"]!["kingdom"]!["inventoryPolicies"]!;
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            { "autoSellMaxTier", "protectQualityIds", "autoEquipEnabled", "upgradeThresholdBps", "autoSellEnabled", "protectBossEquipment", "protectFirstDiscovery" };
            foreach (JProperty property in command.PolicyPatch.Properties())
            {
                if (!allowed.Contains(property.Name) || property.Value.Type == JTokenType.Null) throw new InventoryDomainException("P07_POLICY_INVALID");
                target[property.Name] = property.Value.DeepClone();
            }
            int tier = target.Value<int>("autoSellMaxTier"); int threshold = target.Value<int>("upgradeThresholdBps");
            if (tier is < 0 or > 5 || threshold is < 0 or > 10_000 || target["protectQualityIds"]!.Values<string>().Distinct(StringComparer.Ordinal).Count() != target["protectQualityIds"]!.Count())
                throw new InventoryDomainException("P07_POLICY_INVALID");
            target["protectQualityIds"] = new JArray(target["protectQualityIds"]!.Values<string>().OrderBy(value => value, StringComparer.Ordinal));
            return (JObject)target.DeepClone();
        });

        public InventorySettlementMutation ApplyTerminalLoot(JObject draft, Guid huntOperationId, int killCount, IReadOnlyList<string> party)
        {
            EnsureReady();
            var result = new InventorySettlementMutation();
            if (killCount <= 0) return result;
            JArray stacks = (JArray)draft["payload"]!["inventory"]!["itemStacks"]!;
            JObject softwood = stacks.Children<JObject>().SingleOrDefault(value => value.Value<string>("itemId") == "MAT_R01_SOFTWOOD");
            long quantity = Math.Max(1, killCount * 2L);
            if (softwood == null) stacks.Add(new JObject { ["itemId"] = "MAT_R01_SOFTWOOD", ["quantity"] = quantity });
            else softwood["quantity"] = checked(softwood.Value<long>("quantity") + quantity);
            result.RetainedItems = 1;
            foreach (JObject mercenary in draft["payload"]!["mercenaries"]!.Children<JObject>().Where(value => party.Contains(value.Value<string>("instanceId"), StringComparer.Ordinal)))
                mercenary["records"]!["itemsCollected"] = checked(mercenary["records"]!.Value<long>("itemsCollected") + quantity);

            JObject policies = (JObject)draft["payload"]!["kingdom"]!["inventoryPolicies"]!;
            bool discovered = policies["discoveredEquipmentTemplateIds"]!.Values<string>().Contains("EQ_T1_CLERIC_WEAPON", StringComparer.Ordinal);
            if (!discovered)
            {
                JObject cleric = draft["payload"]!["mercenaries"]!.Children<JObject>()
                    .FirstOrDefault(value => party.Contains(value.Value<string>("instanceId"), StringComparer.Ordinal) && value.Value<string>("jobId") == "JOB_CLERIC");
                string instanceId = UuidV7.NewString(clock.UtcNow);
                var item = new JObject
                {
                    ["instanceId"] = instanceId, ["equipmentTemplateId"] = "EQ_T1_CLERIC_WEAPON", ["tier"] = 1,
                    ["qualityId"] = "QUALITY_COMMON", ["enhancementLevel"] = 0, ["refineOption"] = null, ["locked"] = true,
                    ["equippedByMercenaryInstanceId"] = cleric?.Value<string>("instanceId"),
                    ["sourceContentVersion"] = content.Catalog.ContentVersion,
                    ["generationOperationId"] = huntOperationId.ToString("D")
                };
                ((JArray)draft["payload"]!["inventory"]!["equipment"]!).Add(item);
                ((JArray)policies["discoveredEquipmentTemplateIds"]!).Add("EQ_T1_CLERIC_WEAPON");
                if (cleric != null) cleric["equipmentSlots"]!["WEAPON"] = instanceId;
                result.RetainedEquipment = 1; result.EquipmentInstanceId = instanceId; result.EquippedMercenaryId = cleric?.Value<string>("instanceId");
            }
            InventoryCapacity capacity = catalog.Capacity(WarehouseLevel(draft));
            capacity.Require(stacks.Count, draft["payload"]!["inventory"]!["equipment"]!.Count(), draft["payload"]!["inventory"]!["warehousePotions"]!.Count(), 0);
            return result;
        }

        public void Shutdown()
        { Changed = null; catalog = null; game = null; content = null; save = null; IsBootstrapped = false; }

        private InventoryOperationResult Mutate(InventoryCommand command, Func<JObject, JObject> mutation, Func<JObject, long> gold = null)
        {
            EnsureReady();
            if (command == null) throw new ArgumentNullException(nameof(command));
            VerifyHash(command.RequestHash, new InventoryRequestHasher().Compute(command));
            JObject existing = FindJournal(command.OperationId);
            if (existing != null)
            {
                VerifyHash(command.RequestHash, existing.Value<string>("requestHash"));
                return new InventoryOperationResult(command.OperationId, game.Revision, game.Revision, existing.Value<string>("resultDigest"), true);
            }
            if (command.ExpectedRevision != game.Revision) throw new InventoryDomainException("P07_SAVE_REVISION_CONFLICT");
            long before = game.Revision;
            JObject draft = game.Snapshot();
            JObject payload = mutation(draft);
            long goldDelta = gold?.Invoke(payload) ?? 0;
            var digestInput = new JObject
            {
                ["operationId"] = command.OperationId.ToString("D"), ["revisionBefore"] = before, ["revisionAfter"] = before + 1,
                ["commandType"] = command.CommandType, ["payload"] = payload, ["replayed"] = false
            };
            string digest = Rfc8785Canonicalizer.ComputeSha256(digestInput);
            AppendJournal(draft, command.OperationId, command.RequestHash, digest, clock.UtcNow);
            SaveWriteResult written = save.Repository.Save(game.ActiveProfileId, draft, before, clock.UtcNow);
            if (!written.Success) throw new InventoryDomainException(written.ErrorCode == "SAVE_REVISION_CONFLICT" ? "P07_SAVE_REVISION_CONFLICT" : "P07_SAVE_WRITE_FAILED");
            game.SynchronizeCommittedDocument(written.Document);
            InventorySnapshotDto snapshot = GetSnapshot(); Changed?.Invoke(this, snapshot);
            return new InventoryOperationResult(command.OperationId, before, game.Revision, digest, false, goldDelta);
        }

        private static void MovePotion(JArray source, JArray destination, string potionId, long quantity, int distinctCapacity)
        {
            JObject from = source.Children<JObject>().SingleOrDefault(value => value.Value<string>("potionId") == potionId)
                ?? throw new InventoryDomainException("P07_POTION_EMPTY");
            if (from.Value<long>("quantity") < quantity) throw new InventoryDomainException("P07_POTION_EMPTY");
            JObject to = destination.Children<JObject>().SingleOrDefault(value => value.Value<string>("potionId") == potionId);
            if (to == null && destination.Count >= distinctCapacity) throw new InventoryDomainException("P07_CAPACITY_EXCEEDED");
            from["quantity"] = from.Value<long>("quantity") - quantity;
            if (from.Value<long>("quantity") == 0) from.Remove();
            if (to == null) destination.Add(new JObject { ["potionId"] = potionId, ["quantity"] = quantity });
            else to["quantity"] = checked(to.Value<long>("quantity") + quantity);
        }

        private JObject FindJournal(Guid id) => game.Snapshot()["payload"]!["operationJournal"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("operationId") == id.ToString("D"));
        private static JObject FindMercenary(JObject document, string id) => document["payload"]!["mercenaries"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id) ?? throw new InventoryDomainException("P07_CONTENT_MISSING");
        private static JObject FindEquipment(JObject document, string id) => document["payload"]!["inventory"]!["equipment"]!.Children<JObject>().SingleOrDefault(value => value.Value<string>("instanceId") == id) ?? throw new InventoryDomainException("P07_EQUIPMENT_CANDIDATE_EMPTY");
        private static void RequireTown(JObject mercenary) { if (mercenary["autonomy"]!.Value<string>("state") is not ("IDLE_TOWN" or "PROMOTION_READY" or "INJURED")) throw new InventoryDomainException("P07_COMBAT_STATE_FORBIDDEN"); }
        private static int WarehouseLevel(JObject document) => document["payload"]!["facilities"]!.Children<JObject>().Single(value => value.Value<string>("facilityId") == "FAC_WAREHOUSE").Value<int>("level");
        private static int SlotOrder(string slot) => slot switch { "WEAPON" => 0, "ARMOR" => 1, "HELMET" => 2, "ACCESSORY" => 3, _ => 99 };
        private static void VerifyHash(string expected, string actual)
        {
            byte[] left = System.Text.Encoding.ASCII.GetBytes(expected ?? string.Empty); byte[] right = System.Text.Encoding.ASCII.GetBytes(actual ?? string.Empty);
            if (left.Length != right.Length || !CryptographicOperations.FixedTimeEquals(left, right)) throw new InventoryDomainException("P07_OPERATION_DUPLICATE_MISMATCH");
        }
        private static void AppendJournal(JObject document, Guid operationId, string requestHash, string digest, DateTimeOffset now)
        {
            string timestamp = now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            ((JArray)document["payload"]!["operationJournal"]!).Add(new JObject
            {
                ["operationId"] = operationId.ToString("D"), ["operationType"] = "REWARD", ["facilityJobType"] = null,
                ["requestHash"] = requestHash, ["status"] = "COMMITTED", ["createdAtUtc"] = timestamp, ["updatedAtUtc"] = timestamp,
                ["completedAtUtc"] = timestamp, ["serverReceiptId"] = null, ["errorCode"] = null, ["resultDigest"] = digest,
                ["failureResolution"] = null, ["resolvedAtUtc"] = null
            });
        }
        private void EnsureReady() { if (!IsBootstrapped || catalog == null) throw new InventoryDomainException("P07_CONTENT_MISSING"); }
    }
}
