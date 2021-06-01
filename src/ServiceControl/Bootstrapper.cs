namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using Autofac;
    using Autofac.Core.Activators.Reflection;
    using Autofac.Extensions.DependencyInjection;
    using ByteSizeLib;
    using global::ServiceControl.Infrastructure.BackgroundTasks;
    using global::ServiceControl.Infrastructure.DomainEvents;
    using global::ServiceControl.Infrastructure.Metrics;
    using global::ServiceControl.Infrastructure.RavenDB;
    using global::ServiceControl.Infrastructure.SignalR;
    using global::ServiceControl.Infrastructure.WebApi;
    using global::ServiceControl.Monitoring;
    using global::ServiceControl.Operations;
    using global::ServiceControl.Recoverability;
    using global::ServiceControl.Transports;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;

    class Bootstrapper
    {
        // Windows Service
        public Bootstrapper(Settings settings, EndpointConfiguration configuration, LoggingSettings loggingSettings, Action<ContainerBuilder> additionalRegistrationActions = null, bool isRunningInAcceptanceTests = false)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggingSettings = loggingSettings;
            if (additionalRegistrationActions != null)
            {
                registrationActions.Add(additionalRegistrationActions);
            }
            this.settings = settings;
            this.isRunningInAcceptanceTests = isRunningInAcceptanceTests;

            ApiAssemblies = new List<Assembly>(settings.Components.Select(ci => ci.GetAssembly()))
            {
                Assembly.GetExecutingAssembly()
            };

            CreateHost();
        }

        public Startup Startup { get; }

        public Func<HttpClient> HttpClientFactory { get; set; } = () =>
        {
            if (httpClient == null)
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }

            return httpClient;
        };

        public IHostBuilder HostBuilder { get; private set; }
        public List<Assembly> ApiAssemblies { get; }

        void CreateHost()
        {
            RecordStartup(loggingSettings, configuration);

            if (!string.IsNullOrWhiteSpace(settings.LicenseFileText))
            {
                configuration.License(settings.LicenseFileText);
            }

            // .NET default limit is 10. RavenDB in conjunction with transports that use HTTP exceeds that limit.
            ServicePointManager.DefaultConnectionLimit = settings.HttpDefaultConnectionLimit;

            transportCustomization = settings.LoadTransportCustomization();
            transportSettings = MapSettings(settings);

            HostBuilder = new HostBuilder();
            HostBuilder
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    //HINT: configuration used by NLog comes from LoggingConfigurator.cs
                    builder.AddNLog();
                })
                .ConfigureServices(services
                    => services.Configure<HostOptions>(options
                        => options.ShutdownTimeout = TimeSpan.FromSeconds(30)))
                .UseMetrics(settings.PrintMetrics)
                .UseEmbeddedRavenDb(context =>
                {
                    var documentStore = new EmbeddableDocumentStore();

                    RavenBootstrapper.ConfigureAndStart(documentStore, settings);

                    return documentStore;
                }, settings.StoreInitializer)
                .UseNServiceBus(context =>
                {
                    NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings,
                        configuration, isRunningInAcceptanceTests);

                    return configuration;
                })
                .UseWebApi(registrationActions, ApiAssemblies, settings.RootUrl, !isRunningInAcceptanceTests)
                .UseAsyncTimer()
                ;

            HostBuilder.UseServiceProviderFactory(new AutofacServiceProviderFactory(containerBuilder =>
            {
                var domainEvents = new DomainEvents();
                containerBuilder.RegisterInstance(domainEvents).As<IDomainEvents>();
                containerBuilder.RegisterBuildCallback(c =>
                {
                    domainEvents.SetContainer(c);
                });

                containerBuilder.RegisterInstance(transportSettings).SingleInstance();

                var rawEndpointFactory = new RawEndpointFactory(settings, transportSettings, transportCustomization);
                containerBuilder.RegisterInstance(rawEndpointFactory).AsSelf();

                containerBuilder.RegisterType<MessageStreamerConnection>().SingleInstance();
                containerBuilder.RegisterInstance(loggingSettings);
                containerBuilder.RegisterInstance(settings);
                containerBuilder.Register(c => HttpClientFactory);

                containerBuilder.RegisterType<EndpointInstanceMonitoring>().SingleInstance();
                containerBuilder.RegisterType<MonitoringDataPersister>().AsImplementedInterfaces().AsSelf().SingleInstance();
                containerBuilder.RegisterType<ErrorIngestionComponent>().SingleInstance();

                registrationActions.ForEach(ra => ra.Invoke(containerBuilder));

            }));
        }

        TransportSettings MapSettings(Settings settings)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = settings.ServiceName,
                ConnectionString = settings.TransportConnectionString,
                MaxConcurrency = settings.MaximumConcurrencyLevel,
                RunCustomChecks = true
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
            catch
            {
                return -1;
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
ServiceControl Version:             {version}
Audit Retention Period (optional):  {settings.AuditRetentionPeriod}
Error Retention Period:             {settings.ErrorRetentionPeriod}
Ingest Error Messages:              {settings.IngestErrorMessages}
Forwarding Error Messages:          {settings.ForwardErrorMessages}
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
                    settings.DatabaseMaintenancePort,
                    settings.ErrorLogQueue,
                    settings.DataSpaceRemainingThreshold,
                    settings.DisableRavenDBPerformanceCounters,
                    settings.DbPath,
                    settings.ErrorQueue,
                    settings.ForwardErrorMessages,
                    settings.HttpDefaultConnectionLimit,
                    settings.IngestErrorMessages,
                    settings.MaximumConcurrencyLevel,
                    settings.Port,
                    settings.ProcessRetryBatchesFrequency,
                    settings.NotificationsFilter,
                    settings.RemoteInstances,
                    settings.RetryHistoryDepth,
                    settings.RunInMemory,
                    settings.SkipQueueCreation,
                    settings.EnableFullTextSearchOnBodies,
                    settings.TransportCustomizationType,
                    settings.AllowMessageEditing
                },
                LoggingSettings = loggingSettings
            });
        }

        readonly List<Action<ContainerBuilder>> registrationActions = new List<Action<ContainerBuilder>>();
        EndpointConfiguration configuration;
        LoggingSettings loggingSettings;
        Settings settings;
        TransportSettings transportSettings;
        TransportCustomization transportCustomization;
        static HttpClient httpClient;
        readonly bool isRunningInAcceptanceTests;

        class AllConstructorFinder : IConstructorFinder
        {
            public ConstructorInfo[] FindConstructors(Type targetType)
            {
                var result = Cache.GetOrAdd(targetType, t => t.GetTypeInfo().DeclaredConstructors.ToArray());

                return result.Length > 0 ? result : throw new Exception($"No constructor found for type {targetType.FullName}");
            }

            static readonly ConcurrentDictionary<Type, ConstructorInfo[]> Cache = new ConcurrentDictionary<Type, ConstructorInfo[]>();
        }
    }
}
