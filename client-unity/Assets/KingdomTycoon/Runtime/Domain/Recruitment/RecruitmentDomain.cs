using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomTycoon.Domain.Recruitment
{
    public sealed class RecruitmentDomainException : Exception
    {
        public RecruitmentDomainException(string code) : base(code) => Code = code;
        public RecruitmentDomainException(string code, string detail) : base(string.IsNullOrEmpty(detail) ? code : $"{code}: {detail}") => Code = code;
        public string Code { get; }
    }

    public static class RecruitmentWeightSelector
    {
        public static T Select<T>(IReadOnlyList<T> values, Func<T, int> weight, ulong cursor)
        {
            if (values == null || values.Count == 0) throw new RecruitmentDomainException("P13_POOL_EMPTY");
            long total = values.Sum(value => (long)weight(value));
            if (total <= 0) throw new RecruitmentDomainException("P13_POOL_WEIGHT_INVALID");
            long remaining = (long)(cursor % (ulong)total);
            foreach (T value in values)
            {
                int current = weight(value);
                if (current <= 0) throw new RecruitmentDomainException("P13_POOL_WEIGHT_INVALID");
                remaining -= current;
                if (remaining < 0) return value;
            }
            throw new RecruitmentDomainException("P13_POOL_WEIGHT_INVALID");
        }
    }

    public static class TavernHireCost
    {
        public static long Calculate(long baseCost, int multiplierBps)
        {
            if (baseCost < 0 || multiplierBps <= 0) throw new RecruitmentDomainException("P13_HIRE_COST_INVALID");
            return checked((baseCost * multiplierBps + 9999) / 10000);
        }
    }
}
