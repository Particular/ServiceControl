namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.RavenDb;
    using TestHelper;

    class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public Task<IDictionary<string, string>> CustomizeSettings()
        {
            return Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>
            {
                { "RavenDB35/RunInMemory", bool.TrueString},
                { "DatabaseMaintenancePort", PortUtility.FindAvailablePort(33334).ToString()},
                { "HostName", "localhost" }
            });
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
