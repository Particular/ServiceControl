#nullable enable
namespace ServiceControl.RavenDB
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using ByteSizeLib;
    using Microsoft.Extensions.Hosting;
    using NServiceBus.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Conventions;
    using Raven.Client.ServerWide.Operations;
    using Raven.Embedded;
    using Sparrow.Logging;

    public sealed class EmbeddedDatabase : IDisposable
    {
        EmbeddedDatabase(EmbeddedDatabaseConfiguration configuration, IHostApplicationLifetime lifetime)
        {
            this.configuration = configuration;
            ServerUrl = configuration.ServerUrl;
            shutdownCancellationToken = shutdownTokenSource.Token;
            applicationStoppingRegistration = lifetime.ApplicationStopping.Register(() => shutdownTokenSource.Cancel());
        }

        public string ServerUrl { get; private set; }

        static (string LicenseFileName, string ServerDirectory) GetRavenLicenseFileNameAndServerDirectory()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyDirectory = Path.GetDirectoryName(assembly.Location);

            var licenseFileName = "RavenLicense.json";
            var ravenLicense = Path.Combine(assemblyDirectory!, licenseFileName);
            var serverDirectory = Path.Combine(assemblyDirectory!, "RavenDBServer");

            if (File.Exists(ravenLicense))
            {
                return (ravenLicense, serverDirectory);
            }

            var assemblyName = Path.GetFileName(assembly.Location);
            throw new Exception($"RavenDB license not found. Make sure the RavenDB license file '{licenseFileName}' is stored in the same directory as {assemblyName}.");
        }

        public static EmbeddedDatabase Start(EmbeddedDatabaseConfiguration databaseConfiguration, IHostApplicationLifetime lifetime)
        {
            var licenseFileNameAndServerDirectory = GetRavenLicenseFileNameAndServerDirectory();

            var nugetPackagesPath = Path.Combine(databaseConfiguration.DbPath, "Packages", "NuGet");

            LoggingSource.Instance.SetupLogMode(
                (LogMode)255,
                Path.Combine(databaseConfiguration.LogPath, "Raven.Embedded"),
                retentionTime: TimeSpan.FromDays(14),
                retentionSize: 1024 * 1024 * 10,
                compress: false
            );

            LoggingSource.Instance.EnableConsoleLogging();

            Logger.InfoFormat("Loading RavenDB license from {0}", licenseFileNameAndServerDirectory.LicenseFileName);
            var serverOptions = new ServerOptions
            {
                CommandLineArgs =
                [
                    $"--Logs.Mode={databaseConfiguration.LogsMode}",
                    // HINT: If this is not set, then Raven will pick a default location relative to the server binaries
                    // See https://github.com/ravendb/ravendb/issues/15694
                    $"--Indexing.NuGetPackagesPath=\"{nugetPackagesPath}\""
                ],
                DataDirectory = databaseConfiguration.DbPath,
                ServerUrl = databaseConfiguration.ServerUrl,
                LogsPath = databaseConfiguration.LogPath
            };

            serverOptions.Licensing.EulaAccepted = true;
            serverOptions.Licensing.LicensePath = licenseFileNameAndServerDirectory.LicenseFileName;

            if (!string.IsNullOrWhiteSpace(licenseFileNameAndServerDirectory.ServerDirectory))
            {
                serverOptions.ServerDirectory = licenseFileNameAndServerDirectory.ServerDirectory;
            }

            var embeddedDatabase = new EmbeddedDatabase(databaseConfiguration, lifetime);
            embeddedDatabase.Start(serverOptions);

            RecordStartup(databaseConfiguration);

            return embeddedDatabase;
        }

        void Start(ServerOptions serverOptions)
        {
            this.serverOptions = serverOptions;
            EmbeddedServer.Instance.ServerProcessExited += OnServerProcessExited;
            EmbeddedServer.Instance.StartServer(serverOptions);

            _ = Task.Run(async () =>
            {
                while (!shutdownCancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(delayBetweenRestarts, shutdownCancellationToken);

                        if (!restartRequired)
                        {
                            continue;
                        }

                        shutdownCancellationToken.ThrowIfCancellationRequested();

                        Logger.Info("Restarting RavenDB server process");

                        await EmbeddedServer.Instance.RestartServerAsync();
                        restartRequired = false;

                        Logger.Info("RavenDB server process restarted successfully.");
                    }
                    catch (OperationCanceledException) when (shutdownCancellationToken.IsCancellationRequested)
                    {
                        //no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Fatal($"RavenDB server restart failed. Restart will be retried in {delayBetweenRestarts}.", e);
                    }
                }
            }, CancellationToken.None);
        }

        void OnServerProcessExited(object? sender, ServerProcessExitedEventArgs _)
        {
            if (shutdownCancellationToken.IsCancellationRequested)
            {
                return;
            }

            restartRequired = true;
            if (sender is Process process)
            {
                Logger.Warn($"RavenDB server process exited unexpectedly with exitCode: {process.ExitCode}. Process will be restarted.");
            }
            else
            {
                Logger.Warn($"RavenDB server process exited unexpectedly. Process will be restarted.");
            }
        }

        public async Task<IDocumentStore> Connect(CancellationToken cancellationToken)
        {
            var dbOptions = new DatabaseOptions(configuration.Name)
            {
                Conventions = new DocumentConventions
                {
                    SaveEnumsAsIntegers = true
                },
                SkipCreatingDatabase = true
            };

            if (configuration.FindClrType != null)
            {
                dbOptions.Conventions.FindClrType += configuration.FindClrType;
            }

            var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(dbOptions, cancellationToken);
            return store;
        }

        public async Task DeleteDatabase(string dbName)
        {
            using var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(new DatabaseOptions(dbName)
            {
                SkipCreatingDatabase = true
            });
            await store.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(dbName, true));
        }

        public async Task Stop(CancellationToken cancellationToken)
        {
            Logger.Debug("Stopping RavenDB server");
            EmbeddedServer.Instance.ServerProcessExited -= OnServerProcessExited;

            await shutdownTokenSource.CancelAsync();

            // EmbeddedServer does not have have an async Stop method, the Dispose operation blocks and waits
            // until its GracefulShutdownTimeout is reached and then does a Process.Kill
            //
            // This logic gets the child process ID uses a Task.Delay with infinite.
            //
            // a. The Task.Delay gets cancelled first
            // b. The EmbeddedServer.Dispose completes first
            //
            // If the Task.Delay gets cancelled first this means Dispose is still running and
            // then we kill the process

            serverOptions!.GracefulShutdownTimeout = TimeSpan.FromHours(1); // During Stop/Dispose we manually control this

            var processId = await EmbeddedServer.Instance.GetServerProcessIdAsync(cancellationToken);

            // Dispose always need to be invoked, even when already cancelled
            var disposeTask = Task.Run(() => EmbeddedServer.Instance.Dispose(), CancellationToken.None);

            var waitForCancellationTask = Task.Delay(Timeout.Infinite, cancellationToken);

            var delayIsCancelled = waitForCancellationTask == await Task.WhenAny(disposeTask, waitForCancellationTask);

            if (delayIsCancelled)
            {
                try
                {
                    Logger.WarnFormat("Killing RavenDB server PID {0} because host cancelled", processId);
                    using var ravenChildProcess = Process.GetProcessById(processId);
                    ravenChildProcess.Kill(entireProcessTree: true);
                    // Kill only signals
                    Logger.WarnFormat("Waiting for RavenDB server PID {0} to exit... ", processId);
                    await ravenChildProcess.WaitForExitAsync(CancellationToken.None);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("Failed to kill RavenDB server PID {0} shutdown\n{1}", processId, e);
                }
            }

            serverOptions = null!;
            Logger.Debug("Stopped RavenDB server");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (serverOptions != null)
            {
                EmbeddedServer.Instance.ServerProcessExited -= OnServerProcessExited;
            }

            shutdownTokenSource.Cancel();

            if (serverOptions != null)
            {
                // Set GracefulShutdownTimeout to Zero and exist ASAP, under normal operation instance would already
                // have been allowed to gracefully stop during "Stop" method.
                serverOptions!.GracefulShutdownTimeout = TimeSpan.Zero;
                Logger.Debug("Disposing RavenDB server");
                EmbeddedServer.Instance.Dispose();
                Logger.Debug("Disposed RavenDB server");
            }

            shutdownTokenSource.Dispose();
            applicationStoppingRegistration.Dispose();

            disposed = true;
        }

        static void RecordStartup(EmbeddedDatabaseConfiguration configuration)
        {
            var dataSize = DataSize(configuration);
            var folderSize = FolderSize(configuration.DbPath);

            var startupMessage = $@"
-------------------------------------------------------------
Database Url:                       {configuration.ServerUrl}
Database Path:                      {configuration.DbPath}
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
RavenDB Logging Level:              {configuration.LogsMode}
-------------------------------------------------------------";

            Logger.Info(startupMessage);
        }

        static long DataSize(EmbeddedDatabaseConfiguration configuration)
        {
            var datafilePath = Path.Combine(configuration.DbPath, "Databases", configuration.Name, "Raven.voron");

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

        bool disposed;
        bool restartRequired;
        readonly CancellationTokenSource shutdownTokenSource = new();
        readonly EmbeddedDatabaseConfiguration configuration;
        readonly CancellationToken shutdownCancellationToken;
        readonly CancellationTokenRegistration applicationStoppingRegistration;
        ServerOptions? serverOptions;

        static TimeSpan delayBetweenRestarts = TimeSpan.FromSeconds(60);
        static readonly ILog Logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}