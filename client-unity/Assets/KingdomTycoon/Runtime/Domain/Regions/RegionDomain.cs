using System;

namespace KingdomTycoon.Domain.Regions
{
    public sealed class RegionDomainException : Exception
    {
        public RegionDomainException(string code) : base(code) { Code = code; }
        public string Code { get; }
    }

    public static class RegionProgressMath
    {
        public static int Apply(int current, int victoryProgress, int eliteBonusProgress, int eliteKills, int cap)
        {
            if (current is < 0 or > 100 || victoryProgress < 0 || eliteBonusProgress < 0 || eliteKills < 0 || cap is < 0 or > 100)
                throw new RegionDomainException("P12_SETTLEMENT_INVALID");
            long next = checked((long)current + victoryProgress + (long)eliteBonusProgress * eliteKills);
            return (int)Math.Min(cap, next);
        }
    }
}
