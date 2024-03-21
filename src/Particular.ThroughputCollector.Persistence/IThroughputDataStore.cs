namespace Particular.ThroughputCollector.Persistence;

using Contracts;

public interface IThroughputDataStore
{
    Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints = true, CancellationToken cancellationToken = default);

    Task<Endpoint?> GetEndpoint(string endpointName, ThroughputSource throughputSource, CancellationToken cancellationToken = default) =>
        GetEndpoint(new EndpointIdentifier(endpointName, throughputSource), cancellationToken);

    Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default);

    Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default);

    Task RecordEndpointThroughput(EndpointIdentifier id, IEnumerable<EndpointThroughput> throughput, CancellationToken cancellationToken = default);

    Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator);

    Task AppendEndpointThroughput(Endpoint endpoint);

    Task<bool> IsThereThroughputForLastXDays(int days);

    Task<BrokerData?> GetBrokerData(Broker broker);

    Task SaveBrokerData(Broker broker, string? scopeType, Dictionary<string, string> data);
}