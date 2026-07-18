using System;

namespace KingdomTycoon.Domain.Facilities
{
    public enum FacilityState { LOCKED, BUILDABLE, BUILDING, ACTIVE, UPGRADING, STOPPED }
    public enum FacilityJobStatus { RUNNING, READY, CLAIMED, CANCELLED }
    public enum FacilityStopReason { NONE, NPC_REQUIRED, NPC_PROFESSION_MISMATCH, PHASE_LOCKED }

    public sealed class FacilityCommandException : InvalidOperationException
    {
        public FacilityCommandException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public static class FacilityTransitionPolicy
    {
        public static void Require(bool condition, string errorCode)
        {
            if (!condition) throw new FacilityCommandException(errorCode);
        }
    }
}
