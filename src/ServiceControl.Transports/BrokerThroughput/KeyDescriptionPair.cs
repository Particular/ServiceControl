#nullable enable
namespace ServiceControl.Transports.BrokerThroughput;

public readonly struct KeyDescriptionPair(string key, string description)
{
    public string Key { get; } = key;
    public string Description { get; } = description;
}