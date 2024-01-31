namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.InMemory;

    public class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; } = typeof(InMemoryPersistenceConfiguration).AssemblyQualifiedName;

        public Task<IDictionary<string, string>> CustomizeSettings() => Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());

        public Task Cleanup() => Task.CompletedTask;
    }
}
