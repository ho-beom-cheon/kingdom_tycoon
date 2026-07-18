using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure
{
    public static class StrictJson
    {
        public static JToken Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            using var stringReader = new StringReader(json);
            using var reader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal
            };

            JToken token = JToken.Load(
                reader,
                new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Load,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    LineInfoHandling = LineInfoHandling.Load
                });
            if (ContainsComment(token))
            {
                throw new JsonReaderException("JSON comments are forbidden.");
            }

            if (reader.Read())
            {
                throw new JsonReaderException("Trailing JSON content is forbidden.");
            }

            return token;
        }

        private static bool ContainsComment(JToken token)
        {
            if (token.Type == JTokenType.Comment)
            {
                return true;
            }

            foreach (JToken child in token.Children())
            {
                if (ContainsComment(child))
                {
                    return true;
                }
            }

            return false;
        }

        public static JObject ParseObject(string json)
        {
            JToken token = Parse(json);
            return token as JObject
                ?? throw new JsonReaderException("The JSON root must be an object.");
        }
    }
}
