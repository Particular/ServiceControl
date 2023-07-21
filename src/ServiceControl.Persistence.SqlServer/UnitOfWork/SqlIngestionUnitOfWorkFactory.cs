namespace ServiceControl.Persistence.SqlServer
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.UnitOfWork;

    class SqlIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly string connectionString;

        public SqlIngestionUnitOfWorkFactory(SqlDbConnectionManager connectionManager)
            => connectionString = connectionManager.ConnectionString;

        public async ValueTask<IIngestionUnitOfWork> StartNew()
        {
            var connection = new SqlConnection(connectionString);

            await connection.OpenAsync();

            var transaction = connection.BeginTransaction();

            return new SqlIngestionUnitOfWork(connection, transaction);
        }

        public bool CanIngestMore() => true;
    }
}