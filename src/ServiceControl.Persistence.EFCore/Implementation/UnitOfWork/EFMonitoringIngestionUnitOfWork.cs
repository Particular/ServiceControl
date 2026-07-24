namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.Persistence.UnitOfWork;

public class EFMonitoringIngestionUnitOfWork(EFIngestionUnitOfWork parentUnitOfWork) : IMonitoringIngestionUnitOfWork
{
    public Task RecordKnownEndpoint(KnownEndpoint knownEndpoint)
    {
        parentUnitOfWork.Record(knownEndpoint);

        return Task.CompletedTask;
    }
}
