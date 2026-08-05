namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.RavenDB;
    using ServiceControl.Audit.Persistence.Tests;
    using ServiceControl.RavenDB;

    public class AcceptanceTestStorageConfiguration
    {
        // Serializes database create/delete operations across parallel tests because the
        // embedded RavenDB server does not support concurrent database lifecycle operations.
        public static readonly SemaphoreSlim DatabaseLifecycleLock = new(1, 1);

        public string PersistenceType { get; } = "RavenDB";

        EmbeddedDatabase databaseInstance;
        string databaseName;

        public async Task<IDictionary<string, string>> CustomizeSettings()
        {
            databaseName = Guid.NewGuid().ToString("n");
            databaseInstance = await SharedEmbeddedServer.GetInstance();

            return new Dictionary<string, string>
            {
                { RavenPersistenceConfiguration.ConnectionStringKey,databaseInstance.ServerUrl },
                { RavenPersistenceConfiguration.DatabaseNameKey,databaseName}
            };
        }

        public async Task Cleanup()
        {
            if (databaseInstance == null)
            {
                return;
            }

            await DatabaseLifecycleLock.WaitAsync();
            try
            {
                await databaseInstance.DeleteDatabase(databaseName);
            }
            finally
            {
                DatabaseLifecycleLock.Release();
            }
        }
    }
}
