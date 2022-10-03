namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Client.Documents.Operations.Indexes;
    using Raven.Client.Exceptions;
    using Raven.Client.Exceptions.Database;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;
    using Raven.Client.ServerWide.Operations.DocumentsCompression;
    using Raven.Embedded;
    using ServiceControl.Audit.Persistence.RavenDb.Indexes;
    using ServiceControl.Audit.Persistence.RavenDb5;
    using ServiceControl.SagaAudit;

    public class EmbeddedDatabase : IDisposable
    {
        public EmbeddedDatabase(int expirationProcessTimerInSeconds, string databaseUrl, bool useEmbeddedInstance, bool enableFullTextSearch, AuditDatabaseConfiguration configuration)
        {
            this.expirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
            this.databaseUrl = databaseUrl;
            this.useEmbeddedInstance = useEmbeddedInstance;
            this.enableFullTextSearch = enableFullTextSearch;
            this.configuration = configuration;
        }

        public static EmbeddedDatabase Start(string dbPath, int expirationProcessTimerInSecond, string databaseMaintenanceUrl, bool enableFullTextSearch, AuditDatabaseConfiguration auditDatabaseConfiguration)
        {
            var commandLineArgs = new List<string>();
            var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RavenLicense.json");
            if (File.Exists(localRavenLicense))
            {
                logger.InfoFormat("Loading RavenDB license found from {0}", localRavenLicense);
                commandLineArgs.Add($"--License.Path={localRavenLicense}");
            }
            else
            {
                logger.InfoFormat("Loading Embedded RavenDB license");
                var license = ReadLicense();
                commandLineArgs.Add($"--License=\"{license}\"");
            }

            commandLineArgs.Add($"--Server.MaxTimeForTaskToWaitForDatabaseToLoadInSec={(int)TimeSpan.FromDays(1).TotalSeconds}");
            var serverOptions = new ServerOptions
            {
                CommandLineArgs = commandLineArgs,
                AcceptEula = true,
                DataDirectory = dbPath,
                ServerUrl = databaseMaintenanceUrl,
                MaxServerStartupTimeDuration = TimeSpan.FromDays(1) //TODO: RAVEN5 allow command line override?
            };

            EmbeddedServer.Instance.StartServer(serverOptions);

            return new EmbeddedDatabase(expirationProcessTimerInSecond, databaseMaintenanceUrl, true, enableFullTextSearch, auditDatabaseConfiguration);
        }

        public static string ReadLicense()
        {
            using (var resourceStream = typeof(EmbeddedDatabase).Assembly.GetManifestResourceStream("ServiceControl.Audit.Persistence.RavenDb5.RavenLicense.json"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd()
                    .Replace(" ", "")
                    .Replace(Environment.NewLine, "")
                    .Replace("\"", "'"); //Remove line breaks to pass value via command line argument
            }
        }

        public async Task<IDocumentStore> Initialize(CancellationToken cancellationToken)
        {
            if (useEmbeddedInstance)
            {
                var dbOptions = new DatabaseOptions(configuration.Name)
                {
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                if (configuration.FindClrType != null)
                {
                    dbOptions.Conventions.FindClrType += configuration.FindClrType;
                }

                if (configuration.EnableDocumentCompression)
                {
                    dbOptions.DatabaseRecord.DocumentsCompression = new DocumentsCompressionConfiguration(
                        false,
                        configuration.CollectionsToCompress.ToArray()
                    );
                }

                documentStore =
                    await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var store = new DocumentStore
                {
                    Database = configuration.Name,
                    Urls = new[] { databaseUrl },
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                if (configuration.FindClrType != null)
                {
                    store.Conventions.FindClrType += configuration.FindClrType;
                }

                store.Initialize();

                documentStore = store;
            }

            return documentStore;
        }

        public async Task Setup(CancellationToken cancellationToken)
        {
            try
            {
                await documentStore.Maintenance.ForDatabase(configuration.Name).SendAsync(new GetStatisticsOperation(), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DatabaseDoesNotExistException)
            {
                try
                {
                    await documentStore.Maintenance.Server
                        .SendAsync(new CreateDatabaseOperation(new DatabaseRecord(configuration.Name)), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (ConcurrencyException)
                {
                    // The database was already created before calling CreateDatabaseOperation
                }

            }

            if (configuration.EnableDocumentCompression)
            {
                await documentStore.Maintenance.ForDatabase(configuration.Name).SendAsync(
                    new UpdateDocumentsCompressionConfigurationOperation(new DocumentsCompressionConfiguration(
                        false,
                        configuration.CollectionsToCompress.ToArray()
                    )), cancellationToken).ConfigureAwait(false);
            }

            var indexList =
                  new List<AbstractIndexCreationTask> { new FailedAuditImportIndex(), new SagaDetailsIndex() };

            if (enableFullTextSearch)
            {

                indexList.Add(new MessagesViewIndexWithFullTextSearch());
                await documentStore.Maintenance.SendAsync(new DeleteIndexOperation("MessagesViewIndex"), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                indexList.Add(new MessagesViewIndex());
                await documentStore.Maintenance
                    .SendAsync(new DeleteIndexOperation("MessagesViewIndexWithFullTextSearch"), cancellationToken)
                    .ConfigureAwait(false);
            }

            await IndexCreation.CreateIndexesAsync(indexList, documentStore, null, null, cancellationToken).ConfigureAwait(false);

            // TODO: Check to see if the configuration has changed.
            // If it has, then send an update to the server to change the expires metadata on all documents
            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = expirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken)
                .ConfigureAwait(false);
        }

        public void Dispose()
        {
            documentStore?.Dispose();

            if (useEmbeddedInstance)
            {
                EmbeddedServer.Instance?.Dispose();
            }
        }

        IDocumentStore documentStore;

        readonly int expirationProcessTimerInSeconds;
        readonly string databaseUrl;
        readonly bool useEmbeddedInstance;
        readonly bool enableFullTextSearch;
        readonly AuditDatabaseConfiguration configuration;

        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}