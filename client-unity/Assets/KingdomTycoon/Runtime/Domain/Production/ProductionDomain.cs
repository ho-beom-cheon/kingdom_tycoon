using System;

namespace KingdomTycoon.Domain.Production
{
    public sealed class ProductionDomainException : InvalidOperationException
    {
        public ProductionDomainException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public static class ProductionMath
    {
        public static long EfficientQuantity(long baseQuantity, int efficiencyBps)
        {
            if (baseQuantity < 1 || efficiencyBps is < 1 or > 20000) throw new ProductionDomainException("P09_CONTENT_INVALID");
            return checked(baseQuantity * efficiencyBps + 9999) / 10000;
        }

        public static long DurationTicks(long baseTicks, int facilitySpeedBps, int npcSpeedBps)
        {
            if (baseTicks < 1 || facilitySpeedBps < 1 || npcSpeedBps < 1) throw new ProductionDomainException("P09_CONTENT_INVALID");
            long numerator = checked(baseTicks * 10000L * 10000L);
            long denominator = checked((long)facilitySpeedBps * npcSpeedBps);
            return Math.Max(1, checked(numerator + denominator - 1) / denominator);
        }

        public static int ClampTarget(string productKind, int requested, int emergencyFloor, int maximum)
        {
            if (requested < 0 || requested > maximum) throw new ProductionDomainException("P09_TARGET_INVALID");
            return productKind == "POTION" ? Math.Max(emergencyFloor, requested) : requested;
        }
    }
}
