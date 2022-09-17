namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.InMemory;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

#pragma warning disable IDE0060 // Remove unused parameter
        public void CustomizeSettings(IDictionary<string, string> settings)
#pragma warning restore IDE0060 // Remove unused parameter
        {
        }

        public Task Configure()
        {
            PersistenceType = typeof(InMemoryPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
