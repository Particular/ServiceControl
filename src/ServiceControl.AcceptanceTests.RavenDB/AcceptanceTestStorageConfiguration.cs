namespace ServiceControl.AcceptanceTests
{
    using System.Threading.Tasks;
    using Persistence.RavenDb;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting;

    class AcceptanceTestStorageConfiguration
    {
        static readonly PortPool portPool = new PortPool(33334);

        readonly DatabaseLease databaseLease = SharedDatabaseSetup.LeaseDatabase();
        readonly PortLease portLease = portPool.GetLease();

        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(Settings settings)
        {
            databaseLease.CustomizeSettings(settings);
            settings.Port = portLease.GetPort();
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public async ValueTask Cleanup()
        {
            portLease.Dispose();
            await databaseLease.DisposeAsync();
        }
    }
}
