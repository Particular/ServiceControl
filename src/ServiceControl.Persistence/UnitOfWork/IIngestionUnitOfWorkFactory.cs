namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IIngestionUnitOfWorkFactory
    {
        ValueTask<IIngestionUnitOfWork> StartNew(CancellationToken cancellationToken = default);
        bool CanIngestMore();
    }
}