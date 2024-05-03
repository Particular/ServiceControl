namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    using System.Threading.Tasks;

    public interface IAuditIngestionUnitOfWorkFactory
    {
        ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize); //Throws if not enough space or some other problem preventing from writing data
        bool CanIngestMore();
    }
}