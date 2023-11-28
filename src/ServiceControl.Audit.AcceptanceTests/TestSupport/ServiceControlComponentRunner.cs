namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Auditing;
    using Infrastructure;
    using Infrastructure.OWIN;
    using Infrastructure.Settings;
    using Infrastructure.WebApi;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Owin.Builder;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using TestHelper;
    using LogLevel = NServiceBus.Logging.LogLevel;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse,
            AcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings,
            Action<EndpointConfiguration> customConfiguration,
            Action<IDictionary<string, string>> setStorageConfiguration)
        {
            this.transportToUse = transportToUse;
            this.persistenceToUse = persistenceToUse;
            this.customConfiguration = customConfiguration;
            this.setStorageConfiguration = setStorageConfiguration;
            this.setSettings = setSettings;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public HttpClient HttpClient { get; private set; }
        public IServiceProvider ServiceProvider { get; private set; }
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializerSettings.CreateDefault();
        public string Port => settings.Port.ToString();

        public Task Initialize(RunDescriptor run)
        {
            return InitializeServiceControl(run.ScenarioContext);
        }

        async Task InitializeServiceControl(ScenarioContext context)
        {
            var instancePort = PortUtility.FindAvailablePort(33333);

            settings = new Settings(instanceName, transportToUse.TypeName, persistenceToUse.PersistenceType)
            {
                Port = instancePort,
                TransportConnectionString = transportToUse.ConnectionString,
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                ExposeApi = false,
                ServiceControlQueueAddress = "SHOULDNOTBEUSED",
                MessageFilter = messageContext =>
                {
                    var id = messageContext.MessageId;
                    var headers = messageContext.Headers;

                    var log = LogManager.GetLogger<ServiceControlComponentRunner>();
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

            var persisterSpecificSettings = await persistenceToUse.CustomizeSettings();

            setStorageConfiguration(persisterSpecificSettings);

            foreach (var persisterSpecificSetting in persisterSpecificSettings)
            {
                ConfigurationManager.AppSettings.Set($"ServiceControl.Audit/{persisterSpecificSetting.Key}", persisterSpecificSetting.Value);
            }

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {

                var setupBootstrapper = new SetupBootstrapper(settings);
                await setupBootstrapper.Run(null);
            }

            var configuration = new EndpointConfiguration(instanceName);

            configuration.GetSettings().Set("SC.ScenarioContext", context);
            configuration.GetSettings().Set(context);

            // This is a hack to ensure ServiceControl picks the correct type for the messages that come from plugins otherwise we pick the type from the plugins assembly and that is not the type we want, we need to pick the type from ServiceControl assembly.
            // This is needed because we no longer use the AppDomain separation.
            //configuration.RegisterComponents(r => { configuration.GetSettings().Set("SC.ConfigureComponent", r); });

            configuration.RegisterComponents(r =>
            {
                r.RegisterSingleton(context.GetType(), context);
                r.RegisterSingleton(typeof(ScenarioContext), context);
            });

            configuration.Pipeline.Register<TraceIncomingBehavior.Registration>();
            configuration.Pipeline.Register<TraceOutgoingBehavior.Registration>();
            configuration.Pipeline.Register(new StampDispatchBehavior(context), "Stamps outgoing messages with session ID");
            configuration.Pipeline.Register(new DiscardMessagesBehavior(context), "Discards messages based on session ID");
            configuration.Pipeline.Register(new InterceptMessagesDestinedToServiceControl(context), "Intercepts messages destined to ServiceControl");

            configuration.AssemblyScanner().ExcludeAssemblies(typeof(ServiceControlComponentRunner).Assembly.GetName().Name);

            customConfiguration(configuration);

            using (new DiagnosticTimer($"Starting host for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new LoggingSettings(settings.ServiceName, logPath: logPath);

                bootstrapper = new Bootstrapper(ctx =>
                {
                    var logitem = new ScenarioContext.LogItem
                    {
                        Endpoint = settings.ServiceName,
                        Level = LogLevel.Fatal,
                        LoggerName = $"{settings.ServiceName}.CriticalError",
                        Message = $"{ctx.Error}{Environment.NewLine}{ctx.Exception}"
                    };
                    context.Logs.Enqueue(logitem);
                    ctx.Stop().GetAwaiter().GetResult();
                },
                settings,
                configuration,
                loggingSettings);

                bootstrapper.HostBuilder
                    .ConfigureLogging((c, b) => b.AddScenarioContextLogging())
                    .ConfigureServices(s =>
                    {
                        s.AddTransient<FailedAuditsController>();
                    });

                host = await bootstrapper.HostBuilder.StartAsync();

                ServiceProvider = host.Services;
            }

            using (new DiagnosticTimer($"Initializing WebApi for {instanceName}"))
            {
                var app = new AppBuilder();
                var startup = new Startup(host.Services);
                startup.Configuration(app, typeof(FailedAuditsController).Assembly);
                var appFunc = app.Build();

                handler = new OwinHttpMessageHandler(appFunc)
                {
                    UseCookies = false,
                    AllowAutoRedirect = false
                };
                var httpClient = new HttpClient(handler);
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
                handler.Dispose();
                host.Dispose();
            }

            bootstrapper = null;
            HttpClient = null;
            handler = null;
        }

        Bootstrapper bootstrapper;
        ITransportIntegration transportToUse;
        AcceptanceTestStorageConfiguration persistenceToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        Action<IDictionary<string, string>> setStorageConfiguration;
        string instanceName = Settings.DEFAULT_SERVICE_NAME;
        IHost host;
        Settings settings;
        OwinHttpMessageHandler handler;
    }
}
