namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Persistence.RavenDb;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestHelper;

    class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(Settings settings)
        {
            settings.PersisterSpecificSettings = new RavenDBPersisterSettings
            {
                RunInMemory = true,
                DatabaseMaintenancePort = PortUtility.FindAvailablePort(settings.Port + 1),
                DatabasePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                ErrorRetentionPeriod = TimeSpan.FromDays(10),
            };
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
