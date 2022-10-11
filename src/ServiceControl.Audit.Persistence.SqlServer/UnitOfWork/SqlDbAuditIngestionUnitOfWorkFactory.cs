namespace ServiceControl.Audit.Persistence.SqlServer.UnitOfWork
{
    using Persistence.UnitOfWork;

    class SqlDbAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public IAuditIngestionUnitOfWork StartNew(int batchSize) => new SqlDbAuditIngestionUnitOfWork();
    }
}