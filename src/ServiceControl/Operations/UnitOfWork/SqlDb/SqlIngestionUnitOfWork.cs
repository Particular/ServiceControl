namespace ServiceControl.Operations
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class SqlIngestionUnitOfWork : IIngestionUnitOfWork
    {
        readonly SqlConnection connection;

        public SqlIngestionUnitOfWork(SqlConnection connection)
        {
            this.connection = connection;
            Monitoring = new SqlMonitoringIngestionUnitOfWork(connection);
        }

        public IMonitoringIngestionUnitOfWork Monitoring { get; }
        public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

        public Task Complete() => Task.CompletedTask;

        public void Dispose() => connection?.Dispose();
    }
}