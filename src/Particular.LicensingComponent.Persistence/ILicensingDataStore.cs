namespace Particular.LicensingComponent.Persistence;

using Contracts;

public interface ILicensingDataStore
{
    Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken = default);

    Task<Endpoint?> GetEndpoint(string endpointName, ThroughputSource throughputSource, CancellationToken cancellationToken = default) =>
        GetEndpoint(new EndpointIdentifier(endpointName, throughputSource), cancellationToken);

    Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default);

    Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken = default);

    Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken = default);

    Task RemoveEndpoints(EndpointIdentifier[] endpointIds, CancellationToken cancellationToken = default);

    Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken = default);

    Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, DateOnly date, long messageCount, CancellationToken cancellationToken = default) =>
        RecordEndpointThroughput(endpointName, throughputSource, [new EndpointDailyThroughput(date, messageCount)], cancellationToken);

    Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken = default);

    Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken = default);

    Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken = default);
    Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken = default);

    Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken = default);

    Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken = default);

    Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default);

    Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken = default);
    Task<List<string>> GetReportMasks(CancellationToken cancellationToken = default);
    Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken = default);

    Task<LicensedEndpointDetails?> GetLicensedEndpointDetails(CancellationToken cancellationToken = default);
    Task SaveLicensedEndpointDetails(LicensedEndpointDetails result, CancellationToken cancellationToken = default);
}