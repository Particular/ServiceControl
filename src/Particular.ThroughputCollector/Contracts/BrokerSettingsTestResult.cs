namespace Particular.ThroughputCollector.Contracts
{
    using System.Text.Json.Serialization;

    public class BrokerSettingsTestResult
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Broker Broker { get; set; }
        public bool ConnectionSuccessful { get; set; }
        public List<string> ConnectionErrorMessages { get; set; } = [];
    }
}
