namespace Particular.ThroughputCollector.Contracts
{
    using System.Text.Json.Serialization;

    class AuditCount
    {
        [JsonPropertyName("utc_date")]
        public DateOnly UtcDate { get; set; }
        [JsonPropertyName("count")]
        public long Count { get; set; }
    }
}
