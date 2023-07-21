namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.DomainEvents;
    using Infrastructure.WebApi;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Owin.Builder;
    using Newtonsoft.Json;
    using NLog;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using Particular.ServiceControl;
    using Recoverability.MessageFailures;
    using ServiceBus.Management.Infrastructure.OWIN;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTests.Monitoring;
    using ServiceControl.AcceptanceTests.Monitoring.InternalCustomChecks;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, DataStoreConfiguration dataStoreToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IHostBuilder> hostBuilderCustomization)
        {
            this.transportToUse = transportToUse;
            this.dataStoreToUse = dataStoreToUse;
            this.customConfiguration = customConfiguration;
            this.hostBuilderCustomization = hostBuilderCustomization;
            this.setSettings = setSettings;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public Settings Settings { get; set; }
        public OwinHttpMessageHandler Handler { get; set; }
        public HttpClient HttpClient { get; set; }
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializerSettings.CreateDefault();
        public string Port => Settings.Port.ToString();
        public IDomainEvents DomainEvents { get; set; }

        public Task Initialize(RunDescriptor run)
        {
            return InitializeServiceControl(run.ScenarioContext);
        }

        static int FindAvailablePort(int startPort)
        {
            var activeTcpListeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveTcpListeners();

            for (var port = startPort; port < startPort + 1024; port++)
            {
                var portCopy = port;
                if (activeTcpListeners.All(endPoint => endPoint.Port != portCopy))
                {
                    return port;
                }
            }

            return startPort;
        }

        async Task InitializeServiceControl(ScenarioContext context)
        {
            var instancePort = FindAvailablePort(33333);
            var maintenancePort = FindAvailablePort(instancePort + 1);

            ConfigurationManager.AppSettings.Set("ServiceControl/TransportType", transportToUse.TypeName);
            ConfigurationManager.AppSettings.Set("ServiceControl/SqlStorageConnectionString", dataStoreToUse.ConnectionString);

            var settings = new Settings(instanceName)
            {
                DataStoreType = (DataStoreType)Enum.Parse(typeof(DataStoreType), dataStoreToUse.DataStoreTypeName),
                Port = instancePort,
                DatabaseMaintenancePort = maintenancePort,
                DbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                ForwardErrorMessages = false,
                TransportType = transportToUse.TypeName,
                TransportConnectionString = transportToUse.ConnectionString,
                ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(2),
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                RunInMemory = true,
                DisableHealthChecks = true,
                ExposeApi = true,
                MessageFilter = messageContext =>
                {
                    var headers = messageContext.Headers;
                    var id = messageContext.MessageId;
                    var log = NServiceBus.Logging.LogManager.GetLogger<ServiceControlComponentRunner>();
                    headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                    log.Debug($"OnMessage for message '{id}'({originalMessageId ?? string.Empty}).");

                    //Do not filter out CC, SA and HB messages as they can't be stamped
                    if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                        && (messageTypes.StartsWith("ServiceControl.Contracts") || messageTypes.StartsWith("ServiceControl.EndpointPlugin")))
                    {
                        return false;
                    }

                    //Do not filter out subscribe messages as they can't be stamped
                    if (headers.TryGetValue(Headers.MessageIntent, out var intent)
                        && intent == MessageIntentEnum.Subscribe.ToString())
                    {
                        return false;
                    }

                    var currentSession = context.TestRunId.ToString();
                    if (!headers.TryGetValue("SC.SessionID", out var session) || session != currentSession)
                    {
                        log.Debug($"Discarding message '{id}'({originalMessageId ?? string.Empty}) because it's session id is '{session}' instead of '{currentSession}'.");
                        return true;
                    }

                    return false;
                }
            };

            setSettings(settings);
            Settings = settings;
            var configuration = new EndpointConfiguration(instanceName);

            configuration.GetSettings().Set("SC.ScenarioContext", context);
            configuration.GetSettings().Set(context);

            // This is a hack to ensure ServiceControl picks the correct type for the messages that come from plugins otherwise we pick the type from the plugins assembly and that is not the type we want, we need to pick the type from ServiceControl assembly.
            // This is needed because we no longer use the AppDomain separation.
            configuration.RegisterComponents(r => { configuration.GetSettings().Set("SC.ConfigureComponent", r); });

            configuration.RegisterComponents(r =>
            {
                r.RegisterSingleton(context.GetType(), context);
                r.RegisterSingleton(typeof(ScenarioContext), context);
            });

            configuration.Pipeline.Register<TraceIncomingBehavior.Registration>();
            configuration.Pipeline.Register<TraceOutgoingBehavior.Registration>();
            configuration.Pipeline.Register(new StampDispatchBehavior(context), "Stamps outgoing messages with session ID");
            configuration.Pipeline.Register(new DiscardMessagesBehavior(context), "Discards messages based on session ID");

            configuration.AssemblyScanner().ExcludeAssemblies(typeof(ServiceControlComponentRunner).Assembly.GetName().Name);

            customConfiguration(configuration);

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {
                var setupBootstrapper = new SetupBootstrapper(settings);
                await setupBootstrapper.Run(null);
            }

            using (new DiagnosticTimer($"Creating and starting Bus for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new LoggingSettings(settings.ServiceName, defaultLevel: LogLevel.Debug, logPath: logPath);
                bootstrapper = new Bootstrapper(settings, configuration, loggingSettings)
                {
                    HttpClientFactory = HttpClientFactory
                };

                bootstrapper.HostBuilder
                    .ConfigureLogging((c, b) => b.AddScenarioContextLogging())
                    .ConfigureServices(serviceCollection =>
                {
                    serviceCollection.AddScoped<CriticalErrorTriggerController>();
                    serviceCollection.AddScoped<KnownEndpointPersistenceQueryController>();
                    serviceCollection.AddScoped<FailedErrorsController>();
                    serviceCollection.AddScoped<FailedMessageRetriesController>();
                });

                hostBuilderCustomization(bootstrapper.HostBuilder);

                host = bootstrapper.HostBuilder.Build();
                await host.StartAsync();
                DomainEvents = host.Services.GetService<IDomainEvents>();
            }

            using (new DiagnosticTimer($"Initializing WebApi for {instanceName}"))
            {
                var app = new AppBuilder();
                var startup = new Startup(host.Services, bootstrapper.ApiAssemblies);
                startup.Configuration(app, typeof(FailedErrorsController).Assembly);
                var appFunc = app.Build();

                Handler = new OwinHttpMessageHandler(appFunc)
                {
                    UseCookies = false,
                    AllowAutoRedirect = false
                };

                var httpClient = new HttpClient(Handler);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpClient = httpClient;
            }
        }

        public override async Task Stop()
        {
            using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
            {
                await host.StopAsync();
                HttpClient.Dispose();
                Handler.Dispose();
                DirectoryDeleter.Delete(Settings.DbPath);
            }

            bootstrapper = null;
            HttpClient = null;
            Handler = null;
        }

        HttpClient HttpClientFactory()
        {
            var httpClient = new HttpClient(Handler);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }

        IHost host;
        Bootstrapper bootstrapper;
        ITransportIntegration transportToUse;
        DataStoreConfiguration dataStoreToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        Action<IHostBuilder> hostBuilderCustomization;
        string instanceName = Settings.DEFAULT_SERVICE_NAME;
    }
}