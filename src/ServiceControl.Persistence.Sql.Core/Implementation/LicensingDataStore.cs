namespace ServiceControl.Persistence.Sql.Core.Implementation;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Particular.LicensingComponent.Contracts;
using Particular.LicensingComponent.Persistence;

public class LicensingDataStore : ILicensingDataStore
{
    public Task<IEnumerable<Endpoint>> GetAllEndpoints(bool includePlatformEndpoints, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<AuditServiceMetadata> GetAuditServiceMetadata(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<BrokerMetadata> GetBrokerMetadata(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Endpoint?> GetEndpoint(EndpointIdentifier id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IEnumerable<(EndpointIdentifier Id, Endpoint? Endpoint)>> GetEndpoints(IList<EndpointIdentifier> endpointIds, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<IDictionary<string, IEnumerable<ThroughputData>>> GetEndpointThroughputByQueueName(IList<string> queueNames, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<List<string>> GetReportMasks(CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDays(int days, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> IsThereThroughputForLastXDaysForSource(int days, ThroughputSource throughputSource, bool includeToday, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task RecordEndpointThroughput(string endpointName, ThroughputSource throughputSource, IList<EndpointDailyThroughput> throughput, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveAuditServiceMetadata(AuditServiceMetadata auditServiceMetadata, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveBrokerMetadata(BrokerMetadata brokerMetadata, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveEndpoint(Endpoint endpoint, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task SaveReportMasks(List<string> reportMasks, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task UpdateUserIndicatorOnEndpoints(List<UpdateUserIndicator> userIndicatorUpdates, CancellationToken cancellationToken) => throw new NotImplementedException();
}
