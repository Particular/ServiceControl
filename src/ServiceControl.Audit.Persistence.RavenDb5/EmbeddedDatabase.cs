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
        public EmbeddedDatabase(AuditDatabaseConfiguration configuration, string serverUrl)
        {
            this.configuration = configuration;
            ServerUrl = serverUrl;
        }

        public string ServerUrl { get; private set; }

        public static EmbeddedDatabase Start(string dbPath, string serverUrl, AuditDatabaseConfiguration auditDatabaseConfiguration)
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
                ServerUrl = serverUrl,
                MaxServerStartupTimeDuration = TimeSpan.FromDays(1) //TODO: RAVEN5 allow command line override?
            };

            EmbeddedServer.Instance.StartServer(serverOptions);

            return new EmbeddedDatabase(auditDatabaseConfiguration, serverUrl);
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

        public void Dispose()
        {
            EmbeddedServer.Instance?.Dispose();
        }

        readonly AuditDatabaseConfiguration configuration;

        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}