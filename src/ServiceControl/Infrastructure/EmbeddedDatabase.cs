namespace ServiceControl.Infrastructure
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Embedded;
    using SagaAudit;
    using ServiceBus.Management.Infrastructure.Settings;

    static class EmbeddedDatabase
    {
        public static void Start(ServiceBus.Management.Infrastructure.Settings.Settings settings, LoggingSettings loggingSettings)
        {
            var serverOptions = new ServerOptions
            {
                AcceptEula = true,
                DataDirectory = settings.DbPath,
                LogsPath = loggingSettings.LogPath,
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
        }

        public static async Task<IDocumentStore> PrepareDatabase(ServiceBus.Management.Infrastructure.Settings.Settings settings)
        {
            var dbOptions = new DatabaseOptions("servicecontrol")
            {
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                }
            };

            var documentStore = await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions).ConfigureAwait(false);

            await IndexCreation.CreateIndexesAsync(typeof(EmbeddedDatabase).Assembly, documentStore).ConfigureAwait(false);
            await IndexCreation.CreateIndexesAsync(typeof(SagaDetailsIndex).Assembly, documentStore).ConfigureAwait(false);

            // TODO: Check to see if the configuration has changed.
            // If it has, then send an update to the server to change the expires metadata on all documents

            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = settings.ExpirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig))
                .ConfigureAwait(false);
            return documentStore;
        }
    }
}