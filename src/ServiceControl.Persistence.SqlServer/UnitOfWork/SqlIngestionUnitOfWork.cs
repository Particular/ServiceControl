namespace ServiceControl.Persistence.SqlServer
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Operations;

    class SqlIngestionUnitOfWork : IngestionUnitOfWorkBase
    {
        readonly SqlConnection connection;
        readonly SqlTransaction transaction;

        public SqlIngestionUnitOfWork(SqlConnection connection, SqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
            Monitoring = new SqlMonitoringIngestionUnitOfWork(connection, transaction);
        }

        public override Task Complete()
        {
            transaction.Commit();
            return Task.CompletedTask;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                transaction?.Dispose();
                connection?.Dispose();
            }
        }
    }
}