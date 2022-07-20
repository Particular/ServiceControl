namespace ServiceControl.Persistence.SqlServer
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Operations;

    class SqlIngestionUnitOfWork : IIngestionUnitOfWork
    {
        readonly SqlConnection connection;
        readonly SqlTransaction transaction;

        public SqlIngestionUnitOfWork(SqlConnection connection, SqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
            Monitoring = new SqlMonitoringIngestionUnitOfWork(connection, transaction);
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

        public Task Complete()
        {
            transaction.Commit();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            transaction?.Dispose();
            connection?.Dispose();
        }
    }
}