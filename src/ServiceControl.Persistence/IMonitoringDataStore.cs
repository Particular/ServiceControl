namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Operations;

    public interface IMonitoringDataStore
    {
        Task CreateIfNotExists(EndpointDetails endpoint);
        Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring);
        Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored);
        Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring);
        Task Delete(Guid endpointId);
        Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints();
    }
}