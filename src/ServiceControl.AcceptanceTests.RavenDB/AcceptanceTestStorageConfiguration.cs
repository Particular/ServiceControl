namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using Persistence.RavenDb;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public Task<IDictionary<string, string>> CustomizeSettings()
        {
            return Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>
            {
                { "RavenDB35/RunInMemory", bool.TrueString},
                { "DatabaseMaintenancePort", FindAvailablePort(33334).ToString()},
                { "HostName", "localhost" }
            });
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
