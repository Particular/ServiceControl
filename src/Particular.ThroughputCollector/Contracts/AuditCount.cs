namespace Particular.ThroughputCollector.Contracts
{
    using Newtonsoft.Json;

    class AuditCount
    {
        [JsonProperty("utc_date")]
        public DateTime UtcDate { get; set; }
        [JsonProperty("count")]
        public long Count { get; set; }
    }
}
