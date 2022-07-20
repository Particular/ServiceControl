namespace ServiceControl.Persistence.SqlServer
{
    class SqlDbConnectionManager
    {
        public string ConnectionString { get; }

        public SqlDbConnectionManager(string connectionString) => ConnectionString = connectionString;
    }
}