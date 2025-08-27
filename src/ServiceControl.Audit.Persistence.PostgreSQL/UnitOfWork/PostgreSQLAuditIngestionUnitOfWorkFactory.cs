namespace ServiceControl.Audit.Persistence.PostgreSQL.UnitOfWork;

using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Audit.Persistence.UnitOfWork;
using ServiceControl.Audit.Persistence.PostgreSQL;
class PostgreSQLAuditIngestionUnitOfWorkFactory : IAuditIngestionUnitOfWorkFactory
{
    readonly PostgreSQLConnectionFactory connectionFactory;

    public PostgreSQLAuditIngestionUnitOfWorkFactory(PostgreSQLConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
    {
        var connection = await connectionFactory.OpenConnection(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        return new PostgreSQLAuditIngestionUnitOfWork(connection, transaction);
    }

    public bool CanIngestMore() => true;
}