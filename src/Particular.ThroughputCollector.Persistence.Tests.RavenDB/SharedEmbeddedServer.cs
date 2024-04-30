namespace Particular.ThroughputCollector.Persistence.Tests.RavenDb
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.ServerWide.Operations;
    using Raven.Embedded;
    using TestHelper;

    static class SharedEmbeddedServer
    {
        const string DatabaseNameRoot = "throughput-persistence-tests";

        static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
        static readonly string AssemblyDirectory = Path.GetDirectoryName(Assembly.Location);
        static readonly string DbPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Tests", "ThroughputData");
        static readonly string LogPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Logs", "Throughput");
        static readonly string ServerUrl = $"http://localhost:{PortUtility.FindAvailablePort(33334)}";
        static readonly string NugetPackagesPath = Path.Combine(DbPath, "Packages", "NuGet");
        static readonly string RavenDbLicensePath = Path.Combine(AssemblyDirectory!, "RavenLicense.json");
        static readonly string ServerDirectory = Path.Combine(AssemblyDirectory!, "RavenDBServer");

        static readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        static IDocumentStore store;

        public static async Task<IDocumentStore> GetInstance(CancellationToken cancellationToken = default)
        {
            if (store != null)
            {
                return store;
            }

            await semaphoreSlim.WaitAsync(cancellationToken);

            try
            {
                if (store != null)
                {
                    return store;
                }

                var dbOptions = new DatabaseOptions($"{DatabaseNameRoot}-{DateTime.Now:yyMMdd-HHmmss}")
                {
                    Conventions = new DocumentConventions
                    {
                        SaveEnumsAsIntegers = true
                    }
                };

                store = await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions, cancellationToken);

                var cleanupDatabases = new DirectoryInfo(DbPath)
                    .GetDirectories()
                    .Select(di => di.Name)
                    .Where(name => name.StartsWith(DatabaseNameRoot))
                    .ToArray();

                if (cleanupDatabases.Length > 0)
                {
                    var cleanupOperation = new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters { DatabaseNames = cleanupDatabases, HardDelete = true });
                    await store.Maintenance.Server.SendAsync(cleanupOperation, CancellationToken.None);
                }

                return store;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public static async Task StartServer(CancellationToken cancellationToken = default)
        {
            await semaphoreSlim.WaitAsync(cancellationToken);

            try
            {
                var serverOptions = new ServerOptions
                {
                    CommandLineArgs =
                    [
                        $"--License.Path=\"{RavenDbLicensePath}\"",
                        "--Logs.Mode=Operations",
                        // HINT: If this is not set, then Raven will pick a default location relative to the server binaries
                        // See https://github.com/ravendb/ravendb/issues/15694
                        $"--Indexing.NuGetPackagesPath=\"{NugetPackagesPath}\""
                    ],
                    AcceptEula = true,
                    ServerDirectory = ServerDirectory,
                    DataDirectory = DbPath,
                    ServerUrl = ServerUrl,
                    LogsPath = LogPath
                };

                EmbeddedServer.Instance.StartServer(serverOptions);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public static async Task StopServer(CancellationToken cancellationToken = default)
        {
            await semaphoreSlim.WaitAsync(cancellationToken);
            try
            {
                store.Dispose();
                store = null;
                EmbeddedServer.Instance.Dispose();
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }
    }
}
