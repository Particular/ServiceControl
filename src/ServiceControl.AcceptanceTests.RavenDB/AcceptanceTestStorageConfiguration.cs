namespace ServiceControl.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using Persistence.Tests;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.RavenDB;

    public class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; } = "RavenDB";

        public async Task CustomizeSettings(Settings settings)
        {
            databaseName = Guid.NewGuid().ToString("n");
            databaseInstance = await SharedEmbeddedServer.GetInstance();

            settings.PersisterSpecificSettings = new RavenPersisterSettings
            {
                ErrorRetentionPeriod = TimeSpan.FromDays(10),
                ConnectionString = databaseInstance.ServerUrl,
                DatabaseName = databaseName
            };
        }

        public async Task Cleanup()
        {
            if (databaseInstance == null)
            {
                return;
            }
            await databaseInstance.DeleteDatabase(databaseName);
        }

        EmbeddedDatabase databaseInstance;
        string databaseName;
    }
}