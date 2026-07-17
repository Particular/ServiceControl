namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.UnitOfWork;

public class EFMonitoringIngestionUnitOfWork(ServiceControlDbContext dbContext) : IMonitoringIngestionUnitOfWork
{
    readonly ServiceControlDbContext dbContext = dbContext;

    public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
    {
        var entity = new KnownEndpointInsertOnlyEntity
        {
            KnownEndpointId = knownEndpoint.EndpointDetails.GetDeterministicId(),
            Name = knownEndpoint.EndpointDetails.Name,
            HostId = knownEndpoint.EndpointDetails.HostId,
            Host = knownEndpoint.EndpointDetails.Host
        };

        dbContext.KnownEndpointsInsertOnly.Add(entity);

        return Task.CompletedTask;
    }
}
