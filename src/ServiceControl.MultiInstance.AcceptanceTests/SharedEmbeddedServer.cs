using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.ServerWide.Operations;
using ServiceControl.Persistence.RavenDB;
using TestHelper;

static class SharedEmbeddedServer
{
    public static async Task<EmbeddedDatabase> GetInstance(CancellationToken cancellationToken = default)
    {
        var basePath = Path.Combine(Path.GetTempPath(), "ServiceControlTests", "Primary.RavenMultiInstance");
        var dbPath = Path.Combine(basePath, "DB");
        var databasesPath = Path.Combine(dbPath, "Databases");

        var settings = new RavenPersisterSettings
        {
            DatabasePath = dbPath,
            LogPath = Path.Combine(basePath, "Logs"),
            LogsMode = Sparrow.Logging.LogMode.Operations,
            DatabaseMaintenancePort = PortUtility.FindAvailablePort(RavenPersisterSettings.DatabaseMaintenancePortDefault)
        };

        var instance = EmbeddedDatabase.Start(settings);

        // Make sure that the database is up - this blocks until the cancellation token times out
        using (var docStore = await instance.Connect(cancellationToken))
        {
            var cleanupDatabases = new DirectoryInfo(databasesPath)
                .GetDirectories()
                .Select(di => di.Name)
                .Where(name => name.Length == 32)
                .ToArray();

            if (cleanupDatabases.Any())
            {
                var cleanupOperation = new DeleteDatabasesOperation(new DeleteDatabasesOperation.Parameters { DatabaseNames = cleanupDatabases, HardDelete = true });
                await docStore.Maintenance.Server.SendAsync(cleanupOperation, CancellationToken.None);
            }
        }

        return instance;
    }
}