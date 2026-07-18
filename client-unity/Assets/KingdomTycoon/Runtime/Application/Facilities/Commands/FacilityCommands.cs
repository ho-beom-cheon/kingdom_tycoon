using System;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Application.Facilities.Commands
{
    public interface IUuidV7Provider
    {
        Guid NewId();
    }

    public interface IFacilityOperationHashInput
    {
        JObject ToJson();
    }

    public interface IFacilityRequestHasher
    {
        string ComputeHash(IFacilityOperationHashInput input);
    }

    public abstract class FacilityCommand : IFacilityOperationHashInput
    {
        protected FacilityCommand(Guid operationId, string requestHash, long expectedRevision, string facilityId)
        {
            OperationId = operationId;
            RequestHash = requestHash;
            ExpectedRevision = expectedRevision;
            FacilityId = facilityId ?? throw new ArgumentNullException(nameof(facilityId));
        }

        public Guid OperationId { get; }
        public string RequestHash { get; }
        public long ExpectedRevision { get; }
        public string FacilityId { get; }
        public abstract JObject ToJson();
    }

    public sealed class StartFacilityBuildCommand : FacilityCommand
    {
        public StartFacilityBuildCommand(Guid operationId, string requestHash, long expectedRevision, string facilityId, int targetLevel)
            : base(operationId, requestHash, expectedRevision, facilityId) => TargetLevel = targetLevel;
        public int TargetLevel { get; }
        public override JObject ToJson() => FacilityHashJson.Start("START_FACILITY_BUILD", OperationId, ExpectedRevision, FacilityId, "targetLevel", TargetLevel);
    }

    public sealed class StartFacilityUpgradeCommand : FacilityCommand
    {
        public StartFacilityUpgradeCommand(Guid operationId, string requestHash, long expectedRevision, string facilityId, int targetLevel)
            : base(operationId, requestHash, expectedRevision, facilityId) => TargetLevel = targetLevel;
        public int TargetLevel { get; }
        public override JObject ToJson() => FacilityHashJson.Start("START_FACILITY_UPGRADE", OperationId, ExpectedRevision, FacilityId, "targetLevel", TargetLevel);
    }

    public sealed class ClaimFacilityJobCommand : FacilityCommand
    {
        public ClaimFacilityJobCommand(Guid operationId, string requestHash, long expectedRevision, string facilityId, Guid facilityJobOperationId)
            : base(operationId, requestHash, expectedRevision, facilityId) => FacilityJobOperationId = facilityJobOperationId;
        public Guid FacilityJobOperationId { get; }
        public override JObject ToJson() => FacilityHashJson.Start("CLAIM_FACILITY_JOB", OperationId, ExpectedRevision, FacilityId, "facilityJobOperationId", FacilityJobOperationId.ToString("D"));
    }

    public sealed class AssignManagementNpcCommand : FacilityCommand
    {
        public AssignManagementNpcCommand(Guid operationId, string requestHash, long expectedRevision, string facilityId, Guid npcInstanceId)
            : base(operationId, requestHash, expectedRevision, facilityId) => NpcInstanceId = npcInstanceId;
        public Guid NpcInstanceId { get; }
        public override JObject ToJson() => FacilityHashJson.Start("ASSIGN_MANAGEMENT_NPC", OperationId, ExpectedRevision, FacilityId, "npcInstanceId", NpcInstanceId.ToString("D"));
    }

    public sealed class UnassignManagementNpcCommand : FacilityCommand
    {
        public UnassignManagementNpcCommand(Guid operationId, string requestHash, long expectedRevision, string facilityId, Guid npcInstanceId)
            : base(operationId, requestHash, expectedRevision, facilityId) => NpcInstanceId = npcInstanceId;
        public Guid NpcInstanceId { get; }
        public override JObject ToJson() => FacilityHashJson.Start("UNASSIGN_MANAGEMENT_NPC", OperationId, ExpectedRevision, FacilityId, "npcInstanceId", NpcInstanceId.ToString("D"));
    }

    internal static class FacilityHashJson
    {
        public static JObject Start(string type, Guid operationId, long revision, string facilityId, string field, JToken value) => new()
        {
            ["commandType"] = type,
            ["operationId"] = operationId.ToString("D"),
            ["expectedRevision"] = revision,
            ["facilityId"] = facilityId,
            [field] = value
        };
    }

    public sealed class FacilityOperationRequestFactory
    {
        private readonly IUuidV7Provider ids;
        private readonly IFacilityRequestHasher hasher;

        public FacilityOperationRequestFactory(IUuidV7Provider ids, IFacilityRequestHasher hasher)
        {
            this.ids = ids ?? throw new ArgumentNullException(nameof(ids));
            this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        }

        public StartFacilityBuildCommand CreateBuild(long revision, string facilityId)
        {
            Guid operationId = ids.NewId();
            var draft = new StartFacilityBuildCommand(operationId, null, revision, facilityId, 1);
            return new StartFacilityBuildCommand(operationId, hasher.ComputeHash(draft), revision, facilityId, 1);
        }

        public StartFacilityUpgradeCommand CreateUpgrade(long revision, string facilityId, int targetLevel)
        {
            Guid operationId = ids.NewId();
            var draft = new StartFacilityUpgradeCommand(operationId, null, revision, facilityId, targetLevel);
            return new StartFacilityUpgradeCommand(operationId, hasher.ComputeHash(draft), revision, facilityId, targetLevel);
        }

        public ClaimFacilityJobCommand CreateClaim(long revision, string facilityId, Guid facilityJobOperationId)
        {
            Guid operationId = ids.NewId();
            var draft = new ClaimFacilityJobCommand(operationId, null, revision, facilityId, facilityJobOperationId);
            return new ClaimFacilityJobCommand(operationId, hasher.ComputeHash(draft), revision, facilityId, facilityJobOperationId);
        }

        public AssignManagementNpcCommand CreateAssign(long revision, string facilityId, Guid npcInstanceId)
        {
            Guid operationId = ids.NewId();
            var draft = new AssignManagementNpcCommand(operationId, null, revision, facilityId, npcInstanceId);
            return new AssignManagementNpcCommand(operationId, hasher.ComputeHash(draft), revision, facilityId, npcInstanceId);
        }

        public UnassignManagementNpcCommand CreateUnassign(long revision, string facilityId, Guid npcInstanceId)
        {
            Guid operationId = ids.NewId();
            var draft = new UnassignManagementNpcCommand(operationId, null, revision, facilityId, npcInstanceId);
            return new UnassignManagementNpcCommand(operationId, hasher.ComputeHash(draft), revision, facilityId, npcInstanceId);
        }
    }
}
