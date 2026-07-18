using System;
using System.Diagnostics;

namespace KingdomTycoon.Application.Abstractions
{
    public interface ITrustedUtcClock
    {
        DateTimeOffset UtcNow { get; }
        long MonotonicTicks { get; }
    }

    public sealed class SystemTrustedUtcClock : ITrustedUtcClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public long MonotonicTicks => Stopwatch.GetTimestamp();
    }

    public sealed class DeterministicClock : ITrustedUtcClock
    {
        private DateTimeOffset current;
        private long ticks;

        public DeterministicClock(DateTimeOffset initial)
        {
            current = initial.ToUniversalTime();
        }

        public DateTimeOffset UtcNow => current;
        public long MonotonicTicks => ticks;

        public void Advance(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
            current += elapsed;
            ticks += elapsed.Ticks;
        }
    }
}
