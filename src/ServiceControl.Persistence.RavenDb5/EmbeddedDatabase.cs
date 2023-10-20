namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using System.Collections.Generic;
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
    using Raven.Embedded;

    public class EmbeddedDatabase : IDisposable
    {
        EmbeddedDatabase(RavenDBPersisterSettings configuration)
        {
            this.configuration = configuration;
            ServerUrl = configuration.ServerUrl;
        }

        public string ServerUrl { get; private set; }

        static (string LicenseFileName, string ServerDirectory) GetRavenLicenseFileNameAndServerDirectory()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyDirectory = Path.GetDirectoryName(assembly.Location);

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

        internal static EmbeddedDatabase Start(RavenDBPersisterSettings settings)
        {
            var licenseFileNameAndServerDirectory = GetRavenLicenseFileNameAndServerDirectory();

            var nugetPackagesPath = Path.Combine(settings.DatabasePath, "Packages", "NuGet");

            logger.InfoFormat("Loading RavenDB license from {0}", licenseFileNameAndServerDirectory.LicenseFileName);
            var serverOptions = new ServerOptions
            {
                CommandLineArgs = new List<string>
                {
                    $"--License.Path=\"{licenseFileNameAndServerDirectory.LicenseFileName}\"",
                    $"--Logs.Mode={settings.LogsMode}",
                    // HINT: If this is not set, then Raven will pick a default location relative to the server binaries
                    // See https://github.com/ravendb/ravendb/issues/15694
                    $"--Indexing.NuGetPackagesPath=\"{nugetPackagesPath}\""
                },
                AcceptEula = true,
                DataDirectory = settings.DatabasePath,
                ServerUrl = settings.ServerUrl,
                LogsPath = settings.LogPath
            };

            if (!string.IsNullOrWhiteSpace(licenseFileNameAndServerDirectory.ServerDirectory))
            {
                serverOptions.ServerDirectory = licenseFileNameAndServerDirectory.ServerDirectory;
            }

            var embeddedDatabase = new EmbeddedDatabase(settings);

            embeddedDatabase.Start(serverOptions);

            RecordStartup(settings);

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

            var _ = Task.Run(async () =>
            {
                while (!shutdownTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(delayBetweenRestarts, shutdownTokenSource.Token);

                        if (restartRequired)
                        {
                            logger.Info("Restarting RavenDB server process");

                            await EmbeddedServer.Instance.RestartServerAsync();
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
        }

        public async Task<IDocumentStore> Connect(CancellationToken cancellationToken = default)
        {
            var dbOptions = new DatabaseOptions(configuration.DatabaseName)
            {
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                }
            };

            var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions, cancellationToken);

            var databaseSetup = new DatabaseSetup(configuration);
            await databaseSetup.Execute(store, cancellationToken);

            return store;
        }

        public void Dispose()
        {
            shutdownTokenSource.Cancel();
            EmbeddedServer.Instance?.Dispose();
        }

        static void RecordStartup(RavenDBPersisterSettings settings)
        {
            var dataSize = DataSize(settings);
            var folderSize = FolderSize(settings);

            var startupMessage = $@"
-------------------------------------------------------------
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
-------------------------------------------------------------";

            logger.Info(startupMessage);
        }

        static long DataSize(RavenDBPersisterSettings settings)
        {
            var datafilePath = Path.Combine(settings.DatabasePath, "Databases", settings.DatabaseName, "Raven.voron");

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

        static long FolderSize(RavenDBPersisterSettings settings)
        {
            try
            {
                var dir = new DirectoryInfo(settings.DatabasePath);
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
        CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        bool restartRequired;
        readonly RavenDBPersisterSettings configuration;

        static TimeSpan delayBetweenRestarts = TimeSpan.FromSeconds(60);
        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}