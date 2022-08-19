namespace ServiceControl.Audit.Infrastructure.Migration
{
    using ServiceControl.Audit.Persistence;
    using System.Threading.Tasks;

    class MigrateKnownEndpoints : IDataMigration
    {
        IAuditDataStore dataStore;
        public MigrateKnownEndpoints(IAuditDataStore dataStore)
        {
            this.dataStore = dataStore;
        }

        public Task Migrate(int pageSize = 1024) => dataStore.MigrateEndpoints(pageSize);
    }
}