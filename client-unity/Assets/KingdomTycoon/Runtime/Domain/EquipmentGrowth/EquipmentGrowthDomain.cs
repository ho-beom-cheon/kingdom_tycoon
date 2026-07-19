using System;

namespace KingdomTycoon.Domain.EquipmentGrowth
{
    public sealed class EquipmentGrowthDomainException : Exception
    {
        public EquipmentGrowthDomainException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public static class EquipmentGrowthMath
    {
        public static int EffectiveChanceBps(int baseChanceBps, int pityBps)
        {
            if (baseChanceBps is < 0 or > 10_000 || pityBps is < 0 or > 10_000)
                throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
            return Math.Min(10_000, checked(baseChanceBps + pityBps));
        }

        public static bool IsSuccess(uint randomValue, int chanceBps) => randomValue % 10_000 < chanceBps;

        public static int InclusiveBps(uint randomValue, int minimumBps, int maximumBps)
        {
            if (minimumBps < 0 || maximumBps < minimumBps || maximumBps > 10_000)
                throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
            return checked(minimumBps + (int)(randomValue % (uint)(maximumBps - minimumBps + 1)));
        }

        public static long DismantleBase(long quantity, int qualityBps)
        {
            if (quantity < 1 || qualityBps < 1) throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
            return Math.Max(1, checked(quantity * qualityBps / 10_000));
        }

        public static long Refund(long invested, int refundBps)
        {
            if (invested < 0 || refundBps is < 0 or > 10_000)
                throw new EquipmentGrowthDomainException("P10_CONTENT_INVALID");
            return checked(invested * refundBps / 10_000);
        }
    }
}
