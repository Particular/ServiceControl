namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Auditing;
    using Infrastructure;
    using Infrastructure.Settings;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;

    public class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse,
            AcceptanceTestStorageConfiguration persistenceToUse, Action<Settings> setSettings,
            Action<EndpointConfiguration> customConfiguration,
            Action<IDictionary<string, string>> setStorageConfiguration,
            Action<IHostApplicationBuilder> hostBuilderCustomization)
        {
            this.transportToUse = transportToUse;
            this.persistenceToUse = persistenceToUse;
            this.customConfiguration = customConfiguration;
            this.setStorageConfiguration = setStorageConfiguration;
            this.setSettings = setSettings;
            this.hostBuilderCustomization = hostBuilderCustomization;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public HttpClient HttpClient { get; private set; }
        public JsonSerializerOptions SerializerOptions => Infrastructure.WebApi.SerializerOptions.Default;
        public IServiceProvider ServiceProvider { get; private set; }
        public TestServer InstanceTestServer { get; private set; }
        public Task Initialize(RunDescriptor run) => InitializeServiceControl(run.ScenarioContext);

        async Task InitializeServiceControl(ScenarioContext context)
        {
            settings = new Settings(instanceName, transportToUse.TypeName, persistenceToUse.PersistenceType)
            {
                TransportConnectionString = transportToUse.ConnectionString,
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                ExposeApi = false,
                ServiceControlQueueAddress = "SHOULDNOTBEUSED",
                MessageFilter = messageContext =>
                {
                    var id = messageContext.NativeMessageId;
                    var headers = messageContext.Headers;

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

            var assemblyScanner = configuration.AssemblyScanner();
            assemblyScanner.ExcludeAssemblies(Path.GetFileName(typeof(ServiceControlComponentRunner).Assembly.Location));

            customConfiguration(configuration);

            using (new DiagnosticTimer($"Starting ServiceControl {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new LoggingSettings(settings.ServiceName, defaultLevel: NLog.LogLevel.Debug, logPath: logPath);
                var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    // Force the DI container to run the dependency resolution check to verify all dependencies can be resolved
                    EnvironmentName = Environments.Development
                });
                hostBuilder.AddServiceControlAudit((criticalErrorContext, cancellationToken) =>
                {
                    var logitem = new ScenarioContext.LogItem
                    {
                        Endpoint = settings.ServiceName,
                        Level = NServiceBus.Logging.LogLevel.Fatal,
                        LoggerName = $"{settings.ServiceName}.CriticalError",
                        Message = $"{criticalErrorContext.Error}{Environment.NewLine}{criticalErrorContext.Exception}"
                    };
                    context.Logs.Enqueue(logitem);
                    return criticalErrorContext.Stop(cancellationToken);
                }, settings, configuration, loggingSettings);

                // Do not register additional test controllers or hosted services here. Instead, in the test that needs them, use (for example):
                // CustomizeHostBuilder = builder => builder.ConfigureServices((hostContext, services) => services.AddHostedService<SetupNotificationSettings>());
                hostBuilder.Logging.AddScenarioContextLogging();

                // TODO: the following four lines could go into a AddServiceControlAuditTesting() extension
                hostBuilder.WebHost.UseTestServer(options => options.BaseAddress = new Uri(settings.RootUrl));
                // This facilitates receiving the test server anywhere where DI is available
                hostBuilder.Services.AddSingleton(provider => (TestServer)provider.GetRequiredService<IServer>());

                // By default ASP.NET Core uses entry point assembly to discover controllers from. When running
                // inside a test runner the runner exe becomes the entry point which obviously has no controllers in it ;)
                // so we are explicitly registering all necessary application parts.
                var addControllers = hostBuilder.Services.AddControllers();
                addControllers.AddApplicationPart(typeof(WebApiHostBuilderExtensions).Assembly);
                addControllers.AddApplicationPart(typeof(FailedAuditsController).Assembly);

                hostBuilderCustomization(hostBuilder);

                host = hostBuilder.Build();
                host.UseServiceControlAudit();
                await host.StartAsync();
                // TODO We can probably remove this by switching over to the hostBuilderCustomization
                ServiceProvider = host.Services;
                InstanceTestServer = host.GetTestServer();
                HttpClient = InstanceTestServer.CreateClient();
            }
        }

        public override async Task Stop(CancellationToken cancellationToken = default)
        {
            using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
            {
                await host.StopAsync(cancellationToken);
                HttpClient.Dispose();
                await host.DisposeAsync();
            }
        }

        ITransportIntegration transportToUse;
        AcceptanceTestStorageConfiguration persistenceToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        Action<IDictionary<string, string>> setStorageConfiguration;
        Action<IHostApplicationBuilder> hostBuilderCustomization;
        string instanceName = Settings.DEFAULT_SERVICE_NAME;
        WebApplication host;
        Settings settings;
    }
}