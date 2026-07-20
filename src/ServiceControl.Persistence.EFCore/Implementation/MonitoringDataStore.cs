namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.Operations;

public class MonitoringDataStore : IMonitoringDataStore
{
    public Task CreateIfNotExists(EndpointDetails endpoint) =>
        throw new NotImplementedException();

    public Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring) =>
        throw new NotImplementedException();

    public Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored) =>
        throw new NotImplementedException();

    public Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring) =>
        throw new NotImplementedException();

    public Task Delete(Guid endpointId) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints() =>
        throw new NotImplementedException();
}
