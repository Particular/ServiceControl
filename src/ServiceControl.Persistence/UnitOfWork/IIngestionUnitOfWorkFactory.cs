namespace ServiceControl.Persistence.UnitOfWork
{
    using System.Threading.Tasks;

    public interface IIngestionUnitOfWorkFactory
    {
        ValueTask<IIngestionUnitOfWork> StartNew();
        bool CanIngestMore();
    }
}