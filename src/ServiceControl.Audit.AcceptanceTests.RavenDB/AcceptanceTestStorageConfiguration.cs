namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.RavenDb;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public Task CustomizeSettings(IDictionary<string, string> settings)
        {
            settings["ServiceControl/Audit/RavenDb35/RunInMemory"] = bool.TrueString;
            settings["ServiceControl.Audit/DatabaseMaintenancePort"] = FindAvailablePort(33334).ToString();
            settings["ServiceControl.Audit/HostName"] = "localhost";

            return Task.CompletedTask;
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
