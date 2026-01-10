namespace ServiceControl.Persistence.Sql;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.Operations;
using ServiceControl.Persistence;

class NoOpMonitoringDataStore : IMonitoringDataStore
{
    public Task CreateIfNotExists(EndpointDetails endpoint) => Task.CompletedTask;

    public Task CreateOrUpdate(EndpointDetails endpoint, IEndpointInstanceMonitoring endpointInstanceMonitoring) =>
        Task.CompletedTask;

    public Task UpdateEndpointMonitoring(EndpointDetails endpoint, bool isMonitored) => Task.CompletedTask;

    public Task WarmupMonitoringFromPersistence(IEndpointInstanceMonitoring endpointInstanceMonitoring) =>
        Task.CompletedTask;

    public Task Delete(Guid endpointId) => Task.CompletedTask;

    public Task<IReadOnlyList<KnownEndpoint>> GetAllKnownEndpoints() =>
        Task.FromResult<IReadOnlyList<KnownEndpoint>>([]);
}
