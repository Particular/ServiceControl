namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using ServiceControl.Persistence.UnitOfWork;

public class EFIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
{
    public ValueTask<IIngestionUnitOfWork> StartNew() =>
        throw new NotImplementedException();

    public bool CanIngestMore() =>
        throw new NotImplementedException();
}
