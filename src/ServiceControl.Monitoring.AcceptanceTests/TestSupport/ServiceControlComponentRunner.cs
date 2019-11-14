namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using Microsoft.Owin.Builder;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using ServiceControl.Monitoring;
    using ServiceControl.Monitoring.Infrastructure;
    using ServiceControl.Monitoring.Infrastructure.WebApi;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration)
        {
            this.transportToUse = transportToUse;
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";


        public HttpClient HttpClient { get; set; }
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializerSettings.CreateDefault();
        public Settings Settings { get; set; }
        public OwinHttpMessageHandler Handler { get; set; }
        public BusInstance Bus { get; set; }

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

            ConfigurationManager.AppSettings.Set("Monitoring/TransportType", transportToUse.TypeName);

            var settings = Settings.Load(new SettingsReader(ConfigurationManager.AppSettings));
            settings.EndpointName = instanceName;
            settings.HttpPort = instancePort.ToString();
            settings.TransportType = transportToUse.TypeName;
            settings.ConnectionString = transportToUse.ConnectionString;
            settings.HttpHostName = "localhost";
            //MaximumConcurrencyLevel = 2,
            //HttpDefaultConnectionLimit = int.MaxValue,
            //RunInMemory = true,
            settings.OnMessage = (id, headers, body, @continue) =>
                {
                    var log = LogManager.GetLogger<ServiceControlComponentRunner>();
                    headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                    log.Debug($"OnMessage for message '{id}'({originalMessageId ?? string.Empty}).");

                    //Do not filter out CC, SA and HB messages as they can't be stamped
                    if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                        && messageTypes.StartsWith("ServiceControl."))
                    {
                        return @continue();
                    }

                    //Do not filter out subscribe messages as they can't be stamped
                    if (headers.TryGetValue(Headers.MessageIntent, out var intent)
                        && intent == MessageIntentEnum.Subscribe.ToString())
                    {
                        return @continue();
                    }

                    var currentSession = context.TestRunId.ToString();
                    if (!headers.TryGetValue("SC.SessionID", out var session) || session != currentSession)
                    {
                        log.Debug($"Discarding message '{id}'({originalMessageId ?? string.Empty}) because it's session id is '{session}' instead of '{currentSession}'.");
                        return Task.FromResult(0);
                    }

                    return @continue();
                };

            setSettings(settings);
            Settings = settings;
            var configuration = new EndpointConfiguration(instanceName);
            configuration.EnableInstallers();

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

            using (new DiagnosticTimer($"Initializing Bootstrapper for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                //TODO: move in the logging settings
                /*var loggingSettings = new LoggingSettings(settings.ServiceName, logPath: logPath);
                
                bootstrapper = new Bootstrapper(configuration, loggingSettings, builder => { builder.RegisterType<FailedAuditsController>().FindConstructorsWith(t => t.GetTypeInfo().DeclaredConstructors.ToArray()); }); */

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
                }, settings, configuration);
                
                //bootstrapper.HttpClientFactory = HttpClientFactory;
            }

            using (new DiagnosticTimer($"Initializing AppBuilder for {instanceName}"))
            {
                var app = new AppBuilder();
                bootstrapper.Startup.Configuration(app);
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

            using (new DiagnosticTimer($"Creating and starting Bus for {instanceName}"))
            {
                Bus = await bootstrapper.Start().ConfigureAwait(false);
            }
        }

        public override async Task Stop()
        {
            using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
            {
                await bootstrapper.Stop().ConfigureAwait(false);
                HttpClient.Dispose();
                Handler.Dispose();
            }

            bootstrapper = null;
            Bus = null;
            HttpClient = null;
            Handler = null;
        }

        HttpClient HttpClientFactory()
        {
            var httpClient = new HttpClient(Handler);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }

        Bootstrapper bootstrapper;
        ITransportIntegration transportToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        string instanceName = Settings.DEFAULT_ENDPOINT_NAME;
    }
}