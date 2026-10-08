namespace ServiceControl.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Text.Json;

    static class WebhookSettingsParser
    {
        public static WebhookTarget[] Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException e)
            {
                throw new WebhookConfigurationException($"The webhook configuration is not valid JSON: {e.Message}", e);
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new WebhookConfigurationException("The webhook configuration must be a JSON array of webhook objects.");
                }

                var targets = new List<WebhookTarget>();
                var index = 0;
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    index++;
                    targets.Add(ParseTarget(element, index));
                }

                var duplicate = targets.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
                if (duplicate != null)
                {
                    throw new WebhookConfigurationException($"Webhook names must be unique. '{duplicate.Key}' is used more than once.");
                }

                return [.. targets];
            }
        }

        static WebhookTarget ParseTarget(JsonElement element, int index)
        {
            var position = $"Webhook #{index}";

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new WebhookConfigurationException($"{position} must be a JSON object.");
            }

            string name = null;
            string url = null;
            Dictionary<string, string> headers = [];
            string template = null;
            HashSet<WebhookAlertType> alerts = null;

            foreach (var property in element.EnumerateObject())
            {
                switch (property.Name.ToLowerInvariant())
                {
                    case "name":
                        name = ReadString(property, position)?.Trim();
                        break;
                    case "url":
                        url = ReadString(property, position)?.Trim();
                        break;
                    case "headers":
                        headers = ReadHeaders(property, position);
                        break;
                    case "template":
                        template = ReadTemplate(property, position);
                        break;
                    case "alerts":
                        alerts = ReadAlerts(property, position);
                        break;
                    default:
                        throw new WebhookConfigurationException($"{position} contains the unknown property '{property.Name}'. Supported properties are Name, Url, Headers, Template, and Alerts.");
                }
            }

            if (name != null)
            {
                if (name.Length == 0)
                {
                    throw new WebhookConfigurationException($"{position} has an empty Name.");
                }

                position = $"Webhook '{name}'";
            }

            // The URL can contain secrets, so it is intentionally not included in error messages
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new WebhookConfigurationException($"{position} must have a Url that is an absolute http or https URL.");
            }

            return new WebhookTarget
            {
                Name = name ?? $"webhook-{index}",
                Url = uri,
                Headers = headers,
                Template = template,
                TemplateError = template == null ? null : JustTemplateTransformer.Validate(template),
                Alerts = alerts
            };
        }

        static string ReadString(JsonProperty property, string position)
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new WebhookConfigurationException($"{position} property '{property.Name}' must be a string.");
            }

            return property.Value.GetString();
        }

        static Dictionary<string, string> ReadHeaders(JsonProperty property, string position)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                return headers;
            }

            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                throw new WebhookConfigurationException($"{position} property 'Headers' must be a JSON object of header names and string values.");
            }

            foreach (var header in property.Value.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(header.Name))
                {
                    throw new WebhookConfigurationException($"{position} contains a header with an empty name.");
                }

                if (header.Value.ValueKind != JsonValueKind.String)
                {
                    throw new WebhookConfigurationException($"{position} header '{header.Name}' must have a string value.");
                }

                var name = header.Name.Trim();
                var value = header.Value.GetString();

                if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
                {
                    throw new WebhookConfigurationException($"{position} header '{name}' must not contain line breaks.");
                }

                // Headers are added to either the request or its content, depending on the header
                using var probe = new HttpRequestMessage { Content = new ByteArrayContent([]) };
                if (!probe.Headers.TryAddWithoutValidation(name, value) && !probe.Content.Headers.TryAddWithoutValidation(name, value))
                {
                    throw new WebhookConfigurationException($"{position} contains the invalid header name '{name}'.");
                }

                headers[name] = value;
            }

            return headers;
        }

        static string ReadTemplate(JsonProperty property, string position)
        {
            // Embedding the template as JSON is the most readable option, while a string allows a template to be copied as-is
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                return property.Value.GetRawText();
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }

            throw new WebhookConfigurationException($"{position} property 'Template' must be a JSON object or a string containing a JSON object.");
        }

        static HashSet<WebhookAlertType> ReadAlerts(JsonProperty property, string position)
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            var supported = string.Join(", ", Enum.GetNames<WebhookAlertType>());

            if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() == 0)
            {
                throw new WebhookConfigurationException($"{position} property 'Alerts' must be a non-empty array containing any of: {supported}. Omit it to receive all alerts.");
            }

            var alerts = new HashSet<WebhookAlertType>();
            foreach (var item in property.Value.EnumerateArray())
            {
                var match = item.ValueKind == JsonValueKind.String
                    ? Enum.GetNames<WebhookAlertType>().FirstOrDefault(n => string.Equals(n, item.GetString()?.Trim(), StringComparison.OrdinalIgnoreCase))
                    : null;

                if (match == null)
                {
                    throw new WebhookConfigurationException($"{position} property 'Alerts' contains the unsupported value {item.GetRawText()}. Supported values are: {supported}.");
                }

                alerts.Add(Enum.Parse<WebhookAlertType>(match));
            }

            return alerts;
        }
    }

    class WebhookConfigurationException(string message, Exception innerException = null) : Exception(message, innerException);
}
