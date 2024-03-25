namespace Particular.ThroughputCollector.Contracts;

using System.Text.Json.Serialization;

public class ThroughputConnectionSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Broker Broker { get; set; }
    public List<ThroughputConnectionSetting> Settings { get; set; } = [];
}

public class ThroughputConnectionSetting(string name, string description)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
}
