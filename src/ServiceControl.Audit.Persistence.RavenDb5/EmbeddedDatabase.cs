namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Embedded;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;
    using System.IO;
    using NServiceBus.Logging;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Exceptions;
    using Raven.Client.Exceptions.Database;
    using Raven.Client.ServerWide.Operations.DocumentsCompression;
    using ServiceControl.Audit.Persistence.RavenDb5;

    public class EmbeddedDatabase : IDisposable
    {
        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();

        readonly int expirationProcessTimerInSeconds;
        readonly string databaseUrl;
        readonly bool useEmbeddedInstance;
        readonly Dictionary<string, IDocumentStore> preparedDocumentStores = new Dictionary<string, IDocumentStore>();

        public EmbeddedDatabase(int expirationProcessTimerInSeconds, string databaseUrl, bool useEmbeddedInstance)
        {
            this.expirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
            this.databaseUrl = databaseUrl;
            this.useEmbeddedInstance = useEmbeddedInstance;
        }


        public static EmbeddedDatabase Start(string dbPath, int expirationProcessTimerInSecond, string databaseUrl)
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
                ServerUrl = databaseUrl,
                MaxServerStartupTimeDuration = TimeSpan.FromDays(1) //TODO: RAVEN5 allow command line override?
            };

            var useEmbedded = !dbPath.StartsWith("http");

            if (useEmbedded)
            {
                EmbeddedServer.Instance.StartServer(serverOptions);
            }
            return new EmbeddedDatabase(expirationProcessTimerInSecond, useEmbedded ? databaseUrl : dbPath, useEmbedded);
        }

        public async Task<IDocumentStore> PrepareDatabase(DatabaseConfiguration config)
        {
            if (!preparedDocumentStores.TryGetValue(config.Name, out var store))
            {
                store = await InitializeDatabase(config).ConfigureAwait(false);
                preparedDocumentStores[config.Name] = store;
            }
            return store;
        }

        public static string ReadLicense()
        {
            using (var resourceStream = typeof(EmbeddedDatabase).Assembly.GetManifestResourceStream("ServiceControl.Infrastructure.RavenDB.RavenLicense.json"))
            using (var reader = new StreamReader(resourceStream))
            {
                return reader.ReadToEnd()
                    .Replace(" ", "")
                    .Replace(Environment.NewLine, "")
                    .Replace("\"", "'"); //Remove line breaks to pass value via command line argument
            }
        }

        async Task<IDocumentStore> InitializeDatabase(DatabaseConfiguration config)
        {
            IDocumentStore documentStore;
            if (useEmbeddedInstance)
            {
                var dbOptions = new DatabaseOptions(config.Name)
                {
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                if (config.FindClrType != null)
                {
                    dbOptions.Conventions.FindClrType += config.FindClrType;
                }

                if (config.EnableDocumentCompression)
                {
                    dbOptions.DatabaseRecord.DocumentsCompression = new DocumentsCompressionConfiguration(
                        false,
                        config.CollectionsToCompress.ToArray()
                    );
                }

                documentStore =
                    await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions).ConfigureAwait(false);
            }
            else
            {
                var store = new DocumentStore
                {
                    Database = config.Name,
                    Urls = new[] { databaseUrl },
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                if (config.FindClrType != null)
                {
                    store.Conventions.FindClrType += config.FindClrType;
                }

                store.Initialize();

                try
                {
                    await store.Maintenance.ForDatabase(config.Name).SendAsync(new GetStatisticsOperation())
                        .ConfigureAwait(false);
                }
                catch (DatabaseDoesNotExistException)
                {
                    try
                    {
                        await store.Maintenance.Server.SendAsync(new CreateDatabaseOperation(new DatabaseRecord(config.Name)))
                            .ConfigureAwait(false);
                    }
                    catch (ConcurrencyException)
                    {
                        // The database was already created before calling CreateDatabaseOperation
                    }

                }

                if (config.EnableDocumentCompression)
                {
                    await store.Maintenance.ForDatabase(config.Name).SendAsync(
                        new UpdateDocumentsCompressionConfigurationOperation(new DocumentsCompressionConfiguration(
                            false,
                            config.CollectionsToCompress.ToArray()
                        ))).ConfigureAwait(false);
                }
                documentStore = store;
            }

            foreach (var indexAssembly in config.IndexAssemblies)
            {
                await IndexCreation.CreateIndexesAsync(indexAssembly, documentStore).ConfigureAwait(false);
            }

            // TODO: Check to see if the configuration has changed.
            // If it has, then send an update to the server to change the expires metadata on all documents
            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = expirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig))
                .ConfigureAwait(false);

            return documentStore;
        }

        public void Dispose()
        {
            foreach (var store in preparedDocumentStores.Values)
            {
                store.Dispose();
            }

            if (useEmbeddedInstance)
            {
                EmbeddedServer.Instance.Dispose();
            }
        }
    }
}