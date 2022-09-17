namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.InMemory;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public Task Configure()
        {
            PersistenceType = typeof(InMemoryPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
