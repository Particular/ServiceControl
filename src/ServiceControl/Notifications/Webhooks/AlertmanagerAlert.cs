namespace ServiceControl.Notifications.Webhooks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// An alert in the Prometheus Alertmanager v2 API format (https://prometheus.io/docs/alerting/latest/clients/).
    /// Labels identify the alert and drive routing/grouping; annotations carry descriptive context.
    /// An alert without <see cref="EndsAt"/> is firing, an alert with <see cref="EndsAt"/> is resolved.
    /// </summary>
    public class AlertmanagerAlert
    {
        public Dictionary<string, string> Labels { get; set; } = [];

        public Dictionary<string, string> Annotations { get; set; } = [];

        public DateTime? StartsAt { get; set; }

        public DateTime? EndsAt { get; set; }

        public string GeneratorUrl { get; set; }

        [JsonIgnore]
        public string AlertName => Labels.GetValueOrDefault(AlertLabels.AlertName);

        [JsonIgnore]
        public bool IsResolved => EndsAt.HasValue;

        /// <summary>
        /// Identifies the alert independent of its state, in the same way Alertmanager fingerprints alerts by their label set.
        /// </summary>
        [JsonIgnore]
        public string Fingerprint => BuildFingerprint(Labels);

        public static string ComputeDedupKey(IReadOnlyDictionary<string, string> labels) =>
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(BuildFingerprint(labels))))[..32];

        static string BuildFingerprint(IEnumerable<KeyValuePair<string, string>> labels) =>
            string.Join('\n', labels.OrderBy(l => l.Key, StringComparer.Ordinal).Select(l => $"{l.Key}={l.Value}"));
    }

    static class AlertLabels
    {
        public const string AlertName = "alertname";
        public const string Severity = "severity";
        public const string Instance = "servicecontrol_instance";
        public const string Endpoint = "endpoint";
        public const string Host = "host";
        public const string HostId = "host_id";
        public const string MessageType = "message_type";
        public const string FailedMessageId = "failed_message_id";
        public const string CustomCheckId = "custom_check_id";
        public const string Category = "category";
    }

    static class AlertAnnotations
    {
        public const string Summary = "summary";
        public const string Description = "description";
        public const string ServicePulseUrl = "servicepulse_url";
        public const string DedupKey = "dedup_key";
        public const string ExceptionType = "exception_type";
        public const string ExceptionMessage = "exception_message";
        public const string FailingAddress = "failing_address";
        public const string Host = "host";
        public const string SendingEndpoint = "sending_endpoint";
        public const string MessageId = "message_id";
        public const string ProcessingAttempts = "processing_attempts";
        public const string LastHeartbeatAt = "last_heartbeat_at";
        public const string FailureReason = "failure_reason";
    }

    /// <summary>
    /// Serializes alerts into the Alertmanager v2 <c>POST /api/v2/alerts</c> request body: a JSON array of alerts.
    /// </summary>
    static class AlertmanagerPayload
    {
        public static string Serialize(IEnumerable<AlertmanagerAlert> alerts) =>
            JsonSerializer.Serialize(alerts.Select(a => new AlertDto(
                new SortedDictionary<string, string>(a.Labels, StringComparer.Ordinal),
                new SortedDictionary<string, string>(a.Annotations, StringComparer.Ordinal),
                ToUtc(a.StartsAt),
                ToUtc(a.EndsAt),
                a.GeneratorUrl)), Options);

        public static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            // ServiceControl stores timestamps in UTC, but they can lose their kind in persistence
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };

        static DateTime? ToUtc(DateTime? value) => value.HasValue ? ToUtc(value.Value) : null;

        record AlertDto(
            [property: JsonPropertyName("labels")] SortedDictionary<string, string> Labels,
            [property: JsonPropertyName("annotations")] SortedDictionary<string, string> Annotations,
            [property: JsonPropertyName("startsAt")] DateTime? StartsAt,
            [property: JsonPropertyName("endsAt")] DateTime? EndsAt,
            [property: JsonPropertyName("generatorURL")] string GeneratorUrl);

        static readonly JsonSerializerOptions Options = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    }
}
