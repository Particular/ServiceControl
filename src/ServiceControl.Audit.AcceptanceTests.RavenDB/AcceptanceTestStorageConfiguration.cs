namespace ServiceControl.Audit.AcceptanceTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.RavenDb;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public void CustomizeSettings(IDictionary<string, string> settings)
        {
            settings["RavenDb/RunInMemory"] = bool.TrueString;
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup() => Task.CompletedTask;
    }
}
