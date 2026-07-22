namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.UnitOfWork;

#pragma warning disable CS9113 // Parameter is unread.
public class EFMonitoringIngestionUnitOfWork(ServiceControlDbContext dbContext) : IMonitoringIngestionUnitOfWork
#pragma warning restore CS9113 // Parameter is unread.
{
    public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint) =>
        throw new NotImplementedException();
}
