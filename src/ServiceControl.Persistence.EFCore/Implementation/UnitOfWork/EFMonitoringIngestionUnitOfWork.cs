namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.Persistence.UnitOfWork;

public class EFMonitoringIngestionUnitOfWork : IMonitoringIngestionUnitOfWork
{
    public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint) =>
        throw new NotImplementedException();
}
