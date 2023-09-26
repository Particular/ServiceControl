namespace ServiceControl.AcceptanceTests
{
    using System.Threading.Tasks;
    using Persistence.RavenDb;
    using ServiceBus.Management.Infrastructure.Settings;

    class AcceptanceTestStorageConfiguration
    {
        readonly DatabaseLease databaseLease = SharedDatabaseSetup.LeaseDatabase();

        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(Settings settings)
        {
            databaseLease.CustomizeSettings(settings);
            settings.Port = databaseLease.LeasePort();
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public ValueTask Cleanup() => databaseLease.DisposeAsync();
    }
}
