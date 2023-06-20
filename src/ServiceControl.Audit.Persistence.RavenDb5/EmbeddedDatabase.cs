namespace ServiceControl.Audit.Persistence.RavenDb
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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

        static (string LicenseFileName, string ServerDirectory) GetRavenLicenseFileNameAndServerDirectory()
        {
            var licenseFileName = "RavenLicense.json";
            var localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, licenseFileName);
            if (File.Exists(localRavenLicense))
            {
                return (localRavenLicense, null);
            }

            //TODO: refactor this to extract the folder name to a constant
            localRavenLicense = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Persisters", "RavenDB5", licenseFileName);
            if (!File.Exists(localRavenLicense))
            {
                throw new Exception($"RavenDB license not found. Make sure the RavenDB license file, '{licenseFileName}', " +
                    $"is stored in the '{AppDomain.CurrentDomain.BaseDirectory}' folder or in the 'Persisters/RavenDB5' subfolder.");
            }

            // By default RavenDB 5 searches its binaries in the RavenDBServer right below the BaseDirectory.
            // If we're loading from Persisters/RavenDB5 we also have to signal RavenDB where are binaries
            var serverDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Persisters", "RavenDB5", "RavenDBServer");

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

            EmbeddedServer.Instance.ServerProcessExited += embeddedDatabase.OnServerProcessExited;
            EmbeddedServer.Instance.StartServer(serverOptions);

            return new EmbeddedDatabase(databaseConfiguration);
        }

        async void OnServerProcessExited(object sender, ServerProcessExitedEventArgs args)
        {
            if (sender is Process process)
            {
                if (process.HasExited && process.ExitCode != 0)
                {
                    bool serverRestarted = false;
                    while (!shutdownTokenSource.IsCancellationRequested && !serverRestarted)
                    {
                        logger.Warn($"RavenDB server process exited unexpectedly with exitCode: {process.ExitCode}. Process will be restarted in {delayBetweenRestarts}.");

                        try
                        {
                            await Task.Delay(delayBetweenRestarts, shutdownTokenSource.Token).ConfigureAwait(false);

                            await EmbeddedServer.Instance.RestartServerAsync().ConfigureAwait(false);
                            serverRestarted = true;

                            logger.Info("RavenDB server process restarted successfully.");
                        }
                        catch (OperationCanceledException)
                        {
                            //no-op
                        }
                        catch (Exception e)
                        {
                            logger.Fatal("RavenDB server restart failed.", e);
                        }
                    }
                }
            }
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
            shutdownTokenSource.Cancel();
            EmbeddedServer.Instance?.Dispose();
        }

        CancellationTokenSource shutdownTokenSource = new CancellationTokenSource();
        readonly DatabaseConfiguration configuration;

        static TimeSpan delayBetweenRestarts = TimeSpan.FromSeconds(60);
        static readonly ILog logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}