namespace ServiceControl.Operations
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class SqlIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly string connectionString;

        public SqlIngestionUnitOfWorkFactory(string connectionString)
            => this.connectionString = connectionString;

        public async Task<IIngestionUnitOfWork> StartNew()
        {
            var connection = new SqlConnection(connectionString);

            await connection.OpenAsync().ConfigureAwait(false);

            return new SqlIngestionUnitOfWork(connection);
        }
    }
}