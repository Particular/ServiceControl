namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using Persistence.RavenDb;
    using ServiceBus.Management.Infrastructure.Settings;

    class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(Settings settings)
        {
            settings.PersisterSpecificSettings = new RavenDBPersisterSettings
            {
                DatabaseMaintenancePort = FindAvailablePort(settings.Port + 1),
                DatabasePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                ErrorRetentionPeriod = TimeSpan.FromDays(10),
            };
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
