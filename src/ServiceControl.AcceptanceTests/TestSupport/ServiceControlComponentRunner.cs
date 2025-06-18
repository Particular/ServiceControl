namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.Loader;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Hosting.Commands;
    using Infrastructure.DomainEvents;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using Particular.ServiceControl;
    using Particular.ServiceControl.Hosting;
    using RavenDB.Shared;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;

    public class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
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
        public JsonSerializerOptions SerializerOptions => Infrastructure.WebApi.SerializerOptions.Default;
        public Func<HttpMessageHandler> HttpMessageHandlerFactory { get; private set; }
        public IDomainEvents DomainEvents { get; private set; }

        public Task Initialize(RunDescriptor run) => InitializeServiceControl(run.ScenarioContext);

        async Task InitializeServiceControl(ScenarioContext context)
        {
            var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(logPath);
            var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace, defaultLevel: LogLevel.Debug, logPath: logPath);

            var settings = new Settings(transportToUse.TypeName, persistenceToUse.PersistenceType, loggingSettings, forwardErrorMessages: false, errorRetentionPeriod: TimeSpan.FromDays(10))
            {
                InstanceName = instanceName,
                AllowMessageEditing = true,
                ForwardErrorMessages = false,
                TransportConnectionString = transportToUse.ConnectionString,
                ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(2),
                MaximumConcurrencyLevel = 2,
                DisableHealthChecks = true,
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
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
            };

            await persistenceToUse.CustomizeSettings(settings);

            setSettings(settings);
            Settings = settings;

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {
                var setupCommand = new SetupCommand();
                await setupCommand.Execute(new HostArguments([]), settings);
            }

            var configuration = new EndpointConfiguration(instanceName);
            configuration.CustomizeServiceControlEndpointTesting(context);

            customConfiguration(configuration);

            using (new DiagnosticTimer($"Starting ServiceControl {instanceName}"))
            {
                var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    // Force the DI container to run the dependency resolution check to verify all dependencies can be resolved
                    EnvironmentName = Environments.Development
                });
                hostBuilder.AddServiceControl(settings, configuration);
                hostBuilder.AddServiceControlApi();

                hostBuilder.AddServiceControlTesting(settings);

                hostBuilderCustomization(hostBuilder);

                host = hostBuilder.Build();
                host.UseServiceControl();
                await host.StartAsync();
                DomainEvents = host.Services.GetRequiredService<IDomainEvents>();
                // Bring this back and look into the base address of the client
                HttpClient = host.GetTestServer().CreateClient();
                HttpMessageHandlerFactory = () => host.GetTestServer().CreateHandler();
            }
        }

        public override async Task Stop(CancellationToken cancellationToken = default)
        {
            using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
            {
                await host.StopAsync(cancellationToken);
                HttpClient.Dispose();
                await host.DisposeAsync();
                await persistenceToUse.Cleanup();
            }
        }

        WebApplication host;
        readonly ITransportIntegration transportToUse;
        readonly AcceptanceTestStorageConfiguration persistenceToUse;
        readonly Action<Settings> setSettings;
        readonly Action<EndpointConfiguration> customConfiguration;
        readonly Action<IHostApplicationBuilder> hostBuilderCustomization;
        readonly string instanceName = Settings.DEFAULT_INSTANCE_NAME;
    }
}