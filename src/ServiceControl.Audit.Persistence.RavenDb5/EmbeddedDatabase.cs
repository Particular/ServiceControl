namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using ByteSizeLib;
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

        static (string LicenseFileName, string ServerDirectory) GetRavenLicenseFileNameAndServerDirectory()
        {
            var licenseFileName = "RavenLicense.json";
            var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, licenseFileName);
            if (File.Exists(localRavenLicense))
            {
                return (localRavenLicense, null);
            }

            const string Persisters = "Persisters";
            const string RavenDB5 = "RavenDB5";

            var persisterDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Persisters, RavenDB5);

            localRavenLicense = Path.Combine(persisterDirectory, licenseFileName);
            if (!File.Exists(localRavenLicense))
            {
                throw new Exception($"RavenDB license not found. Make sure the RavenDB license file, '{licenseFileName}', " +
                    $"is stored in the '{AppDomain.CurrentDomain.BaseDirectory}' folder or in the '{Persisters}/{RavenDB5}' subfolder.");
            }

            // By default RavenDB 5 searches its binaries in the RavenDBServer right below the BaseDirectory.
            // If we're loading from Persisters/RavenDB5 we also have to signal RavenDB where are binaries
            var serverDirectory = Path.Combine(persisterDirectory, "RavenDBServer");

            return (localRavenLicense, serverDirectory);
        }

        public static EmbeddedDatabase Start(DatabaseConfiguration databaseConfiguration)
        {
            var licenseFileNameAndServerDirectory = GetRavenLicenseFileNameAndServerDirectory();

            var nugetPackagesPath = Path.Combine(databaseConfiguration.ServerConfiguration.DbPath, "Packages", "NuGet");

            logger.InfoFormat("Loading RavenDB license from {0}", licenseFileNameAndServerDirectory.LicenseFileName);
            var serverOptions = new ServerOptions
            {
                CommandLineArgs = new List<string>
                {
                    $"--License.Path=\"{licenseFileNameAndServerDirectory.LicenseFileName}\"",
                    $"--Logs.Mode={databaseConfiguration.ServerConfiguration.LogsMode}",
                    // HINT: If this is not set, then Raven will pick a default location relative to the server binaries
                    // See https://github.com/ravendb/ravendb/issues/15694
                    $"--Indexing.NuGetPackagesPath=\"{nugetPackagesPath}\""
                },
                AcceptEula = true,
                DataDirectory = databaseConfiguration.ServerConfiguration.DbPath,
                ServerUrl = databaseConfiguration.ServerConfiguration.ServerUrl,
                LogsPath = databaseConfiguration.ServerConfiguration.LogPath
            };

            if (!string.IsNullOrWhiteSpace(licenseFileNameAndServerDirectory.ServerDirectory))
            {
                serverOptions.ServerDirectory = licenseFileNameAndServerDirectory.ServerDirectory;
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

            if (configuration.FindClrType != null)
            {
                dbOptions.Conventions.FindClrType += configuration.FindClrType;
            }

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

        void RecordStartup()
        {
            var dataSize = DataSize();
            var folderSize = FolderSize(configuration.ServerConfiguration.DbPath);

            var startupMessage = $@"
-------------------------------------------------------------
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
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

        long FolderSize(string path)
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

        CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        bool restartRequired;
        readonly DatabaseConfiguration configuration;

        static TimeSpan delayBetweenRestarts = TimeSpan.FromSeconds(60);
        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}