using System.IO;
using NServiceBus.Logging;

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
    using System.Reflection;
    using System.Threading.Tasks;

    public class EmbeddedDatabase : IDisposable
    {
        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();

        readonly int expirationProcessTimerInSeconds;
        readonly Dictionary<string, IDocumentStore> preparedDocumentStores = new Dictionary<string, IDocumentStore>();

        public EmbeddedDatabase(int expirationProcessTimerInSeconds)
        {
            this.expirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
        }


        public static EmbeddedDatabase Start(string dbPath, string logPath, int expirationProcessTimerInSecond, string databaseUrl)
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

            var highestUsableNetCoreRuntime = NetCoreRuntime.FindAll()
                .Where(x => x.Runtime == "Microsoft.NETCore.App")
                .Where(x => x.Version.Major == 3 && x.Version.Minor == 1)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault() ?? throw new Exception("Could not find any .NET Core runtime 3.1.x");

            var serverOptions = new ServerOptions
            {
                CommandLineArgs = commandLineArgs,
                AcceptEula = true,
                DataDirectory = dbPath,
                LogsPath = logPath,
                FrameworkVersion = highestUsableNetCoreRuntime.Version.ToString(),
                ServerUrl = databaseUrl,
            };
            EmbeddedServer.Instance.StartServer(serverOptions);
            return new EmbeddedDatabase(expirationProcessTimerInSecond);
        }

        public async Task<IDocumentStore> PrepareDatabase(string name, params Assembly[] indexAssemblies)
        {
            if (!preparedDocumentStores.TryGetValue(name, out var store))
            {
                store = await InitializeDatabase(name, indexAssemblies).ConfigureAwait(false);
                preparedDocumentStores[name] = store;
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

        async Task<IDocumentStore> InitializeDatabase(string name, Assembly[] indexAssemblies)
        {
            var dbOptions = new DatabaseOptions(name)
            {
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                }
            };

            var documentStore =
                await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions).ConfigureAwait(false);

            foreach (var indexAssembly in indexAssemblies)
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
            EmbeddedServer.Instance.Dispose();
        }
    }
}