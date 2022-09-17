namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.RavenDb;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
