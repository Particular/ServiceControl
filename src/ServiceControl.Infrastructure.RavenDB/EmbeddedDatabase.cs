using System.IO;
using NServiceBus.Logging;
using Sparrow.Json;

namespace ServiceControl.Infrastructure.RavenDB
{
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Embedded;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;

    public class EmbeddedDatabase : IDisposable
    {
        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();

        readonly int expirationProcessTimerInSeconds;
        readonly Dictionary<string, IDocumentStore> preparedDocumentStores = new Dictionary<string, IDocumentStore>();

        public EmbeddedDatabase(int expirationProcessTimerInSeconds)
        {
            this.expirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
        }


        public static EmbeddedDatabase Start(string dbPath, string logPath, string specificRuntimeVersion, int expirationProcessTimerInSecond, string databaseUrl)
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
            
            // HINT: Needed for document compression
            commandLineArgs.Add("Features.Availability=Experimental");

            var highestUsableNetCoreRuntime = NetCoreRuntime.FindAll()
                .Where(x => x.Runtime == "Microsoft.NETCore.App")
                .Where(x => x.Version.Major == 5 && x.Version.Minor == 0)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault() ?? throw new Exception("Could not find any .NET runtime 5.0.x");

            var serverOptions = new ServerOptions
            {
                CommandLineArgs = commandLineArgs,
                AcceptEula = true,
                DataDirectory = dbPath,
                LogsPath = logPath,
                FrameworkVersion = specificRuntimeVersion ?? highestUsableNetCoreRuntime.Version.ToString(),
                ServerUrl = databaseUrl,
                MaxServerStartupTimeDuration = TimeSpan.FromDays(1) //TODO: RAVEN5 allow command line override?
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
            return new EmbeddedDatabase(expirationProcessTimerInSecond);
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
                    .Replace(" ","")
                    .Replace(Environment.NewLine, "")
                    .Replace("\"", "'"); //Remove line breaks to pass value via command line argument
            }
        }

        async Task<IDocumentStore> InitializeDatabase(DatabaseConfiguration config)
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

            var documentStore =
                await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions).ConfigureAwait(false);

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

            if (config.EnableDocumentCompression)
            {
                await EnableDocumentCompression(documentStore, config.CollectionsToCompress)
                .ConfigureAwait(false);
            }

            return documentStore;
        }

        private async Task EnableDocumentCompression(IDocumentStore documentStore, IEnumerable<string> collectionsToCompress)
        {
            var record = await documentStore.Maintenance.Server.SendAsync(
                new GetDatabaseRecordOperation(documentStore.Database)
            ).ConfigureAwait(false);
            
            record.DocumentsCompression = new DocumentsCompressionConfiguration(true, collectionsToCompress.ToArray());

            await documentStore.Maintenance.Server.SendAsync(new UpdateDatabaseOperation(record, record.Etag))
                .ConfigureAwait(false);
        }

        public void Dispose()
        {
            foreach (var store in preparedDocumentStores.Values)
            {
                store.Dispose();
            }
            EmbeddedServer.Instance.Dispose();
        }
    }
}