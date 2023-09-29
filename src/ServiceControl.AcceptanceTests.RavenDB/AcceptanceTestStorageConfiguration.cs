namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Persistence.RavenDb;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTesting;

    class AcceptanceTestStorageConfiguration
    {
        static readonly PortPool portPool = new PortPool(33334);
        readonly PortLease portLease = portPool.GetLease();

        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(Settings settings)
        {
            settings.Port = portLease.GetPort();

            settings.PersisterSpecificSettings = new RavenDBPersisterSettings
            {
                RunInMemory = true,
                DatabaseMaintenancePort = portLease.GetPort(),
                DatabasePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                ErrorRetentionPeriod = TimeSpan.FromDays(10),
            };
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            portLease.Dispose();
            return Task.CompletedTask;
        }
    }
}
