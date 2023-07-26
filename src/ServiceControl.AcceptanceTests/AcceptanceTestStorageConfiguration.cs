namespace ServiceControl.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.InMemory;

    class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public Task<IDictionary<string, string>> CustomizeSettings() => Task.FromResult<IDictionary<string, string>>(new Dictionary<string, string>());

        public Task Configure()
        {
            PersistenceType = typeof(InMemoryPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
