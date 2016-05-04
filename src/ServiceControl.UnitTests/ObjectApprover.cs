namespace ServiceControl.UnitTests
{
    using System;
    using System.IO;
    using System.Text;
    using ApprovalTests;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public static class ObjectApprover
    {
        public static JsonSerializer JsonSerializer;

        static ObjectApprover()
        {
            JsonSerializer = new JsonSerializer
            {
                Formatting = Formatting.Indented
            };
            JsonSerializer.Converters.Add(new StringEnumConverter());
        }

        public static void VerifyWithJson(object target)
        {
            VerifyWithJson(target, s => s);
        }

        public static void VerifyWithJson(object target, Func<string, string> scrubber)
        {
            var formatJson = AsFormattedJson(target);
            Approvals.Verify(formatJson, scrubber);
        }

        public static string AsFormattedJson(object target)
        {
            var stringBuilder = new StringBuilder();
            using (var stringWriter = new StringWriter(stringBuilder))
            {
                using (var jsonWriter = new JsonTextWriter(stringWriter))
                {
                    jsonWriter.Formatting = JsonSerializer.Formatting;
                    JsonSerializer.Serialize(jsonWriter, target);
                }
                return stringWriter.ToString();
            }
        }
    }
}