namespace ServiceControl.Monitoring
{
    using System.Threading.Tasks;
    using Contracts.Operations;

    interface IMonitoringDataStore
    {
        Task CreateIfNotExists(EndpointDetails endpoint);
        Task CreateOrUpdate(EndpointDetails endpoint);
        Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored);
        Task WarmupMonitoringFromPersistence();
    }
}