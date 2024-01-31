namespace ServiceControl.AcceptanceTests
{
    using System.Threading.Tasks;
    using Persistence.RavenDB;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting;

    // TODO move this class into the same Shared folder where the component runner is
    public class AcceptanceTestStorageConfiguration
    {
        static readonly PortPool portPool = new PortPool(33334);

        readonly DatabaseLease databaseLease = SharedDatabaseSetup.LeaseDatabase();
        readonly PortLease portLease = portPool.GetLease();

        public string PersistenceType { get; } = typeof(RavenPersistenceConfiguration).AssemblyQualifiedName;

        public void CustomizeSettings(Settings settings)
        {
            databaseLease.CustomizeSettings(settings);
            settings.Port = portLease.GetPort();
        }

        public async ValueTask Cleanup()
        {
            portLease.Dispose();
            await databaseLease.DisposeAsync();
        }
    }
}
