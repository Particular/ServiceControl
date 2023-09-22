namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using Persistence.RavenDb;
    using Raven.Client.ServerWide.Operations;
    using ServiceBus.Management.Infrastructure.Settings;

    class AcceptanceTestStorageConfiguration
    {
        readonly string databaseName = Guid.NewGuid().ToString("n");

        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(Settings settings)
        {
            settings.PersisterSpecificSettings = new RavenDBPersisterSettings
            {
                ErrorRetentionPeriod = TimeSpan.FromDays(10),
                ConnectionString = SharedDatabaseSetup.SharedInstance.ServerUrl,
                DatabaseName = databaseName
            };
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public async Task Cleanup()
        {
            var documentStore = await SharedDatabaseSetup.SharedInstance.Connect();

            // Comment this out temporarily to be able to inspect a database after the test has completed
            var deleteDatabasesOperation = new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters { DatabaseNames = new[] { databaseName }, HardDelete = true });
            await documentStore.Maintenance.Server.SendAsync(deleteDatabasesOperation);
        }
    }
}
