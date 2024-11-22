namespace Particular.LicensingComponent.Contracts;

public class ThroughputConnectionSettings
{
    public List<ThroughputConnectionSetting> ServiceControlSettings { get; set; } = [];
    public List<ThroughputConnectionSetting> MonitoringSettings { get; set; } = [];
    public List<ThroughputConnectionSetting> BrokerSettings { get; set; } = [];
}

public class ThroughputConnectionSetting(string name, string description)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
}