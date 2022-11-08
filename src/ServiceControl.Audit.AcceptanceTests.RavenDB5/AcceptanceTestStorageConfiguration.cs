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

        public async Task<IDictionary<string, string>> CustomizeSettings()
        {
            var databaseName = Guid.NewGuid().ToString();

            var instance = await SharedEmbeddedServer.GetInstance();

            return new Dictionary<string, string>
            {
                { RavenDbPersistenceConfiguration.ConnectionStringKey,instance.ServerUrl },
                { RavenDbPersistenceConfiguration.DatabaseNameKey,databaseName}
            };
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
