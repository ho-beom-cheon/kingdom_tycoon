using System;
using KingdomTycoon.Application.Facilities.Commands;
using KingdomTycoon.Infrastructure.Save;

namespace KingdomTycoon.Infrastructure.Facilities
{
    public sealed class FacilityRequestHasher : IFacilityRequestHasher
    {
        public string ComputeHash(IFacilityOperationHashInput input) =>
            Rfc8785Canonicalizer.ComputeSha256(input?.ToJson() ?? throw new ArgumentNullException(nameof(input)));
    }

    public sealed class SystemUuidV7Provider : IUuidV7Provider
    {
        public Guid NewId() => Guid.Parse(UuidV7.NewString(DateTimeOffset.UtcNow));
    }
}
