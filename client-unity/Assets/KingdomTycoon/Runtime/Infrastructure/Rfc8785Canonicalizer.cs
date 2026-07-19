using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure
{
    public static class Rfc8785Canonicalizer
    {
        public static byte[] Canonicalize(JToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var builder = new StringBuilder();
            Append(token, builder);
            return new UTF8Encoding(false, true).GetBytes(builder.ToString());
        }

        public static string ComputeSha256(JToken token)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Canonicalize(token));
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void Append(JToken token, StringBuilder builder)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    AppendObject((JObject)token, builder);
                    return;
                case JTokenType.Array:
                    AppendArray((JArray)token, builder);
                    return;
                case JTokenType.String:
                    AppendString(token.Value<string>(), builder);
                    return;
                case JTokenType.Integer:
                    builder.Append(Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture));
                    return;
                case JTokenType.Float:
                    AppendNumber((JValue)token, builder);
                    return;
                case JTokenType.Boolean:
                    builder.Append(token.Value<bool>() ? "true" : "false");
                    return;
                case JTokenType.Null:
                    builder.Append("null");
                    return;
                default:
                    throw new InvalidOperationException($"JCS does not support JSON token type {token.Type}.");
            }
        }

        private static void AppendObject(JObject value, StringBuilder builder)
        {
            builder.Append('{');
            bool first = true;
            foreach (JProperty property in value.Properties().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                AppendString(property.Name, builder);
                builder.Append(':');
                Append(property.Value, builder);
            }

            builder.Append('}');
        }

        private static void AppendArray(JArray value, StringBuilder builder)
        {
            builder.Append('[');
            for (int index = 0; index < value.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                Append(value[index], builder);
            }

            builder.Append(']');
        }

        private static void AppendNumber(JValue token, StringBuilder builder)
        {
            string formatted;
            if (token.Value is decimal decimalValue)
            {
                if (decimalValue == 0m && decimal.GetBits(decimalValue)[3] < 0)
                {
                    throw new InvalidOperationException("JCS input must not contain negative zero.");
                }

                formatted = decimalValue.ToString("G29", CultureInfo.InvariantCulture);
            }
            else
            {
                double doubleValue = Convert.ToDouble(token.Value, CultureInfo.InvariantCulture);
                if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
                {
                    throw new InvalidOperationException("JCS input must contain only finite numbers.");
                }

                if (doubleValue == 0d && BitConverter.DoubleToInt64Bits(doubleValue) < 0)
                {
                    throw new InvalidOperationException("JCS input must not contain negative zero.");
                }

                formatted = doubleValue.ToString("R", CultureInfo.InvariantCulture);
            }

            builder.Append(NormalizeExponent(formatted));
        }

        private static string NormalizeExponent(string value)
        {
            int exponentIndex = value.IndexOfAny(new[] { 'E', 'e' });
            if (exponentIndex < 0)
            {
                return value;
            }

            string mantissa = value.Substring(0, exponentIndex);
            string exponent = value.Substring(exponentIndex + 1);
            string sign = string.Empty;
            if (exponent.StartsWith("+", StringComparison.Ordinal) || exponent.StartsWith("-", StringComparison.Ordinal))
            {
                sign = exponent.Substring(0, 1);
                exponent = exponent.Substring(1);
            }

            exponent = exponent.TrimStart('0');
            if (exponent.Length == 0)
            {
                exponent = "0";
            }

            return $"{mantissa}e{sign}{exponent}";
        }

        private static void AppendString(string value, StringBuilder builder)
        {
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character <= 0x1f)
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
