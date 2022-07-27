namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Contracts.Operations;
    using ServiceControl.Monitoring;

    interface IMonitoringDataStore
    {
        Task CreateIfNotExists(EndpointDetails endpoint);
        Task CreateOrUpdate(EndpointDetails endpoint, EndpointInstanceMonitoring endpointInstanceMonitoring);
        Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored);
        Task WarmupMonitoringFromPersistence(EndpointInstanceMonitoring endpointInstanceMonitoring);
        Task Delete(Guid endpointId);
        Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints();
    }
}