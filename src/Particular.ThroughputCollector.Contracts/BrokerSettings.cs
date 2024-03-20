namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

public class BrokerSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Broker Broker { get; set; }
    public List<BrokerSetting> Settings { get; set; } = [];
}

public class BrokerSetting
{
    public required string Name { get; set; }
    public required string Description { get; set; }
}
