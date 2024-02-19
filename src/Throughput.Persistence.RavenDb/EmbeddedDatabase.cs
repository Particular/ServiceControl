namespace Throughput.Persistence.RavenDb;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using NServiceBus.Logging;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.ServerWide.Operations;
using Raven.Embedded;

public sealed class EmbeddedDatabase(DatabaseConfiguration configuration) : IDisposable
{
    public string ServerUrl { get; } = configuration.ServerConfiguration.ServerUrl;

    static (string LicenseFileName, string ServerDirectory) GetRavenLicenseFileNameAndServerDirectory()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyDirectory = Path.GetDirectoryName(assembly.Location);
        if (assemblyDirectory == null)
        {
            var e = new InvalidOperationException("Could not get path from assembly location");
            e.Data.Add("assembly.Location", assembly.Location);
            throw e;
        }

        var licenseFileName = "RavenLicense.json";
        var ravenLicense = Path.Combine(assemblyDirectory, licenseFileName);
        var serverDirectory = Path.Combine(assemblyDirectory, "RavenDBServer");

        if (File.Exists(ravenLicense))
        {
            return (ravenLicense, serverDirectory);
        }
        else
        {
            var assemblyName = Path.GetFileName(assembly.Location);
            throw new Exception($"RavenDB license not found. Make sure the RavenDB license file '{licenseFileName}' is stored in the same directory as {assemblyName}.");
        }
    }

    public static EmbeddedDatabase Start(DatabaseConfiguration databaseConfiguration)
    {
        var (licenseFileName, serverDirectory) = GetRavenLicenseFileNameAndServerDirectory();

        var nugetPackagesPath = Path.Combine(databaseConfiguration.ServerConfiguration.DbPath, "Packages", "NuGet");

        logger.InfoFormat("Loading RavenDB license from {0}", licenseFileName);
        var serverOptions = new ServerOptions
        {
            CommandLineArgs =
            [
                $"--License.Path=\"{licenseFileName}\"",
                $"--Logs.Mode={databaseConfiguration.ServerConfiguration.LogsMode}",
                // HINT: If this is not set, then Raven will pick a default location relative to the server binaries
                // See https://github.com/ravendb/ravendb/issues/15694
                $"--Indexing.NuGetPackagesPath=\"{nugetPackagesPath}\""
            ],
            AcceptEula = true,
            DataDirectory = databaseConfiguration.ServerConfiguration.DbPath,
            ServerUrl = databaseConfiguration.ServerConfiguration.ServerUrl,
            LogsPath = databaseConfiguration.ServerConfiguration.LogPath
        };

        if (!string.IsNullOrWhiteSpace(serverDirectory))
        {
            serverOptions.ServerDirectory = serverDirectory;
        }

        var embeddedDatabase = new EmbeddedDatabase(databaseConfiguration);

        embeddedDatabase.Start(serverOptions);

        return embeddedDatabase;
    }

    void Start(ServerOptions serverOptions)
    {
        EmbeddedServer.Instance.ServerProcessExited += (sender, args) =>
        {
            if (sender is Process process && process.HasExited && process.ExitCode != 0)
            {
                logger.Warn($"RavenDB server process exited unexpectedly with exitCode: {process.ExitCode}. Process will be restarted.");

                restartRequired = true;
            }
        };

        EmbeddedServer.Instance.StartServer(serverOptions);

        _ = Task.Run(async () =>
        {
            while (!shutdownTokenSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delayBetweenRestarts, shutdownTokenSource.Token).ConfigureAwait(false);

                    if (restartRequired)
                    {
                        logger.Info("Restarting RavenDB server process");

                        await EmbeddedServer.Instance.RestartServerAsync().ConfigureAwait(false);
                        restartRequired = false;

                        logger.Info("RavenDB server process restarted successfully.");
                    }
                }
                catch (OperationCanceledException)
                {
                    //no-op
                }
                catch (Exception e)
                {
                    logger.Fatal($"RavenDB server restart failed. Restart will be retried in {delayBetweenRestarts}.", e);
                }
            }
        });

        RecordStartup();
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

        var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions, cancellationToken).ConfigureAwait(false);

        var databaseSetup = new DatabaseSetup(configuration);
        await databaseSetup.Execute(store, cancellationToken).ConfigureAwait(false);

        return store;
    }

    public static async Task DeleteDatabase(string dbName)
    {
        using var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(new DatabaseOptions(dbName) { SkipCreatingDatabase = true }).ConfigureAwait(false);
        await store.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(dbName, true)).ConfigureAwait(false);
    }

    public void Dispose()
    {
        shutdownTokenSource.Cancel();
        EmbeddedServer.Instance?.Dispose();
    }

    void RecordStartup()
    {
        var dataSize = DataSize();
        var folderSize = FolderSize(configuration.ServerConfiguration.DbPath);

        var startupMessage = $@"
-------------------------------------------------------------
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
RavenDB Logging Level:              {configuration.ServerConfiguration.LogsMode}
-------------------------------------------------------------";

        logger.Info(startupMessage);
    }

    long DataSize()
    {
        var datafilePath = Path.Combine(configuration.ServerConfiguration.DbPath, "Databases", configuration.Name, "Raven.voron");

        try
        {
            var info = new FileInfo(datafilePath);
            if (!info.Exists)
            {
                return -1;
            }
            return info.Length;
        }
        catch
        {
            return -1;
        }
    }

    static long FolderSize(string path)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            var dirSize = DirSize(dir);
            return dirSize;
        }
        catch
        {
            return -1;
        }
    }

    static long DirSize(DirectoryInfo d)
    {
        long size = 0;
        if (d.Exists)
        {
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }

            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += DirSize(di);
            }
        }

        return size;
    }

    readonly CancellationTokenSource shutdownTokenSource = new();
    bool restartRequired;

    static readonly TimeSpan delayBetweenRestarts = TimeSpan.FromSeconds(60);
    static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
}