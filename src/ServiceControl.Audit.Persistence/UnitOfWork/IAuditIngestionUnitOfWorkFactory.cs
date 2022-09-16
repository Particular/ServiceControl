namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    public interface IAuditIngestionUnitOfWorkFactory
    {
        IAuditIngestionUnitOfWork StartNew(int batchSize);
    }
}