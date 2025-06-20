namespace ServiceControl.Monitoring.AcceptanceTests.TestSupport
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Monitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControl.Infrastructure;

    class ServiceControlComponentRunner(
        ITransportIntegration transportToUse,
        Action<Settings> setSettings,
        Action<EndpointConfiguration> customConfiguration)
        : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public HttpClient HttpClient { get; private set; }
        public JsonSerializerOptions SerializerOptions => Infrastructure.SerializerOptions.Default;

        public Task Initialize(RunDescriptor run) => InitializeServiceControl(run.ScenarioContext);

        async Task InitializeServiceControl(ScenarioContext context)
        {
            LoggerUtil.ActiveLoggers = Loggers.Test;
            settings = new Settings(transportType: transportToUse.TypeName)
            {
                ConnectionString = transportToUse.ConnectionString,
                HttpHostName = "localhost",
                OnMessage = (id, headers, body, @continue) =>
                {
                    var logger = LoggerUtil.CreateStaticLogger<ServiceControlComponentRunner>();
                    headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                    logger.LogDebug("OnMessage for message '{MessageId}'({OriginalMessageId})", id, originalMessageId ?? string.Empty);

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
                        logger.LogDebug("Discarding message '{MessageId}'({OriginalMessageId}) because it's session id is '{SessionId}' instead of '{CurrentSessionId}'", id, originalMessageId ?? string.Empty, session, currentSession);
                        return Task.CompletedTask;
                    }

                    return @continue();
                }
            };

            setSettings(settings);

            using (new DiagnosticTimer($"Creating infrastructure for {settings.InstanceName}"))
            {
                var setupCommand = new SetupCommand();
                await setupCommand.Execute(new HostArguments([]), settings);
            }

            var configuration = new EndpointConfiguration(settings.InstanceName);
            configuration.CustomizeServiceControlMonitoringEndpointTesting(context);

            customConfiguration(configuration);

            using (new DiagnosticTimer($"Starting host for {settings.InstanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    // Force the DI container to run the dependency resolution check to verify all dependencies can be resolved
                    EnvironmentName = Environments.Development
                });
                hostBuilder.Logging.ClearProviders();
                hostBuilder.Logging.BuildLogger(LogLevel.Information);

                hostBuilder.AddServiceControlMonitoring((criticalErrorContext, cancellationToken) =>
                {
                    var logitem = new ScenarioContext.LogItem
                    {
                        Endpoint = settings.InstanceName,
                        Level = NServiceBus.Logging.LogLevel.Fatal,
                        LoggerName = $"{settings.InstanceName}.CriticalError",
                        Message = $"{criticalErrorContext.Error}{Environment.NewLine}{criticalErrorContext.Exception}"
                    };
                    context.Logs.Enqueue(logitem);
                    return criticalErrorContext.Stop(cancellationToken);
                }, settings, configuration);
                hostBuilder.AddServiceControlMonitoringApi();

                hostBuilder.AddServiceControlMonitoringTesting(settings);

                host = hostBuilder.Build();
                host.UseServiceControlMonitoring();
                await host.StartAsync();

                HttpClient = host.Services.GetRequiredKeyedService<TestServer>(settings.InstanceName).CreateClient();
            }
        }

        public override async Task Stop(CancellationToken cancellationToken = default)
        {
            using (new DiagnosticTimer($"Test TearDown for {settings.InstanceName}"))
            {
                await host.StopAsync(cancellationToken);
                HttpClient.Dispose();
                await host.DisposeAsync();
            }
        }

        WebApplication host;
        Settings settings;
    }
}