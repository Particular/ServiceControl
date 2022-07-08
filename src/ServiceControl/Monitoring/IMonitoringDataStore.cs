namespace ServiceControl.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Contracts.Operations;

    interface IMonitoringDataStore
    {
        Task CreateIfNotExists(EndpointDetails endpoint);
        Task CreateOrUpdate(EndpointDetails endpoint, EndpointInstanceMonitoring endpointInstanceMonitoring);
        Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored);
        Task WarmupMonitoringFromPersistence(EndpointInstanceMonitoring endpointInstanceMonitoring);
        Task Delete(Guid endpointId);
        Task BulkCreate(EndpointDetails[] newEndpoints);
    }
}