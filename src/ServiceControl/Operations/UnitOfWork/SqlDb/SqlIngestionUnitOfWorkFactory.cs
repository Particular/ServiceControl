namespace ServiceControl.Operations
{
    class SqlIngestionUnitOfWorkFactory : IIngestionUnitOfWorkFactory
    {
        readonly string connectionString;

        public SqlIngestionUnitOfWorkFactory(string connectionString)
            => this.connectionString = connectionString;

        public IIngestionUnitOfWork StartNew()
            => new SqlIngestionUnitOfWork(connectionString);
    }
}