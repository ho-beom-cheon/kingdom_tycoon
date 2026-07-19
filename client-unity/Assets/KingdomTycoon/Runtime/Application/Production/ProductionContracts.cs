using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Production
{
    public abstract class ProductionCommand
    {
        protected ProductionCommand(Guid operationId, long expectedRevision, string requestHash)
        { OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash ?? string.Empty; }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public abstract string CommandType { get; }
        public abstract JObject ToHashJson();
        protected JObject Header() => new() { ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"), ["expectedRevision"] = ExpectedRevision };
    }

    public sealed class SetStockTargetCommand : ProductionCommand
    {
        public SetStockTargetCommand(Guid operationId, long expectedRevision, string requestHash, string targetId, int targetQuantity, int priority, bool enabled) : base(operationId, expectedRevision, requestHash)
        { TargetId = targetId; TargetQuantity = targetQuantity; Priority = priority; Enabled = enabled; }
        public override string CommandType => "SET_STOCK_TARGET";
        public string TargetId { get; }
        public int TargetQuantity { get; }
        public int Priority { get; }
        public bool Enabled { get; }
        public override JObject ToHashJson() { JObject value = Header(); value["targetId"] = TargetId; value["targetQuantity"] = TargetQuantity; value["priority"] = Priority; value["enabled"] = Enabled; return value; }
    }

    public sealed class EnqueueProductionCommand : ProductionCommand
    {
        public EnqueueProductionCommand(Guid operationId, long expectedRevision, string requestHash, string recipeId, int quantity) : base(operationId, expectedRevision, requestHash)
        { RecipeId = recipeId; Quantity = quantity; }
        public override string CommandType => "ENQUEUE_PRODUCTION";
        public string RecipeId { get; }
        public int Quantity { get; }
        public override JObject ToHashJson() { JObject value = Header(); value["recipeId"] = RecipeId; value["quantity"] = Quantity; return value; }
    }

    public sealed class AdvanceProductionTicksCommand : ProductionCommand
    {
        public AdvanceProductionTicksCommand(Guid operationId, long expectedRevision, string requestHash, int ticks) : base(operationId, expectedRevision, requestHash) => Ticks = ticks;
        public override string CommandType => "ADVANCE_PRODUCTION_TICKS";
        public int Ticks { get; }
        public override JObject ToHashJson() { JObject value = Header(); value["ticks"] = Ticks; return value; }
    }

    public sealed class RunProductionAutomationCommand : ProductionCommand
    {
        public RunProductionAutomationCommand(Guid operationId, long expectedRevision, string requestHash) : base(operationId, expectedRevision, requestHash) { }
        public override string CommandType => "RUN_PRODUCTION_AUTOMATION";
        public override JObject ToHashJson() => Header();
    }

    public sealed class EnqueueTreatmentCommand : ProductionCommand
    {
        public EnqueueTreatmentCommand(Guid operationId, long expectedRevision, string requestHash, string mercenaryInstanceId) : base(operationId, expectedRevision, requestHash) => MercenaryInstanceId = mercenaryInstanceId;
        public override string CommandType => "ENQUEUE_TREATMENT";
        public string MercenaryInstanceId { get; }
        public override JObject ToHashJson() { JObject value = Header(); value["mercenaryInstanceId"] = MercenaryInstanceId; return value; }
    }

    public sealed class ProductionOperationResult
    {
        public ProductionOperationResult(Guid operationId, long revisionBefore, long revisionAfter, bool replayed, int completed, string resultDigest)
        { OperationId = operationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; Replayed = replayed; Completed = completed; ResultDigest = resultDigest; }
        public Guid OperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Replayed { get; }
        public int Completed { get; }
        public string ResultDigest { get; }
    }

    public sealed class StockTargetDto
    {
        public StockTargetDto(string id, int priority, string kind, string productId, string recipeId, int current, int queued, int target, bool enabled, string stopReason)
        { Id = id; Priority = priority; Kind = kind; ProductId = productId; RecipeId = recipeId; Current = current; Queued = queued; Target = target; Enabled = enabled; StopReason = stopReason; }
        public string Id { get; }
        public int Priority { get; }
        public string Kind { get; }
        public string ProductId { get; }
        public string RecipeId { get; }
        public int Current { get; }
        public int Queued { get; }
        public int Target { get; }
        public bool Enabled { get; }
        public string StopReason { get; }
    }

    public sealed class ProductionFacilityDto
    {
        public ProductionFacilityDto(string facilityId, string state, int level, string npcProfession, string proficiency, long xp, string stoppedReason, int queueCount, int queueCapacity, long remainingTicks)
        { FacilityId = facilityId; State = state; Level = level; NpcProfession = npcProfession; Proficiency = proficiency; Xp = xp; StoppedReason = stoppedReason; QueueCount = queueCount; QueueCapacity = queueCapacity; RemainingTicks = remainingTicks; }
        public string FacilityId { get; }
        public string State { get; }
        public int Level { get; }
        public string NpcProfession { get; }
        public string Proficiency { get; }
        public long Xp { get; }
        public string StoppedReason { get; }
        public int QueueCount { get; }
        public int QueueCapacity { get; }
        public long RemainingTicks { get; }
    }

    public sealed class ProductionOverviewDto
    {
        public ProductionOverviewDto(long revision, long currentTick, IReadOnlyList<ProductionFacilityDto> facilities, IReadOnlyList<StockTargetDto> targets, int recentEvents)
        { Revision = revision; CurrentTick = currentTick; Facilities = facilities; Targets = targets; RecentEvents = recentEvents; }
        public long Revision { get; }
        public long CurrentTick { get; }
        public IReadOnlyList<ProductionFacilityDto> Facilities { get; }
        public IReadOnlyList<StockTargetDto> Targets { get; }
        public int RecentEvents { get; }
    }

    public interface IStoreStockSink
    {
        long Count(JObject document, string productKind, string productId);
        void AddStack(JObject document, string productId, long quantity, Guid operationId);
        void AddEquipment(JObject document, string equipmentTemplateId, string instanceId, string qualityId, Guid operationId, string contentVersion);
    }
}
