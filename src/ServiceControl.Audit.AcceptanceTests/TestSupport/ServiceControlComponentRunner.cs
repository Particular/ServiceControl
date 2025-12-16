namespace ServiceControl.Audit.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.Loader;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure.Hosting;
    using Infrastructure.Hosting.Commands;
    using Infrastructure.Settings;
    using Infrastructure.WebApi;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using ServiceControl.Infrastructure;

    public class ServiceControlComponentRunner(
        ITransportIntegration transportToUse,
        AcceptanceTestStorageConfiguration persistenceToUse,
        Action<Settings> setSettings,
        Action<EndpointConfiguration> customConfiguration,
        Action<IDictionary<string, string>> setStorageConfiguration,
        Action<IHostApplicationBuilder> hostBuilderCustomization)
        : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public HttpClient HttpClient { get; private set; }
        public JsonSerializerOptions SerializerOptions => Infrastructure.WebApi.SerializerOptions.Default;
        public IServiceProvider ServiceProvider { get; private set; }
        public TestServer InstanceTestServer { get; private set; }
        public Task Initialize(RunDescriptor run) => InitializeServiceControl(run.ScenarioContext);

        async Task InitializeServiceControl(ScenarioContext context)
        {
            var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(logPath);

            var loggingSettings = new LoggingSettings(Settings.SettingsRootNamespace, defaultLevel: LogLevel.Debug, logPath: logPath);
            LoggerUtil.ActiveLoggers = Loggers.Test;

            settings = new Settings(transportToUse.TypeName, persistenceToUse.PersistenceType, loggingSettings)
            {
                InstanceName = instanceName,
                TransportConnectionString = transportToUse.ConnectionString,
                MaximumConcurrencyLevel = 2,
                ServiceControlQueueAddress = "SHOULDNOTBEUSED",
                MessageFilter = messageContext =>
                {
                    var id = messageContext.NativeMessageId;
                    var headers = messageContext.Headers;
                    var logger = LoggerUtil.CreateStaticLogger<ServiceControlComponentRunner>(loggingSettings.LogLevel);
                    headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                    logger.LogDebug("OnMessage for message '{MessageId}'({OriginalMessageId})", id, originalMessageId ?? string.Empty);

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
                        logger.LogDebug("Discarding message '{MessageId}'({OriginalMessageId}) because it's session id is '{SessionId}' instead of '{CurrentSessionId}'", id, originalMessageId ?? string.Empty, session, currentSession);
                        return true;
                    }

                    return false;
                },
                AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default
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
                var setupCommand = new SetupCommand();
                await setupCommand.Execute(new HostArguments([]), settings);
            }

            var configuration = new EndpointConfiguration(instanceName);
            configuration.CustomizeServiceControlAuditEndpointTesting(context);

            customConfiguration(configuration);

            using (new DiagnosticTimer($"Starting ServiceControl {instanceName}"))
            {
                var hostBuilder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    // Force the DI container to run the dependency resolution check to verify all dependencies can be resolved
                    EnvironmentName = Environments.Development
                });
                hostBuilder.AddServiceControlAudit((criticalErrorContext, cancellationToken) =>
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

                hostBuilder.AddServiceControlAuditApi(settings.CorsSettings);

                hostBuilder.AddServiceControlAuditTesting(settings);

                hostBuilderCustomization(hostBuilder);

                host = hostBuilder.Build();

                // Test middleware: Set RemoteIpAddress from X-Test-Remote-IP header
                // This must run BEFORE UseServiceControlAudit (which adds ForwardedHeaders middleware)
                // so that the ForwardedHeaders middleware can properly check KnownProxies/KnownNetworks
                host.Use(async (context, next) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Test-Remote-IP", out var testIpHeader))
                    {
                        var testIpValue = testIpHeader.ToString();
                        if (IPAddress.TryParse(testIpValue, out var testIp))
                        {
                            context.Connection.RemoteIpAddress = testIp;
                        }
                    }
                    await next();
                });

                host.UseServiceControlAudit(settings.ForwardedHeadersSettings, settings.HttpsSettings);
                await host.StartAsync();
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

        string instanceName = Settings.DEFAULT_INSTANCE_NAME;
        WebApplication host;
        Settings settings;
    }
}