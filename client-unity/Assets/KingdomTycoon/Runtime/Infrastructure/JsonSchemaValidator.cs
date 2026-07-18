using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure
{
    public sealed class JsonSchemaValidator
    {
        private static readonly Regex UuidPattern = new(
            "^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex UuidV7Pattern = new(
            "^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex SemVerPattern = new(
            "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
            RegexOptions.CultureInvariant);

        public ValidationReport Validate(JToken instance, JObject schema, string source)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            var report = new ValidationReport();
            ValidateNode(instance, schema, schema, source ?? string.Empty, string.Empty, report);
            return report;
        }

        private static void ValidateNode(
            JToken instance,
            JToken schemaToken,
            JObject rootSchema,
            string source,
            string pointer,
            ValidationReport report)
        {
            if (schemaToken.Type == JTokenType.Boolean)
            {
                if (!schemaToken.Value<bool>())
                {
                    AddSchemaError(report, source, pointer, "Value is forbidden by the schema.");
                }

                return;
            }

            if (schemaToken is not JObject schema)
            {
                AddSchemaError(report, source, pointer, "Schema node must be an object or boolean.");
                return;
            }

            if (schema.TryGetValue("$ref", out JToken referenceToken))
            {
                string reference = referenceToken.Value<string>();
                if (!TryResolveReference(rootSchema, reference, out JToken referencedSchema))
                {
                    AddSchemaError(report, source, pointer, $"Unresolved schema reference: {reference}.");
                    return;
                }

                ValidateNode(instance, referencedSchema, rootSchema, source, pointer, report);
                return;
            }

            ValidateCombinators(instance, schema, rootSchema, source, pointer, report);

            if (schema.TryGetValue("type", out JToken typeToken) && !MatchesType(instance, typeToken))
            {
                AddSchemaError(report, source, pointer, $"Expected type {typeToken.ToString(Newtonsoft.Json.Formatting.None)} but found {instance.Type}.");
                return;
            }

            if (schema.TryGetValue("const", out JToken constToken) && !JToken.DeepEquals(instance, constToken))
            {
                AddSchemaError(report, source, pointer, "Value does not match const.");
            }

            if (schema["enum"] is JArray enumValues && !enumValues.Any(value => JToken.DeepEquals(instance, value)))
            {
                AddSchemaError(report, source, pointer, "Value is outside the allowed enum.");
            }

            switch (instance.Type)
            {
                case JTokenType.Object:
                    ValidateObject((JObject)instance, schema, rootSchema, source, pointer, report);
                    break;
                case JTokenType.Array:
                    ValidateArray((JArray)instance, schema, rootSchema, source, pointer, report);
                    break;
                case JTokenType.String:
                    ValidateString(instance.Value<string>(), schema, source, pointer, report);
                    break;
                case JTokenType.Integer:
                case JTokenType.Float:
                    ValidateNumber(instance, schema, source, pointer, report);
                    break;
            }
        }

        private static void ValidateCombinators(
            JToken instance,
            JObject schema,
            JObject rootSchema,
            string source,
            string pointer,
            ValidationReport report)
        {
            if (schema["allOf"] is JArray allOf)
            {
                foreach (JToken childSchema in allOf)
                {
                    ValidateNode(instance, childSchema, rootSchema, source, pointer, report);
                }
            }

            if (schema["anyOf"] is JArray anyOf && !anyOf.Any(child => IsValid(instance, child, rootSchema)))
            {
                AddSchemaError(report, source, pointer, "Value does not match any anyOf branch.");
            }

            if (schema["oneOf"] is JArray oneOf && oneOf.Count(child => IsValid(instance, child, rootSchema)) != 1)
            {
                AddSchemaError(report, source, pointer, "Value must match exactly one oneOf branch.");
            }

            if (schema.TryGetValue("not", out JToken notSchema) && IsValid(instance, notSchema, rootSchema))
            {
                AddSchemaError(report, source, pointer, "Value matches a forbidden not schema.");
            }
        }

        private static void ValidateObject(
            JObject instance,
            JObject schema,
            JObject rootSchema,
            string source,
            string pointer,
            ValidationReport report)
        {
            if (schema["minProperties"]?.Value<int>() is int minimum && instance.Count < minimum)
            {
                AddSchemaError(report, source, pointer, $"Object has fewer than {minimum} properties.");
            }

            if (schema["maxProperties"]?.Value<int>() is int maximum && instance.Count > maximum)
            {
                AddSchemaError(report, source, pointer, $"Object has more than {maximum} properties.");
            }

            if (schema["required"] is JArray required)
            {
                foreach (string propertyName in required.Values<string>())
                {
                    if (instance.Property(propertyName, StringComparison.Ordinal) == null)
                    {
                        AddSchemaError(report, source, AppendPointer(pointer, propertyName), "Required property is missing.");
                    }
                }
            }

            JObject properties = schema["properties"] as JObject;
            foreach (JProperty property in instance.Properties())
            {
                JToken propertySchema = properties?.Property(property.Name, StringComparison.Ordinal)?.Value;
                if (propertySchema != null)
                {
                    ValidateNode(property.Value, propertySchema, rootSchema, source, AppendPointer(pointer, property.Name), report);
                    continue;
                }

                JToken additionalProperties = schema["additionalProperties"];
                if (additionalProperties?.Type == JTokenType.Boolean && !additionalProperties.Value<bool>())
                {
                    AddSchemaError(report, source, AppendPointer(pointer, property.Name), "Unknown property is forbidden.");
                }
                else if (additionalProperties is JObject)
                {
                    ValidateNode(property.Value, additionalProperties, rootSchema, source, AppendPointer(pointer, property.Name), report);
                }
            }
        }

        private static void ValidateArray(
            JArray instance,
            JObject schema,
            JObject rootSchema,
            string source,
            string pointer,
            ValidationReport report)
        {
            if (schema["minItems"]?.Value<int>() is int minimum && instance.Count < minimum)
            {
                AddSchemaError(report, source, pointer, $"Array has fewer than {minimum} items.");
            }

            if (schema["maxItems"]?.Value<int>() is int maximum && instance.Count > maximum)
            {
                AddSchemaError(report, source, pointer, $"Array has more than {maximum} items.");
            }

            if (schema["uniqueItems"]?.Value<bool>() == true)
            {
                for (int left = 0; left < instance.Count; left++)
                {
                    for (int right = left + 1; right < instance.Count; right++)
                    {
                        if (JToken.DeepEquals(instance[left], instance[right]))
                        {
                            AddSchemaError(report, source, AppendPointer(pointer, right.ToString(CultureInfo.InvariantCulture)), "Array item must be unique.");
                        }
                    }
                }
            }

            if (schema.TryGetValue("items", out JToken itemSchema))
            {
                for (int index = 0; index < instance.Count; index++)
                {
                    ValidateNode(instance[index], itemSchema, rootSchema, source, AppendPointer(pointer, index.ToString(CultureInfo.InvariantCulture)), report);
                }
            }
        }

        private static void ValidateString(
            string value,
            JObject schema,
            string source,
            string pointer,
            ValidationReport report)
        {
            int scalarLength = CountUnicodeScalars(value);
            if (schema["minLength"]?.Value<int>() is int minimum && scalarLength < minimum)
            {
                AddSchemaError(report, source, pointer, $"String is shorter than {minimum} Unicode scalars.");
            }

            if (schema["maxLength"]?.Value<int>() is int maximum && scalarLength > maximum)
            {
                AddSchemaError(report, source, pointer, $"String is longer than {maximum} Unicode scalars.");
            }

            if (schema["pattern"]?.Value<string>() is string pattern && !Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant))
            {
                AddSchemaError(report, source, pointer, "String does not match the required pattern.");
            }

            if (schema["format"]?.Value<string>() is string format && !MatchesFormat(value, format))
            {
                AddSchemaError(report, source, pointer, $"String does not match format {format}.");
            }
        }

        private static void ValidateNumber(
            JToken value,
            JObject schema,
            string source,
            string pointer,
            ValidationReport report)
        {
            decimal number;
            try
            {
                number = value.Value<decimal>();
            }
            catch (Exception)
            {
                AddSchemaError(report, source, pointer, "Number cannot be represented as a finite decimal.");
                return;
            }

            if (schema["minimum"]?.Value<decimal>() is decimal minimum && number < minimum)
            {
                AddSchemaError(report, source, pointer, $"Number is less than {minimum.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (schema["maximum"]?.Value<decimal>() is decimal maximum && number > maximum)
            {
                AddSchemaError(report, source, pointer, $"Number is greater than {maximum.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (schema["exclusiveMinimum"]?.Value<decimal>() is decimal exclusiveMinimum && number <= exclusiveMinimum)
            {
                AddSchemaError(report, source, pointer, $"Number must be greater than {exclusiveMinimum.ToString(CultureInfo.InvariantCulture)}.");
            }

            if (schema["exclusiveMaximum"]?.Value<decimal>() is decimal exclusiveMaximum && number >= exclusiveMaximum)
            {
                AddSchemaError(report, source, pointer, $"Number must be less than {exclusiveMaximum.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        private static bool MatchesType(JToken instance, JToken typeToken)
        {
            if (typeToken.Type == JTokenType.Array)
            {
                return typeToken.Values<string>().Any(type => MatchesType(instance, type));
            }

            return MatchesType(instance, typeToken.Value<string>());
        }

        private static bool MatchesType(JToken instance, string type)
        {
            return type switch
            {
                "null" => instance.Type == JTokenType.Null,
                "object" => instance.Type == JTokenType.Object,
                "array" => instance.Type == JTokenType.Array,
                "string" => instance.Type == JTokenType.String,
                "boolean" => instance.Type == JTokenType.Boolean,
                "integer" => instance.Type == JTokenType.Integer,
                "number" => instance.Type is JTokenType.Integer or JTokenType.Float,
                _ => false
            };
        }

        private static bool MatchesFormat(string value, string format)
        {
            switch (format)
            {
                case "uuid":
                    return UuidPattern.IsMatch(value);
                case "uuid-v7":
                    return UuidV7Pattern.IsMatch(value);
                case "semver":
                    return SemVerPattern.IsMatch(value);
                case "utc-instant":
                    return DateTimeOffset.TryParseExact(
                        value,
                        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out _);
                default:
                    return true;
            }
        }

        private static int CountUnicodeScalars(string value)
        {
            int count = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsHighSurrogate(character) &&
                    index + 1 < value.Length &&
                    char.IsLowSurrogate(value[index + 1]))
                {
                    index++;
                }

                count++;
            }

            return count;
        }

        private static bool IsValid(JToken instance, JToken schema, JObject rootSchema)
        {
            var report = new ValidationReport();
            ValidateNode(instance, schema, rootSchema, string.Empty, string.Empty, report);
            return report.IsValid;
        }

        private static bool TryResolveReference(JObject rootSchema, string reference, out JToken schema)
        {
            schema = null;
            if (string.IsNullOrEmpty(reference) || !reference.StartsWith("#/", StringComparison.Ordinal))
            {
                return false;
            }

            JToken current = rootSchema;
            foreach (string rawSegment in reference.Substring(2).Split('/'))
            {
                string segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
                current = current?[segment];
                if (current == null)
                {
                    return false;
                }
            }

            schema = current;
            return true;
        }

        private static string AppendPointer(string pointer, string segment)
        {
            return pointer + "/" + segment.Replace("~", "~0").Replace("/", "~1");
        }

        private static void AddSchemaError(ValidationReport report, string source, string pointer, string message)
        {
            report.AddError("SAVE_SCHEMA_INVALID", source, string.IsNullOrEmpty(pointer) ? "/" : pointer, message);
        }
    }
}
