namespace ServiceControl.Audit.Infrastructure
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Web.Http.Controllers;
    using Auditing;
    using Auditing.MessagesView;
    using Autofac;
    using Autofac.Core.Activators.Reflection;
    using Autofac.Extensions.DependencyInjection;
    using Autofac.Features.ResolveAnything;
    using ByteSizeLib;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Monitoring;
    using NLog.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using OWIN;
    using Raven.Client.Embedded;
    using RavenDB;
    using ServiceControl.Infrastructure.Metrics;
    using Settings;
    using Transports;
    using WebApi;

    class Bootstrapper
    {
        public IHostBuilder HostBuilder { get; private set; }

        public IContainer Container { get; private set; }

        public AuditIngestionComponent AuditIngestionComponent { get; private set; }

        // Windows Service
        public Bootstrapper(Action<ICriticalErrorContext> onCriticalError, Settings.Settings settings, EndpointConfiguration configuration, LoggingSettings loggingSettings, Action<ContainerBuilder> additionalRegistrationActions = null, bool isRunningInAcceptanceTests = false)
        {
            this.onCriticalError = onCriticalError;
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.loggingSettings = loggingSettings;
            this.additionalRegistrationActions = additionalRegistrationActions;
            this.isRunningInAcceptanceTests = isRunningInAcceptanceTests;
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
                .UseServiceProviderFactory(new AutofacServiceProviderFactory(containerBuilder =>
                {
                    containerBuilder.RegisterSource(new AnyConcreteTypeNotAlreadyRegisteredSource(type =>
                        type.Assembly == typeof(Bootstrapper).Assembly && type.GetInterfaces().Any() == false));


                    containerBuilder.RegisterInstance(transportSettings).SingleInstance();

                    var rawEndpointFactory = new RawEndpointFactory(settings, transportSettings, transportCustomization);
                    containerBuilder.RegisterInstance(rawEndpointFactory).AsSelf();

                    RegisterInternalWebApiControllers(containerBuilder);

                    additionalRegistrationActions?.Invoke(containerBuilder);

                    containerBuilder.RegisterInstance(new Metrics { Enabled = settings.PrintMetrics }).ExternallyOwned();

                    containerBuilder.RegisterInstance(loggingSettings);
                    containerBuilder.RegisterInstance(settings);
                    containerBuilder.RegisterModule<ApisModule>();
                    containerBuilder.RegisterType<EndpointInstanceMonitoring>().SingleInstance();
                    containerBuilder.RegisterType<AuditIngestionComponent>().SingleInstance();

                    containerBuilder.RegisterBuildCallback(c =>
                    {
                        Container = c;
                        AuditIngestionComponent = c.Resolve<AuditIngestionComponent>();
                    });

                    containerBuilder.Register(cc => new Startup(Container));

                }))
                .ConfigureServices(services =>
                {
                    if (isRunningInAcceptanceTests == false)
                    {
                        services.AddHostedService<WebApiHostedService>();
                    }

                    services.AddHostedService<MetricsReporterHostedService>();
                })
                .UseEmbeddedRavenDb(context =>
                {
                    var documentStore = new EmbeddableDocumentStore();

                    RavenBootstrapper.ConfigureAndStart(documentStore, settings);

                    return documentStore;
                })
                .UseNServiceBus(context =>
                {
                    NServiceBusFactory.Configure(settings, transportCustomization, transportSettings, loggingSettings, onCriticalError, configuration, false);

                    return configuration;
                })
                .ConfigureLogging(builder =>
                {
                    builder.ClearProviders();
                    //HINT: configuration used by NLog comes from LoggingConfigurator.cs
                    builder.AddNLog();
                });
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

        static void RegisterInternalWebApiControllers(ContainerBuilder containerBuilder)
        {
            var controllerTypes = Assembly.GetExecutingAssembly().DefinedTypes
                .Where(t => typeof(IHttpController).IsAssignableFrom(t) && t.Name.EndsWith("Controller", StringComparison.Ordinal));

            foreach (var controllerType in controllerTypes)
            {
                containerBuilder.RegisterType(controllerType).FindConstructorsWith(new AllConstructorFinder());
            }
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

        readonly Action<ContainerBuilder> additionalRegistrationActions;
        readonly bool isRunningInAcceptanceTests;
        EndpointConfiguration configuration;
        LoggingSettings loggingSettings;
        Action<ICriticalErrorContext> onCriticalError;
        Settings.Settings settings;
        //BusInstance bus;
        TransportSettings transportSettings;
        TransportCustomization transportCustomization;

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
