using System;

namespace KingdomTycoon.Application.Facilities
{
    public sealed class FacilityOperationResult
    {
        public FacilityOperationResult(Guid operationId, long revision, string facilityId, string resultDigest, bool replayed)
        {
            OperationId = operationId;
            Revision = revision;
            FacilityId = facilityId;
            ResultDigest = resultDigest;
            Replayed = replayed;
        }

        public Guid OperationId { get; }
        public long Revision { get; }
        public string FacilityId { get; }
        public string ResultDigest { get; }
        public bool Replayed { get; }
    }
}
