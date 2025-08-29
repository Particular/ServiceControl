namespace ServiceControl.Audit.Persistence.PostgreSQL.UnitOfWork;

using System.Threading;
using System.Threading.Tasks;
using ServiceControl.Audit.Persistence.UnitOfWork;
using ServiceControl.Audit.Persistence.PostgreSQL;

class PostgreSQLAuditIngestionUnitOfWorkFactory(PostgreSQLConnectionFactory connectionFactory, DatabaseConfiguration databaseConfiguration) : IAuditIngestionUnitOfWorkFactory
{
    public async ValueTask<IAuditIngestionUnitOfWork> StartNew(int batchSize, CancellationToken cancellationToken)
    {
        var connection = await connectionFactory.OpenConnection(cancellationToken);
        return new PostgreSQLAuditIngestionUnitOfWork(connection, databaseConfiguration);
    }

    public bool CanIngestMore() => true;
}