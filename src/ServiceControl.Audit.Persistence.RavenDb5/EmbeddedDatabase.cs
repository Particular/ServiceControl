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
    using Raven.Client.ServerWide;
    using Raven.Embedded;
    using ServiceControl.Audit.Persistence.RavenDb5;

    public class EmbeddedDatabase : IDisposable
    {
        public EmbeddedDatabase(string databaseUrl, bool useEmbeddedInstance, AuditDatabaseConfiguration configuration)
        {
            this.databaseUrl = databaseUrl;
            this.useEmbeddedInstance = useEmbeddedInstance;
            this.configuration = configuration;
        }

        public static EmbeddedDatabase Start(string dbPath, string databaseMaintenanceUrl, AuditDatabaseConfiguration auditDatabaseConfiguration)
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

            return new EmbeddedDatabase(databaseMaintenanceUrl, true, auditDatabaseConfiguration);
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

                return await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions, cancellationToken).ConfigureAwait(false);
            }

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


            return store;
        }

        public void Dispose()
        {
            if (useEmbeddedInstance)
            {
                EmbeddedServer.Instance?.Dispose();
            }
        }

        readonly string databaseUrl;
        readonly bool useEmbeddedInstance;
        readonly AuditDatabaseConfiguration configuration;

        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}