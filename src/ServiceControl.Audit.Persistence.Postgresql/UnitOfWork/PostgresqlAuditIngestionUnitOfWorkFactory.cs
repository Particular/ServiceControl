namespace ServiceControl.Audit.Persistence.Postgresql.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.UnitOfWork;

    public class PostgresqlAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        public ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
        {
            // TODO: Implement logic to start a new unit of work for PostgreSQL
            throw new System.NotImplementedException();
        }

        public bool CanIngestMore() => true; // TODO: Implement logic based on storage state
    }
}
