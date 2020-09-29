namespace ServiceControl.Audit.Infrastructure
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Embedded;
    using ServiceControl.SagaAudit;
    using Settings;

    static class EmbeddedDatabase
    {
        public static void Start(Settings.Settings settings, LoggingSettings loggingSettings)
        {
            var serverOptions = new ServerOptions
            {
                AcceptEula = true,
                DataDirectory = settings.DbPath,
                LogsPath = loggingSettings.LogPath
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
        }

        public static async Task<IDocumentStore> PrepareAuditDatabase()
        {
            var documentStore = await EmbeddedServer.Instance.GetDocumentStoreAsync("audit").ConfigureAwait(false);
            await IndexCreation.CreateIndexesAsync(typeof(EmbeddedDatabase).Assembly, documentStore).ConfigureAwait(false);
            await IndexCreation.CreateIndexesAsync(typeof(SagaDetailsIndex).Assembly, documentStore).ConfigureAwait(false);
            return documentStore;
        }
    }
}