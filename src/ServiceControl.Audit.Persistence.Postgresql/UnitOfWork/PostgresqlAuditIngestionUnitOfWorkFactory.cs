namespace ServiceControl.Audit.Persistence.PostgreSQL.UnitOfWork
{
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using ServiceControl.Audit.Persistence.UnitOfWork;
    using ServiceControl.Audit.Persistence.PostgreSQL;

    public class PostgreSQLAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
    {
        readonly PostgreSQLConnectionFactory connectionFactory;

        public PostgreSQLAuditIngestionUnitOfWorkFactory(PostgreSQLConnectionFactory connectionFactory)
        {
            this.connectionFactory = connectionFactory;
        }

        public async ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
        {
            var connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            return new PostgreSQLAuditIngestionUnitOfWork(connection, transaction);
        }

        public bool CanIngestMore() => true; // TODO: Implement logic based on storage state
    }
}
