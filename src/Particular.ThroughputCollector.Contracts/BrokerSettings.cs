namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

public class BrokerSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Broker Broker { get; set; }
    public List<BrokerSetting> Settings { get; set; } = [];
}

public class BrokerSetting(string name, string description)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
}
