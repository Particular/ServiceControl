namespace ServiceControl.Audit.Persistence.SqlServer
{
    public class SqlDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public IPersistence Create(PersistenceSettings settings)
        {
            var connectionString = settings.PersisterSpecificSettings["Sql/ConnectionString"];

            return new SqlDbPersistence(connectionString);
        }
    }
}
