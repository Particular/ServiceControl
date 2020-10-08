namespace ServiceControl.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Embedded;
    using SagaAudit;
    using ServiceBus.Management.Infrastructure.Settings;

    public class EmbeddedDatabase : IDisposable
    {
        readonly ServiceBus.Management.Infrastructure.Settings.Settings settings;
        IDocumentStore preparedDocumentStore;

        EmbeddedDatabase(ServiceBus.Management.Infrastructure.Settings.Settings settings)
        {
            this.settings = settings;
        }

        public static EmbeddedDatabase Start(ServiceBus.Management.Infrastructure.Settings.Settings settings, LoggingSettings loggingSettings)
        {
            var watch = new Stopwatch();
            watch.Start();
            var serverOptions = new ServerOptions
            {
                AcceptEula = true,
                DataDirectory = settings.DbPath,
                LogsPath = loggingSettings.LogPath,
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
            watch.Stop();
            Console.WriteLine($"EmbeddedDatabase::Start took {watch.ElapsedMilliseconds} ms");

            return new EmbeddedDatabase(settings);
        }

        public async Task<IDocumentStore> PrepareDatabase()
        {
            if (preparedDocumentStore != null)
            {
                return preparedDocumentStore;
            }

            var watch = new Stopwatch();
            watch.Start();
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

            watch.Stop();
            Console.WriteLine($"EmbeddedDatabase::PrepareDatabase took {watch.ElapsedMilliseconds} ms");

            preparedDocumentStore = documentStore;

            return documentStore;
        }

        public void Dispose()
        {
            preparedDocumentStore?.Dispose();
            EmbeddedServer.Instance.Dispose();

        }
    }
}