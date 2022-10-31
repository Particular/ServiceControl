namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Embedded;

    public class EmbeddedDatabase : IDisposable
    {
        public EmbeddedDatabase(DatabaseConfiguration configuration)
        {
            this.configuration = configuration;
            ServerUrl = configuration.ServerConfiguration.ServerUrl;
        }

        public string ServerUrl { get; private set; }

        public static EmbeddedDatabase Start(DatabaseConfiguration databaseConfiguration)
        {
            var licenseFileName = "RavenLicense.json";
            var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, licenseFileName);
            if (!File.Exists(localRavenLicense))
            {
                throw new Exception($"RavenDB license not found. Make sure the RavenDB license file, '{licenseFileName}', is stored in the '{AppDomain.CurrentDomain.BaseDirectory}' folder.");
            }

            logger.InfoFormat("Loading RavenDB license from {0}", localRavenLicense);
            var serverOptions = new ServerOptions
            {
                CommandLineArgs = new List<string>
                {
                    $"--License.Path=\"{localRavenLicense}\""
                },
                AcceptEula = true,
                DataDirectory = databaseConfiguration.ServerConfiguration.DbPath,
                ServerUrl = databaseConfiguration.ServerConfiguration.ServerUrl
            };

            EmbeddedServer.Instance.StartServer(serverOptions);

            return new EmbeddedDatabase(databaseConfiguration);
        }

        public async Task<IDocumentStore> Connect(CancellationToken cancellationToken)
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

            return await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            EmbeddedServer.Instance?.Dispose();
        }

        readonly DatabaseConfiguration configuration;

        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}