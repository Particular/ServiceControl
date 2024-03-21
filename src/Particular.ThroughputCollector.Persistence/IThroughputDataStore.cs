namespace Particular.ThroughputCollector.Persistence;

using Contracts;

public interface IThroughputDataStore
{
    Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints = true, CancellationToken cancellationToken = default);

    Task<Endpoint?> GetEndpointByName(string name, ThroughputSource throughputSource);

    Task RecordEndpointThroughput(Endpoint endpoint);

    Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator);

    Task AppendEndpointThroughput(Endpoint endpoint);

    Task<bool> IsThereThroughputForLastXDays(int days);

    Task<BrokerData?> GetBrokerData(Broker broker);

    Task SaveBrokerData(Broker broker, string? scopeType, Dictionary<string, string> data);
}