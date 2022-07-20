namespace ServiceControl.Persistence.SqlServer
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Operations;

    class SqlIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly string connectionString;

        public SqlIngestionUnitOfWorkFactory(string connectionString)
            => this.connectionString = connectionString;

        public async ValueTask<IIngestionUnitOfWork> StartNew()
        {
            var connection = new SqlConnection(connectionString);

            await connection.OpenAsync().ConfigureAwait(false);

            var transaction = connection.BeginTransaction();

            return new SqlIngestionUnitOfWork(connection, transaction);
        }
    }
}