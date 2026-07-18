using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Inventory
{
    public abstract class InventoryCommand
    {
        protected InventoryCommand(Guid operationId, long expectedRevision, string requestHash)
        { OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash ?? string.Empty; }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public abstract string CommandType { get; }
        public abstract JObject ToHashJson();
    }

    public sealed class PotionTransferCommand : InventoryCommand
    {
        public PotionTransferCommand(string commandType, Guid operationId, long expectedRevision, string requestHash, string mercenaryId, string potionId, long quantity)
            : base(operationId, expectedRevision, requestHash)
        { CommandType = commandType; MercenaryId = mercenaryId; PotionId = potionId; Quantity = quantity; }
        public override string CommandType { get; }
        public string MercenaryId { get; }
        public string PotionId { get; }
        public long Quantity { get; }
        public override JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["mercenaryInstanceId"] = MercenaryId, ["potionId"] = PotionId, ["quantity"] = Quantity
        };
    }

    public sealed class EquipItemCommand : InventoryCommand
    {
        public EquipItemCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryId, string equipmentInstanceId)
            : base(operationId, expectedRevision, requestHash)
        { MercenaryId = mercenaryId; EquipmentInstanceId = equipmentInstanceId; }
        public override string CommandType => "EQUIP_ITEM";
        public string MercenaryId { get; }
        public string EquipmentInstanceId { get; }
        public override JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["mercenaryInstanceId"] = MercenaryId, ["equipmentInstanceId"] = EquipmentInstanceId
        };
    }

    public sealed class UnequipItemCommand : InventoryCommand
    {
        public UnequipItemCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryId, string slot)
            : base(operationId, expectedRevision, requestHash)
        { MercenaryId = mercenaryId; Slot = slot; }
        public override string CommandType => "UNEQUIP_ITEM";
        public string MercenaryId { get; }
        public string Slot { get; }
        public override JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["mercenaryInstanceId"] = MercenaryId, ["slot"] = Slot
        };
    }

    public sealed class SellInventoryCommand : InventoryCommand
    {
        public SellInventoryCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryId, string kind, string id, long quantity)
            : base(operationId, expectedRevision, requestHash)
        { MercenaryId = mercenaryId; Kind = kind; Id = id; Quantity = quantity; }
        public override string CommandType => "SELL_INVENTORY";
        public string MercenaryId { get; }
        public string Kind { get; }
        public string Id { get; }
        public long Quantity { get; }
        public override JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["mercenaryInstanceId"] = MercenaryId,
            ["lines"] = new JArray(new JObject { ["kind"] = Kind, ["id"] = Id, ["quantity"] = Quantity })
        };
    }

    public sealed class SetInventoryPoliciesCommand : InventoryCommand
    {
        public SetInventoryPoliciesCommand(Guid operationId, long expectedRevision, string requestHash, JObject policyPatch)
            : base(operationId, expectedRevision, requestHash) => PolicyPatch = (JObject)(policyPatch ?? throw new ArgumentNullException(nameof(policyPatch))).DeepClone();
        public override string CommandType => "SET_INVENTORY_POLICIES";
        public JObject PolicyPatch { get; }
        public override JObject ToHashJson() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision,
            ["policy"] = PolicyPatch.DeepClone()
        };
    }

    public sealed class InventoryOperationResult
    {
        public InventoryOperationResult(Guid operationId, long revisionBefore, long revisionAfter, string resultDigest, bool replayed, long personalGoldDelta = 0)
        { OperationId = operationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; ResultDigest = resultDigest; Replayed = replayed; PersonalGoldDelta = personalGoldDelta; }
        public Guid OperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public string ResultDigest { get; }
        public bool Replayed { get; }
        public long PersonalGoldDelta { get; }
    }

    public sealed class InventoryItemDto
    {
        public InventoryItemDto(string kind, string id, string name, long quantity, int tier, string quality, bool locked, bool equipped, long score)
        { Kind = kind; Id = id; Name = name; Quantity = quantity; Tier = tier; Quality = quality; Locked = locked; Equipped = equipped; Score = score; }
        public string Kind { get; }
        public string Id { get; }
        public string Name { get; }
        public long Quantity { get; }
        public int Tier { get; }
        public string Quality { get; }
        public bool Locked { get; }
        public bool Equipped { get; }
        public long Score { get; }
    }

    public sealed class InventorySnapshotDto
    {
        public InventorySnapshotDto(long revision, int itemSlots, int equipmentSlots, int potionSlots, IReadOnlyList<InventoryItemDto> items)
        { Revision = revision; ItemSlots = itemSlots; EquipmentSlots = equipmentSlots; PotionSlots = potionSlots; Items = items ?? Array.Empty<InventoryItemDto>(); }
        public long Revision { get; }
        public int ItemSlots { get; }
        public int EquipmentSlots { get; }
        public int PotionSlots { get; }
        public IReadOnlyList<InventoryItemDto> Items { get; }
        public int TotalRows => Items.Count;
    }
}
