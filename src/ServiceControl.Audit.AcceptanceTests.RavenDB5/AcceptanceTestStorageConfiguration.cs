namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.RavenDb;
    using ServiceControl.Audit.Persistence.Tests;

    partial class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; protected set; }

        public async Task CustomizeSettings(IDictionary<string, string> settings)
        {
            var databaseName = Guid.NewGuid().ToString();

            var instance = await SharedEmbeddedServer.GetInstance();

            settings["ServiceControl/Audit/RavenDb5/ConnectionString"] = instance.ServerUrl;
            settings["ServiceControl/Audit/RavenDb5/DatabaseName"] = databaseName;
        }

        public Task Configure()
        {
            PersistenceType = typeof(RavenDbPersistenceConfiguration).AssemblyQualifiedName;

            return Task.CompletedTask;
        }

        public Task Cleanup()
        {
            return Task.CompletedTask;
        }
    }
}
