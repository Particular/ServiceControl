namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    public interface IMonitoringDataStore
    {
        Task CreateIfNotExists(EndpointDetails endpoint, CancellationToken cancellationToken = default);
        Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring, CancellationToken cancellationToken = default);
        Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored, CancellationToken cancellationToken = default);
        Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring, CancellationToken cancellationToken = default);
        Task Delete(Guid endpointId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints(CancellationToken cancellationToken = default);
    }
}