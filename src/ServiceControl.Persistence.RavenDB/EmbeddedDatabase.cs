﻿#nullable enable

namespace ServiceControl.Persistence.RavenDB
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

    public sealed class EmbeddedDatabase : IDisposable
    {
        EmbeddedDatabase(RavenPersisterSettings configuration, IHostApplicationLifetime lifetime)
        {
            this.configuration = configuration;
            ServerUrl = configuration.ServerUrl;
            shutdownTokenSourceRegistration = shutdownTokenSource.Token.Register(() => isStopping = true);
            applicationStoppingRegistration = lifetime.ApplicationStopping.Register(() => isStopping = true);
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

        internal static EmbeddedDatabase Start(RavenPersisterSettings settings, IHostApplicationLifetime lifetime)
        {
            var licenseFileNameAndServerDirectory = GetRavenLicenseFileNameAndServerDirectory();

            var nugetPackagesPath = Path.Combine(settings.DatabasePath, "Packages", "NuGet");

            Logger.InfoFormat("Loading RavenDB license from {0}", licenseFileNameAndServerDirectory.LicenseFileName);
            var serverOptions = new ServerOptions
            {
                CommandLineArgs =
                [
                    $"--License.Path=\"{licenseFileNameAndServerDirectory.LicenseFileName}\"",
                    $"--Logs.Mode={settings.LogsMode}",
                    // HINT: If this is not set, then Raven will pick a default location relative to the server binaries
                    // See https://github.com/ravendb/ravendb/issues/15694
                    $"--Indexing.NuGetPackagesPath=\"{nugetPackagesPath}\""
                ],
                AcceptEula = true,
                DataDirectory = settings.DatabasePath,
                ServerUrl = settings.ServerUrl,
                LogsPath = settings.LogPath
            };

            if (!string.IsNullOrWhiteSpace(licenseFileNameAndServerDirectory.ServerDirectory))
            {
                serverOptions.ServerDirectory = licenseFileNameAndServerDirectory.ServerDirectory;
            }

            var embeddedDatabase = new EmbeddedDatabase(settings, lifetime);
            embeddedDatabase.Start(serverOptions);

            RecordStartup(settings);

            return embeddedDatabase;
        }

        void Start(ServerOptions serverOptions)
        {
            EmbeddedServer.Instance.ServerProcessExited += OnServerProcessExited;
            EmbeddedServer.Instance.StartServer(serverOptions);

            _ = Task.Run(async () =>
            {
                while (!isStopping)
                {
                    try
                    {
                        await Task.Delay(delayBetweenRestarts, shutdownTokenSource.Token);

                        if (!restartRequired)
                        {
                            continue;
                        }

                        Logger.Info("Restarting RavenDB server process");

                        await EmbeddedServer.Instance.RestartServerAsync();
                        restartRequired = false;

                        Logger.Info("RavenDB server process restarted successfully.");
                    }
                    catch (OperationCanceledException)
                    {
                        //no-op
                    }
                    catch (Exception e)
                    {
                        Logger.Fatal($"RavenDB server restart failed. Restart will be retried in {delayBetweenRestarts}.", e);
                    }
                }
            });
        }

        void OnServerProcessExited(object? sender, ServerProcessExitedEventArgs _)
        {
            if (isStopping)
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

        public async Task DeleteDatabase(string dbName)
        {
            using var store = await EmbeddedServer.Instance.GetDocumentStoreAsync(new DatabaseOptions(dbName) { SkipCreatingDatabase = true });
            await store.Maintenance.Server.SendAsync(new DeleteDatabasesOperation(dbName, true));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            EmbeddedServer.Instance.ServerProcessExited -= OnServerProcessExited;

            shutdownTokenSource.Cancel();
            EmbeddedServer.Instance.Dispose();
            shutdownTokenSource.Dispose();
            applicationStoppingRegistration.Dispose();
            shutdownTokenSourceRegistration.Dispose();

            disposed = true;
        }

        static void RecordStartup(RavenPersisterSettings settings)
        {
            var dataSize = DataSize(settings);
            var folderSize = FolderSize(settings);

            var startupMessage = $@"
-------------------------------------------------------------
Database Path:                      {settings.DatabasePath}
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
RavenDB Logging Level:              {settings.LogsMode}
-------------------------------------------------------------";

            Logger.Info(startupMessage);
        }

        static long DataSize(RavenPersisterSettings settings)
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

        static long FolderSize(RavenPersisterSettings settings)
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

        bool disposed;
        bool restartRequired;
        bool isStopping;
        readonly CancellationTokenSource shutdownTokenSource = new();
        readonly RavenPersisterSettings configuration;
        readonly CancellationTokenRegistration applicationStoppingRegistration;
        readonly CancellationTokenRegistration shutdownTokenSourceRegistration;

        static TimeSpan delayBetweenRestarts = TimeSpan.FromSeconds(60);
        static readonly ILog Logger = LogManager.GetLogger<EmbeddedDatabase>();
    }
}