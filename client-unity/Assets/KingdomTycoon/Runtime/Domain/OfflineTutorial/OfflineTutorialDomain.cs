using System;

namespace KingdomTycoon.Domain.OfflineTutorial
{
    public sealed class OfflineTutorialDomainException : Exception
    {
        public OfflineTutorialDomainException(string code, string message = null) : base(message ?? code) => Code = code;
        public string Code { get; }
    }

    public sealed class OfflineWindow
    {
        public OfflineWindow(string status, long elapsedSeconds, long eligibleSeconds) { Status = status; ElapsedSeconds = elapsedSeconds; EligibleSeconds = eligibleSeconds; }
        public string Status { get; }
        public long ElapsedSeconds { get; }
        public long EligibleSeconds { get; }
    }

    public static class OfflineWindowPolicy
    {
        public static OfflineWindow Evaluate(DateTimeOffset cursorUtc, DateTimeOffset nowUtc, long minimumSeconds, long maximumSeconds, long rollbackToleranceSeconds)
        {
            if (minimumSeconds < 0 || maximumSeconds < minimumSeconds || rollbackToleranceSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(minimumSeconds));
            long elapsed = checked((long)Math.Floor((nowUtc - cursorUtc).TotalSeconds));
            if (elapsed < -rollbackToleranceSeconds) return new OfflineWindow("CLOCK_ROLLBACK", 0, 0);
            elapsed = Math.Max(0, elapsed);
            if (elapsed < minimumSeconds) return new OfflineWindow("BELOW_MINIMUM", elapsed, 0);
            return new OfflineWindow(elapsed > maximumSeconds ? "CAPPED" : "APPLIED", elapsed, Math.Min(elapsed, maximumSeconds));
        }

        public static long Efficient(long seconds, int basisPoints) => checked(seconds * basisPoints / 10000L);
    }
}
