namespace Particular.ThroughputCollector.Persistence;

using Contracts;

public interface IThroughputDataStore
{
    Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken);

    Task<Endpoint?> GetEndpoint(string endpointName, ThroughputSource throughputSource, CancellationToken cancellationToken) =>
        GetEndpoint(new EndpointIdentifier(endpointName, throughputSource), cancellationToken);

    Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default);

    Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IEnumerable<EndpointIdentifier> endpointIds, CancellationToken cancellationToken);

    Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken);

    Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IEnumerable<string> queueNames, CancellationToken cancellationToken);

    Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, DateOnly date, long messageCount, CancellationToken cancellationToken) =>
        RecordEndpointThroughput(endpointName, throughputSource, [new EndpointDailyThroughput(date, messageCount)], cancellationToken);

    Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IEnumerable<EndpointDailyThroughput> throughput, CancellationToken cancellationToken);

    Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator, CancellationToken cancellationToken);

    Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken);
    Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, CancellationToken cancellationToken);

    Task<EnvironmentData?> GetEnvironmentData(CancellationToken cancellationToken);

    Task SaveEnvironmentData(string? scopeType, Dictionary<string, string> data, CancellationToken cancellationToken);
}