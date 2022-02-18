namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using Auditing;
    using Autofac.Extensions.DependencyInjection;
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
    using Raven.Client.Embedded;
    using RavenDB;
    using Settings;
    using SQL;
    using Transports;
    using WebApi;

    class Bootstrapper
    {
        public IHostBuilder HostBuilder { get; private set; }

        public Bootstrapper(Action<ICriticalErrorContext> onCriticalError, Settings.Settings settings, EndpointConfiguration configuration, LoggingSettings loggingSettings)
        {
            this.onCriticalError = onCriticalError;
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggingSettings = loggingSettings;
            this.settings = settings;

            CreateHost();
        }

        void CreateHost()
        {
            RecordStartup(loggingSettings, configuration);

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
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureServices(services =>
                {
                    services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));
                    services.AddSingleton(transportSettings);
                    var rawEndpointFactory = new RawEndpointFactory(settings, transportSettings, transportCustomization);
                    services.AddSingleton(rawEndpointFactory);
                    services.AddSingleton(loggingSettings);
                    services.AddSingleton(settings);
                    services.AddSingleton<EndpointInstanceMonitoring>();
                    services.AddSingleton<AuditIngestionComponent>();
                    services.AddSingleton<AuditIngestionCustomCheck.State>();

                    services.Configure<RavenStartup>(database =>
                    {
                        foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
                        {
                            database.AddIndexAssembly(indexAssembly);
                        }
                    });
                })
                .UseMetrics(settings.PrintMetrics)
                //.UseEmbeddedRavenDb(context =>
                //{
                //    var documentStore = new EmbeddableDocumentStore();

                //    RavenBootstrapper.Configure(documentStore, settings);

                //    return documentStore;
                //})
                .UseSqlDb(context =>
                {
                    var connectionString = Environment.GetEnvironmentVariable("PlatformSpike_AzureSQLConnectionString");

                    return (new SqlQueryStore(connectionString), new SqlWriteStore(connectionString), connectionString);
                })
                .UseNServiceBus(context =>
                {
                    NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings, onCriticalError, configuration, false);

                    return configuration;
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

        long DataSize()
        {
            var datafilePath = Path.Combine(settings.DbPath, "data");

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

        long FolderSize()
        {
            try
            {
                var dir = new DirectoryInfo(settings.DbPath);
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

        void RecordStartup(LoggingSettings loggingSettings, EndpointConfiguration endpointConfiguration)
        {
            var version = FileVersionInfo.GetVersionInfo(typeof(Bootstrapper).Assembly.Location).ProductVersion;
            var dataSize = DataSize();
            var folderSize = FolderSize();
            var startupMessage = $@"
-------------------------------------------------------------
ServiceControl Audit Version:       {version}
Audit Retention Period:             {settings.AuditRetentionPeriod}
Forwarding Audit Messages:          {settings.ForwardAuditMessages}
Database Size:                      {ByteSize.FromBytes(dataSize).ToString("#.##", CultureInfo.InvariantCulture)}
Database Folder Size:               {ByteSize.FromBytes(folderSize).ToString("#.##", CultureInfo.InvariantCulture)}
ServiceControl Logging Level:       {loggingSettings.LoggingLevel}
RavenDB Logging Level:              {loggingSettings.RavenDBLogLevel}
Selected Transport Customization:   {settings.TransportCustomizationType}
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
                    settings.DatabaseMaintenancePort,
                    settings.DisableRavenDBPerformanceCounters,
                    settings.DbPath,
                    settings.ForwardAuditMessages,
                    settings.HttpDefaultConnectionLimit,
                    settings.IngestAuditMessages,
                    settings.MaxBodySizeToStore,
                    settings.MaximumConcurrencyLevel,
                    settings.Port,
                    settings.RunInMemory,
                    settings.SkipQueueCreation,
                    settings.EnableFullTextSearchOnBodies,
                    settings.TransportCustomizationType
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
