namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport
{
    using System;
    using System.Configuration;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting.Server;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using TestHelper;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration)
        {
            this.transportToUse = transportToUse;
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public HttpClient HttpClient { get; private set; }
        public JsonSerializerOptions SerializerOptions => Infrastructure.SerializerOptions.Default;
        public string Port => settings.HttpPort;

        public Task Initialize(RunDescriptor run) => InitializeServiceControl(run.ScenarioContext);

        async Task InitializeServiceControl(ScenarioContext context)
        {
            var instancePort = PortUtility.FindAvailablePort(33333);

            // TODO Check if we still need this
            ConfigurationManager.AppSettings.Set("Monitoring/TransportType", transportToUse.TypeName);

            settings = new Settings
            {
                EndpointName = instanceName,
                HttpPort = instancePort.ToString(),
                TransportType = transportToUse.TypeName,
                ConnectionString = transportToUse.ConnectionString,
                HttpHostName = "localhost",
                ExposeApi = true,
                OnMessage = (id, headers, body, @continue) =>
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
                            && intent == MessageIntent.Subscribe.ToString())
                    {
                        return @continue();
                    }

                    var currentSession = context.TestRunId.ToString();
                    if (!headers.TryGetValue("SC.SessionID", out var session) || session != currentSession)
                    {
                        log.Debug($"Discarding message '{id}'({originalMessageId ?? string.Empty}) because it's session id is '{session}' instead of '{currentSession}'.");
                        return Task.CompletedTask;
                    }

                    return @continue();
                }
            };

            setSettings(settings);

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

            using (new DiagnosticTimer($"Starting host for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    // Force the DI container to run the dependency resolution check to verify all dependencies can be resolved
                    EnvironmentName = Environments.Development
                });
                hostBuilder.AddServiceControlMonitoring((criticalErrorContext, cancellationToken) =>
                {
                    var logitem = new ScenarioContext.LogItem
                    {
                        Endpoint = settings.ServiceName,
                        Level = LogLevel.Fatal,
                        LoggerName = $"{settings.ServiceName}.CriticalError",
                        Message = $"{criticalErrorContext.Error}{Environment.NewLine}{criticalErrorContext.Exception}"
                    };
                    context.Logs.Enqueue(logitem);
                    return criticalErrorContext.Stop(cancellationToken);
                }, settings, configuration);

                hostBuilder.Logging.AddScenarioContextLogging();

                hostBuilder.WebHost.UseTestServer();
                // This facilitates receiving the test server anywhere where DI is available
                hostBuilder.Services.AddSingleton(provider => (TestServer)provider.GetRequiredService<IServer>());

                // By default ASP.NET Core uses entry point assembly to discover controllers from. When running
                // inside a test runner the runner exe becomes the entry point which obviously has no controllers in it ;)
                // so we are explicitly registering all necessary application parts.
                var addControllers = hostBuilder.Services.AddControllers();
                addControllers.AddApplicationPart(typeof(WebApplicationBuilderExtensions).Assembly);

                host = hostBuilder.Build();
                host.UseServiceControlMonitoring();
                await host.StartAsync();

                HttpClient = host.Services.GetRequiredService<TestServer>().CreateClient();
            }
        }

        public override async Task Stop()
        {
            using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
            {
                await host.StopAsync();
                HttpClient.Dispose();
                await host.DisposeAsync();
            }
        }

        ITransportIntegration transportToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        string instanceName = Settings.DEFAULT_ENDPOINT_NAME;
        WebApplication host;
        Settings settings;
    }
}