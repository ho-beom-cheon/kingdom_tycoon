using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KingdomTycoon.Infrastructure.Content
{
    public struct SplitMix64
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private ulong state;

        public SplitMix64(ulong seed)
        {
            state = seed;
        }

        public ulong NextUInt64()
        {
            ulong value = unchecked(state += Gamma);
            value = unchecked((value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL);
            value = unchecked((value ^ (value >> 27)) * 0x94D049BB133111EBUL);
            return value ^ (value >> 31);
        }

        public ulong NextBounded(ulong bound)
        {
            if (bound == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bound), "Bound must be positive.");
            }

            ulong threshold = unchecked(0UL - bound) % bound;
            while (true)
            {
                ulong raw = NextUInt64();
                if (raw >= threshold)
                {
                    return raw % bound;
                }
            }
        }
    }

    public static class ContentSeedDerivation
    {
        public static ulong DeriveUInt64(string canonicalInput)
        {
            if (canonicalInput == null)
            {
                throw new ArgumentNullException(nameof(canonicalInput));
            }

            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(new UTF8Encoding(false, true).GetBytes(canonicalInput));
            return ((ulong)hash[0] << 56) |
                   ((ulong)hash[1] << 48) |
                   ((ulong)hash[2] << 40) |
                   ((ulong)hash[3] << 32) |
                   ((ulong)hash[4] << 24) |
                   ((ulong)hash[5] << 16) |
                   ((ulong)hash[6] << 8) |
                   hash[7];
        }

        public static int GrowthFactorBps(string contentVersion, ulong growthSeed, string stat)
        {
            string input = string.Join("|", "KT", "GROWTH_FACTOR_V1", contentVersion, growthSeed.ToString(CultureInfo.InvariantCulture), stat);
            var random = new SplitMix64(DeriveUInt64(input));
            return checked(9_500 + (int)random.NextBounded(1_001));
        }

        public static string Sha256Hex(string canonicalInput)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(new UTF8Encoding(false, true).GetBytes(canonicalInput));
            var output = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                output.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return output.ToString();
        }
    }
}
