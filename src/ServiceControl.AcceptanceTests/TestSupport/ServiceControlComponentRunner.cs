namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.DomainEvents;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Newtonsoft.Json;
    using NLog;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestHelper;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, AcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration, Action<IHostApplicationBuilder> hostBuilderCustomization)
        {
            this.transportToUse = transportToUse;
            this.persistenceToUse = persistenceToUse;
            this.customConfiguration = customConfiguration;
            this.hostBuilderCustomization = hostBuilderCustomization;
            this.setSettings = setSettings;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public Settings Settings { get; private set; }
        public HttpClient HttpClient { get; private set; }
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializerSettings.CreateDefault();
        public string Port => Settings.Port.ToString();
        public Func<HttpMessageHandler, HttpMessageHandler> HttpMessageHandlerFactory { get; private set; }
        public IDomainEvents DomainEvents { get; private set; }

        public Task Initialize(RunDescriptor run) => InitializeServiceControl(run.ScenarioContext);

        async Task InitializeServiceControl(ScenarioContext context)
        {
            var instancePort = PortUtility.FindAvailablePort(33333);

            var settings = new Settings(instanceName, transportToUse.TypeName, persistenceToUse.PersistenceType, forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))
            {
                AllowMessageEditing = true,
                Port = instancePort,
                ForwardErrorMessages = false,
                TransportConnectionString = transportToUse.ConnectionString,
                ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(2),
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                DisableHealthChecks = true,
                ExposeApi = true,
                MessageFilter = messageContext =>
                {
                    var headers = messageContext.Headers;
                    var id = messageContext.NativeMessageId;
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
                        && intent == MessageIntent.Subscribe.ToString())
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
                },
            };

            persistenceToUse.CustomizeSettings(settings);

            setSettings(settings);
            Settings = settings;

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {

                var setupBootstrapper = new SetupBootstrapper(settings);
                await setupBootstrapper.Run();
            }

            var configuration = new EndpointConfiguration(instanceName);

            configuration.GetSettings().Set("SC.ScenarioContext", context);
            configuration.GetSettings().Set(context);

            configuration.RegisterComponents(r =>
            {
                r.AddSingleton(context.GetType(), context);
                r.AddSingleton(typeof(ScenarioContext), context);
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
                await setupBootstrapper.Run();
            }

            using (new DiagnosticTimer($"Starting ServiceControl {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new LoggingSettings(settings.ServiceName, defaultLevel: LogLevel.Debug, logPath: logPath);
                var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions()
                {
                    // TODO this is needed because for some reason we're registering all concrete types for no reason
                    EnvironmentName = Environments.Production
                });
                hostBuilder.AddServiceControl(settings, configuration, loggingSettings);

                // Do not register additional test controllers or hosted services here. Instead, in the test that needs them, use (for example):
                // CustomizeHostBuilder = builder => builder.ConfigureServices((hostContext, services) => services.AddHostedService<SetupNotificationSettings>());
                hostBuilder.Logging.AddScenarioContextLogging();

                // TODO: the following four lines could go into a AddServiceControlTesting() extension
                hostBuilder.WebHost.UseTestServer();
                // This facilitates receiving the test server anywhere where DI is available
                hostBuilder.Services.AddSingleton(provider => (TestServer)provider.GetRequiredService<IServer>());

                // By default ASP.NET Core uses entry point assembly to discover controllers from. When running
                // inside a test runner the runner exe becomes the entry point which obviously has no controllers in it ;)
                // so we are explicitly registering all necessary application parts.
                var addControllers = hostBuilder.Services.AddControllers();
                addControllers.AddApplicationPart(typeof(WebApiHostBuilderExtensions).Assembly);
                addControllers.AddApplicationPart(typeof(AcceptanceTest).Assembly);

                hostBuilder.Services.ConfigureHttpClientDefaults(b =>
                {
                    b.ConfigurePrimaryHttpMessageHandler(p => p.GetRequiredService<TestServer>().CreateHandler());
                    // TODO: See if this is even still needed or should be moved to the named instance for the tests only
                    b.ConfigureHttpClient(httpClient => httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")));
                });

                hostBuilderCustomization(hostBuilder);

                host = hostBuilder.Build();
                host.UseServiceControl();
                await host.StartServiceControl();
                DomainEvents = host.Services.GetRequiredService<IDomainEvents>();
                // Bring this back and look into the base address of the client
                HttpClient = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(instanceName);
                HttpMessageHandlerFactory = _ => host.GetTestServer().CreateHandler();
            }
        }

        public override async Task Stop()
        {
            using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
            {
                await host.StopAsync();
                HttpClient.Dispose();
                await host.DisposeAsync();
                DirectoryDeleter.Delete(Settings.PersisterSpecificSettings.DatabasePath);
            }

            HttpClient = null;
        }

        WebApplication host;
        readonly ITransportIntegration transportToUse;
        readonly AcceptanceTestStorageConfiguration persistenceToUse;
        readonly Action<Settings> setSettings;
        readonly Action<EndpointConfiguration> customConfiguration;
        readonly Action<IHostApplicationBuilder> hostBuilderCustomization;
        readonly string instanceName = Settings.DEFAULT_SERVICE_NAME;
    }
}