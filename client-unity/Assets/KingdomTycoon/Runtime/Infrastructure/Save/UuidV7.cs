using System;
using System.Globalization;
using System.Security.Cryptography;

namespace KingdomTycoon.Infrastructure.Save
{
    public static class UuidV7
    {
        public static string NewString(DateTimeOffset timestamp)
        {
            long milliseconds = timestamp.ToUnixTimeMilliseconds();
            if (milliseconds < 0 || milliseconds > 0x0000FFFFFFFFFFFFL)
            {
                throw new ArgumentOutOfRangeException(nameof(timestamp));
            }

            var bytes = new byte[16];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            for (int index = 5; index >= 0; index--)
            {
                bytes[index] = (byte)(milliseconds & 0xff);
                milliseconds >>= 8;
            }

            bytes[6] = (byte)((bytes[6] & 0x0f) | 0x70);
            bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
            return string.Create(
                36,
                bytes,
                static (destination, value) =>
                {
                    int target = 0;
                    for (int source = 0; source < value.Length; source++)
                    {
                        if (source is 4 or 6 or 8 or 10)
                        {
                            destination[target++] = '-';
                        }

                        string hex = value[source].ToString("x2", CultureInfo.InvariantCulture);
                        destination[target++] = hex[0];
                        destination[target++] = hex[1];
                    }
                });
        }
    }
}
