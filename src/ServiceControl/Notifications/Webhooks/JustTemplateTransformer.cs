namespace ServiceControl.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using JUST;

    /// <summary>
    /// Applies user-supplied JUST.net templates to webhook payloads.
    /// </summary>
    static class JustTemplateTransformer
    {
        public static string Validate(string template)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return "The template is empty.";
            }

            try
            {
                using var document = JsonDocument.Parse(template);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return "The template must be a JSON object.";
                }
            }
            catch (JsonException e)
            {
                return $"The template is not valid JSON: {e.Message}";
            }

            // #customfunction invokes arbitrary static .NET methods. Checking without the '#' also catches
            // attempts to assemble the function name at runtime, e.g. #xconcat(#,customfunction(...))
            if (template.Contains("customfunction", StringComparison.OrdinalIgnoreCase))
            {
                return "The template uses #customfunction, which is not supported because it can execute arbitrary code.";
            }

            return null;
        }

        public static string Transform(string template, string inputJson)
        {
            var validationError = Validate(template);
            if (validationError != null)
            {
                throw new WebhookTemplateException(validationError);
            }

            try
            {
                var input = Rewrite(JsonNode.Parse(inputJson), '#', Sentinel);
                var output = new JsonTransformer(new JUSTContext()).Transform(template, input?.ToJsonString() ?? "null");
                return Rewrite(JsonNode.Parse(output), Sentinel, '#')?.ToJsonString(OutputOptions) ?? "null";
            }
            catch (Exception e)
            {
                // JUST.net throws a wide variety of exception types for template problems
                throw new WebhookTemplateException($"The template could not be applied: {e.GetBaseException().Message}", e);
            }
        }

        // JUST.net evaluates any string *value* starting with '#' that a function such as #valueof returns.
        // Payload values come from failed messages (e.g. exception messages and headers) that anyone able to
        // send a message controls, so a value like "#customfunction(...)" would otherwise execute arbitrary code.
        // Every '#' in the input is therefore swapped for a private-use character before transforming and
        // swapped back in the output.
        static JsonNode Rewrite(JsonNode node, char from, char to) => node switch
        {
            null => null,
            JsonObject obj => new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key.Replace(from, to), Rewrite(p.Value, from, to)))),
            JsonArray array => new JsonArray([.. array.Select(item => Rewrite(item, from, to))]),
            JsonValue value when value.GetValueKind() == JsonValueKind.String => JsonValue.Create(value.GetValue<string>().Replace(from, to)),
            _ => node.DeepClone()
        };

        const char Sentinel = '\uE023';

        static readonly JsonSerializerOptions OutputOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    }

    class WebhookTemplateException(string message, Exception innerException = null) : Exception(message, innerException);
}
