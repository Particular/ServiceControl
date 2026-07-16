namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.Persistence.UnitOfWork;

public class EFIngestionUnitOfWork : IIngestionUnitOfWork
{
    public IMonitoringIngestionUnitOfWork Monitoring =>
        throw new NotImplementedException();

    public IRecoverabilityIngestionUnitOfWork Recoverability =>
        throw new NotImplementedException();

    public Task Complete(CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public void Dispose()
    {
        // Nothing to dispose yet
        GC.SuppressFinalize(this);
    }
}
