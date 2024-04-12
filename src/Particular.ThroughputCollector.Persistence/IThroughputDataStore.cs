namespace Particular.ThroughputCollector.Persistence;

using Contracts;

public interface IThroughputDataStore
{
    Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints = true, CancellationToken cancellationToken = default);

    Task<Endpoint?> GetEndpoint(string endpointName, ThroughputSource throughputSource, CancellationToken cancellationToken = default) =>
        GetEndpoint(new EndpointIdentifier(endpointName, throughputSource), cancellationToken);

    Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default);

    Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IEnumerable<EndpointIdentifier> endpointIds, CancellationToken cancellationToken = default);

    Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default);

    Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IEnumerable<string> queueNames, CancellationToken cancellationToken = default);

    Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, DateOnly date, long messageCount, CancellationToken cancellationToken = default) =>
        RecordEndpointThroughput(endpointName, throughputSource, [new EndpointDailyThroughput(date, messageCount)], cancellationToken);

    Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IEnumerable<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default);

    Task UpdateUserIndicatorOnEndpoints(List<Endpoint> endpointsWithUserIndicator);

    Task<bool> IsThereThroughputForLastXDays(int days);
    Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource);

    Task<EnvironmentData?> GetEnvironmentData();

    Task SaveEnvironmentData(string? scopeType, Dictionary<string, string> data);
}