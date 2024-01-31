﻿namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.RavenDB;
    using ServiceControl.Audit.Persistence.Tests;

    public class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; } = typeof(RavenPersistenceConfiguration).AssemblyQualifiedName;

        EmbeddedDatabase databaseInstance;
        string databaseName;

        public async Task<IDictionary<string, string>> CustomizeSettings()
        {
            databaseName = Guid.NewGuid().ToString();
            databaseInstance = await SharedEmbeddedServer.GetInstance();

            return new Dictionary<string, string>
            {
                { RavenPersistenceConfiguration.ConnectionStringKey,databaseInstance.ServerUrl },
                { RavenPersistenceConfiguration.DatabaseNameKey,databaseName}
            };
        }

        public async Task Cleanup() => await databaseInstance.DeleteDatabase(databaseName);
    }
}
