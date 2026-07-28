using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;

class LicensingDataStore : ILicensingDataStore
{
    public Task<IEnumerable<Particular.LicensingComponent.Contracts.Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Particular.LicensingComponent.Contracts.Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IEnumerable<(EndpointIdentifier Id, Particular.LicensingComponent.Contracts.Endpoint? Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<LicensedEndpointDetails?> GetLicensedEndpointDetails(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<List<string>> GetReportMasks(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveEndpoint(Particular.LicensingComponent.Contracts.Endpoint endpoint, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveLicensedEndpointDetails(LicensedEndpointDetails result, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken) => throw new NotImplementedException();
}