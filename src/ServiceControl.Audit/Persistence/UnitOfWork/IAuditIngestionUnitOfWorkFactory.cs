namespace ServiceControl.Audit.Persistence.UnitOfWork
{
    interface IAuditIngestionUnitOfWorkFactory
    {
        IAuditIngestionUnitOfWork StartNew(int batchSize);
    }
}