namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using Auditing;
    using ByteSizeLib;
    using Metrics;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Monitoring;
    using NLog.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using ServiceControl.Audit.Persistence;
    using Settings;
    using Transports;
    using WebApi;

    class Bootstrapper
    {
        public IHostBuilder HostBuilder { get; private set; }

        public Bootstrapper(
            Action<ICriticalErrorContext> onCriticalError,
            Settings.Settings settings,
            EndpointConfiguration configuration,
            LoggingSettings loggingSettings)
        {
            this.onCriticalError = onCriticalError;
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggingSettings = loggingSettings;
            this.settings = settings;

            CreateHost();
        }

        void CreateHost()
        {
            var persistenceConfiguration = PersistenceConfigurationFactory.LoadPersistenceConfiguration(settings.PersistenceCustomizationType);
            var persistenceSettings = persistenceConfiguration.BuildPersistenceSettings(settings);

            RecordStartup(loggingSettings, configuration, persistenceConfiguration);

            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                configuration.License(settings.LicenseFileText);
            }

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            transportSettings = MapSettings(settings);
            transportCustomization = settings.LoadTransportCustomization();

            HostBuilder = new HostBuilder();
            HostBuilder
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    //HINT: configuration used by NLog comes from LoggingConfigurator.cs
                    builder.AddNLog();
                    builder.SetMinimumLevel(loggingSettings.ToHostLogLevel());
                })
                .ConfigureServices(services =>
                {
                    services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
                    services.AddSingleton(transportSettings);
                    services.AddSingleton(transportCustomization);

                    services.AddSingleton(loggingSettings);
                    services.AddSingleton(settings);
                    services.AddSingleton<EndpointInstanceMonitoring>();
                    services.AddSingleton<AuditIngestor>();
                    services.AddSingleton<ImportFailedAudits>();
                    services.AddSingleton<AuditIngestionCustomCheck.State>(); // required by the ingestion custom check which is auto-loaded
                })
                .UseMetrics(settings.PrintMetrics)
                .SetupPersistence(persistenceSettings, persistenceConfiguration)
                .UseNServiceBus(context =>
                {
                    NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings, onCriticalError, configuration, false);

                    return configuration;
                })
                .ConfigureServices(services =>
                {
                    // Configure after the NServiceBus hosted service to ensure NServiceBus is already started
                    if (settings.IngestAuditMessages)
                    {
                        services.AddHostedService<AuditIngestion>();
                    }
                })
                .UseWebApi(settings.RootUrl, settings.ExposeApi);
        }

        static TransportSettings MapSettings(Settings.Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel
            };
            return transportSettings;
        }

        long DataSize(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath))
            {
                return 0;
            }

            var datafilePath = Path.Combine(dbPath, "data");

            try
            {
                var info = new FileInfo(datafilePath);

                return info.Length;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        long FolderSize(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath))
            {
                return 0;
            }

            try
            {
                var dir = new DirectoryInfo(dbPath);
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
            return size;
        }

        void RecordStartup(LoggingSettings loggingSettings, EndpointConfiguration endpointConfiguration, IPersistenceConfiguration persistenceConfiguration)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(Bootstrapper).Assembly.Location).ProductVersion;
            var dbPath = SettingsReader<string>.Read("DbPath", null);
            var dataSize = DataSize(dbPath);
            var folderSize = FolderSize(dbPath);

            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Audit Version:       {version}
Audit Retention Period:             {settings.AuditRetentionPeriod}
Forwarding Audit Messages:          {settings.ForwardAuditMessages}
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
ServiceControl Logging Level:       {loggingSettings.LoggingLevel}
RavenDB Logging Level:              {loggingSettings.RavenDBLogLevel}
Transport Customization:            {settings.TransportType},
Persistence:                        {persistenceConfiguration.Name}
-------------------------------------------------------------";

            var logger = LogManager.GetLogger(typeof(Bootstrapper));
            logger.Info(startupMessage);
            endpointConfiguration.GetSettings().AddStartupDiagnosticsSection("Startup", new
            {
                Settings = new
                {
                    settings.ApiUrl,
                    settings.AuditLogQueue,
                    settings.AuditQueue,
                    settings.DataSpaceRemainingThreshold,
                    settings.ForwardAuditMessages,
                    settings.HttpDefaultConnectionLimit,
                    settings.IngestAuditMessages,
                    settings.MaxBodySizeToStore,
                    settings.MaximumConcurrencyLevel,
                    settings.Port,
                    settings.SkipQueueCreation,
                    settings.EnableFullTextSearchOnBodies,
                    settings.TransportType
                },
                LoggingSettings = loggingSettings
            });
        }

        EndpointConfiguration configuration;
        LoggingSettings loggingSettings;
        Action<ICriticalErrorContext> onCriticalError;
        Settings.Settings settings;
        TransportSettings transportSettings;
        TransportCustomization transportCustomization;
    }
}
