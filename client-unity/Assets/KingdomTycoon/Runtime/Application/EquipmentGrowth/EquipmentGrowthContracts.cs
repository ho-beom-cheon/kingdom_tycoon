using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.EquipmentGrowth
{
    public abstract class EquipmentGrowthCommand
    {
        protected EquipmentGrowthCommand(Guid operationId, long expectedRevision, string requestHash, string actorMercenaryInstanceId, string equipmentInstanceId)
        {
            OperationId = operationId; ExpectedRevision = expectedRevision; RequestHash = requestHash ?? string.Empty;
            ActorMercenaryInstanceId = actorMercenaryInstanceId; EquipmentInstanceId = equipmentInstanceId;
        }
        public Guid OperationId { get; }
        public long ExpectedRevision { get; }
        public string RequestHash { get; }
        public string ActorMercenaryInstanceId { get; }
        public string EquipmentInstanceId { get; }
        public abstract string CommandType { get; }
        public abstract JObject ToHashJson();
        protected JObject Header() => new()
        {
            ["commandType"] = CommandType, ["operationId"] = OperationId.ToString("D"),
            ["expectedRevision"] = ExpectedRevision, ["actorMercenaryInstanceId"] = ActorMercenaryInstanceId,
            ["equipmentInstanceId"] = EquipmentInstanceId
        };
    }

    public sealed class EnhanceEquipmentCommand : EquipmentGrowthCommand
    {
        public EnhanceEquipmentCommand(Guid operationId, long expectedRevision, string requestHash, string actorId, string equipmentId)
            : base(operationId, expectedRevision, requestHash, actorId, equipmentId) { }
        public override string CommandType => "ENHANCE_EQUIPMENT";
        public override JObject ToHashJson() => Header();
    }

    public sealed class RollRefineOptionCommand : EquipmentGrowthCommand
    {
        public RollRefineOptionCommand(Guid operationId, long expectedRevision, string requestHash, string actorId, string equipmentId, string refineOptionId)
            : base(operationId, expectedRevision, requestHash, actorId, equipmentId) => RefineOptionId = refineOptionId;
        public string RefineOptionId { get; }
        public override string CommandType => "ROLL_REFINE_OPTION";
        public override JObject ToHashJson() { JObject value = Header(); value["refineOptionId"] = RefineOptionId; return value; }
    }

    public sealed class ResolveRefineOptionCommand : EquipmentGrowthCommand
    {
        public ResolveRefineOptionCommand(Guid operationId, long expectedRevision, string requestHash, string actorId, string equipmentId, bool acceptCandidate)
            : base(operationId, expectedRevision, requestHash, actorId, equipmentId) => AcceptCandidate = acceptCandidate;
        public bool AcceptCandidate { get; }
        public override string CommandType => "RESOLVE_REFINE_OPTION";
        public override JObject ToHashJson() { JObject value = Header(); value["acceptCandidate"] = AcceptCandidate; return value; }
    }

    public sealed class DismantleEquipmentCommand : EquipmentGrowthCommand
    {
        public DismantleEquipmentCommand(Guid operationId, long expectedRevision, string requestHash, string actorId, IEnumerable<string> equipmentIds, bool confirmProtected)
            : base(operationId, expectedRevision, requestHash, actorId, null)
        { EquipmentInstanceIds = (equipmentIds ?? Array.Empty<string>()).ToArray(); ConfirmProtected = confirmProtected; }
        public IReadOnlyList<string> EquipmentInstanceIds { get; }
        public bool ConfirmProtected { get; }
        public override string CommandType => "DISMANTLE_EQUIPMENT";
        public override JObject ToHashJson()
        {
            JObject value = Header(); value.Remove("equipmentInstanceId");
            value["equipmentInstanceIds"] = new JArray(EquipmentInstanceIds); value["confirmProtected"] = ConfirmProtected; return value;
        }
    }

    public sealed class EquipmentGrowthOperationResult
    {
        public EquipmentGrowthOperationResult(Guid operationId, long revisionBefore, long revisionAfter, bool replayed, string resultCode, string resultDigest, JObject payload)
        { OperationId = operationId; RevisionBefore = revisionBefore; RevisionAfter = revisionAfter; Replayed = replayed; ResultCode = resultCode; ResultDigest = resultDigest; Payload = payload; }
        public Guid OperationId { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public bool Replayed { get; }
        public string ResultCode { get; }
        public string ResultDigest { get; }
        public JObject Payload { get; }
    }

    public sealed class GrowthEquipmentDto
    {
        public GrowthEquipmentDto(string id, string templateId, int tier, string qualityId, int enhancementLevel, int pityBps,
            string refineOptionId, int refineValueBps, string pendingOptionId, int pendingValueBps, bool locked, bool equipped, long power)
        {
            Id = id; TemplateId = templateId; Tier = tier; QualityId = qualityId; EnhancementLevel = enhancementLevel; PityBps = pityBps;
            RefineOptionId = refineOptionId; RefineValueBps = refineValueBps; PendingOptionId = pendingOptionId; PendingValueBps = pendingValueBps;
            Locked = locked; Equipped = equipped; Power = power;
        }
        public string Id { get; }
        public string TemplateId { get; }
        public int Tier { get; }
        public string QualityId { get; }
        public int EnhancementLevel { get; }
        public int PityBps { get; }
        public string RefineOptionId { get; }
        public int RefineValueBps { get; }
        public string PendingOptionId { get; }
        public int PendingValueBps { get; }
        public bool Locked { get; }
        public bool Equipped { get; }
        public long Power { get; }
    }

    public sealed class EquipmentGrowthOverviewDto
    {
        public EquipmentGrowthOverviewDto(long revision, string facilityState, int facilityLevel, string actorId, long personalGold,
            IReadOnlyList<GrowthEquipmentDto> equipment, string lastResult)
        { Revision = revision; FacilityState = facilityState; FacilityLevel = facilityLevel; ActorId = actorId; PersonalGold = personalGold; Equipment = equipment; LastResult = lastResult; }
        public long Revision { get; }
        public string FacilityState { get; }
        public int FacilityLevel { get; }
        public string ActorId { get; }
        public long PersonalGold { get; }
        public IReadOnlyList<GrowthEquipmentDto> Equipment { get; }
        public string LastResult { get; }
    }
}
