namespace Particular.ThroughputCollector.Broker;

public interface IBrokerInfo
{
    Dictionary<string, string> Data { get; }
    string MessageTransport { get; }
}