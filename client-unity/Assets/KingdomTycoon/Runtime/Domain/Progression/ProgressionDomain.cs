using System;
using System.Collections.Generic;
using System.Linq;

namespace KingdomTycoon.Domain.Progression
{
    public sealed class ProgressionDomainException : Exception
    {
        public ProgressionDomainException(string errorCode) : base(errorCode) => ErrorCode = errorCode;
        public string ErrorCode { get; }
    }

    public readonly struct LevelProgress
    {
        public LevelProgress(int levelBefore, int levelAfter, long experienceAfter)
        { LevelBefore = levelBefore; LevelAfter = levelAfter; ExperienceAfter = experienceAfter; }
        public int LevelBefore { get; }
        public int LevelAfter { get; }
        public long ExperienceAfter { get; }
    }

    public static class ProgressionMath
    {
        public static IReadOnlyDictionary<string, long> AllocateExperience(long totalExperience, IEnumerable<string> partyIds)
        {
            if (totalExperience < 0) throw new ProgressionDomainException("P11_CONTENT_INVALID");
            string[] ids = (partyIds ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ProgressionDomainException("P11_CONTENT_INVALID");
            long share = totalExperience / ids.Length;
            long remainder = totalExperience % ids.Length;
            return ids.Select((id, index) => new KeyValuePair<string, long>(id, checked(share + (index < remainder ? 1 : 0))))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }

        public static LevelProgress ApplyExperience(string rankId, int level, long experience, long gained,
            Func<string, int> maximumLevel, Func<string, int, long> experienceToNext)
        {
            if (level < 1 || experience < 0 || gained < 0) throw new ProgressionDomainException("P11_CONTENT_INVALID");
            int before = level;
            int maximum = maximumLevel(rankId);
            long current = checked(experience + gained);
            while (level < maximum)
            {
                long required = experienceToNext(rankId, level);
                if (required <= 0) throw new ProgressionDomainException("P11_CONTENT_INVALID");
                if (current < required) break;
                current -= required;
                level++;
            }
            if (level == maximum) current = 0;
            return new LevelProgress(before, level, current);
        }

        public static long ScaleCost(long baseCost, int multiplierBps)
        {
            if (baseCost < 0 || multiplierBps < 0) throw new ProgressionDomainException("P11_CONTENT_INVALID");
            return checked((baseCost * multiplierBps + 9_999L) / 10_000L);
        }
    }
}
