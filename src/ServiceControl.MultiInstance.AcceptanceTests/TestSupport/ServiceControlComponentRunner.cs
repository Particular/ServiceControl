namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Infrastructure.OWIN;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Owin.Builder;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Particular.ServiceControl;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.WebApi;

    using EndpointConfiguration = NServiceBus.EndpointConfiguration;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProviderMultiInstance
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, DataStoreConfiguration dataStoreConfiguration, Action<EndpointConfiguration> customEndpointConfiguration, Action<EndpointConfiguration> customAuditEndpointConfiguration, Action<Settings> customServiceControlSettings, Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings)
        {
            this.customServiceControlSettings = customServiceControlSettings;
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            this.customEndpointConfiguration = customEndpointConfiguration;
            this.dataStoreConfiguration = dataStoreConfiguration;
            this.transportToUse = transportToUse;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public Dictionary<string, HttpClient> HttpClients { get; } = [];
        public JsonSerializerSettings SerializerSettings { get; } = new();
        public Dictionary<string, dynamic> SettingsPerInstance { get; } = [];

        public async Task Initialize(RunDescriptor run)
        {
            SettingsPerInstance.Clear();

            var mainInstancePort = portLeases.GetPort();
            var auditInstancePort = portLeases.GetPort();

            await InitializeServiceControlAudit(run.ScenarioContext, auditInstancePort);
            await InitializeServiceControl(run.ScenarioContext, mainInstancePort, auditInstancePort);
        }

        async Task InitializeServiceControl(ScenarioContext context, int instancePort, int auditInstanceApiPort)
        {
            var instanceName = Settings.DEFAULT_SERVICE_NAME;
            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(context, instanceName);

            ConfigurationManager.AppSettings.Set("ServiceControl/TransportType", transportToUse.TypeName);

            var settings = new Settings(instanceName, transportToUse.TypeName, dataStoreConfiguration.DataStoreTypeName)
            {
                Port = instancePort,
                ForwardErrorMessages = false,
                TransportType = transportToUse.TypeName,
                TransportConnectionString = transportToUse.ConnectionString,
                ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(2),
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                DisableHealthChecks = true,
                ExposeApi = false,
                RemoteInstances =
                [
                    new RemoteInstanceSetting($"http://localhost:{auditInstanceApiPort}/api") // evil assumption for now
                ],
                MessageFilter = messageContext =>
                {
                    var headers = messageContext.Headers;
                    var id = messageContext.NativeMessageId;
                    var log = LogManager.GetLogger<ServiceControlComponentRunner>();
                    headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                    log.Debug($"OnMessage for message '{id}'({originalMessageId ?? string.Empty}).");

                    //Do not filter out CC, SA and HB messages as they can't be stamped
                    if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                        && messageTypes.StartsWith("ServiceControl."))
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

            databaseLease.CustomizeSettings(settings);

            customServiceControlSettings(settings);
            SettingsPerInstance[instanceName] = settings;

            var configuration = new EndpointConfiguration(instanceName);
            var scanner = configuration.AssemblyScanner();
            var excludedAssemblies = new[]
            {
                Path.GetFileName(typeof(Audit.Infrastructure.Settings.Settings).Assembly.Location),
                typeof(ServiceControlComponentRunner).Assembly.GetName().Name
            };
            scanner.ExcludeAssemblies(excludedAssemblies);

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

            customEndpointConfiguration(configuration);

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {
                var setupBootstrapper = new SetupBootstrapper(settings);
                await setupBootstrapper.Run();
            }

            using (new DiagnosticTimer($"Creating and starting Bus for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new LoggingSettings(settings.ServiceName, defaultLevel: NLog.LogLevel.Debug, logPath: logPath);

                var hostBuilder = WebApplication.CreateBuilder();
                hostBuilder.AddServiceControl(settings, configuration, loggingSettings);

                var app = hostBuilder.Build();

                app.UseServiceControl();
                await app.StartServiceControl();

                // TODO update this collection store WebApplication instances instead
                //hosts[instanceName] = host;
            }

            // TODO we shouldn't need this separate section anymore, but see if there is any config settings that need to be lifted to the section above before removing
            //using (new DiagnosticTimer($"Initializing AppBuilder for {instanceName}"))
            //{
            //    var app = new AppBuilder();
            //    var startup = new ServiceBus.Management.Infrastructure.OWIN.Startup(host.Services, bootstrapper.ApiAssemblies);
            //    startup.Configuration(app);
            //    var appFunc = app.Build();

            //    var handler = new OwinHttpMessageHandler(appFunc)
            //    {
            //        UseCookies = false,
            //        AllowAutoRedirect = false
            //    };
            //    handlers[instanceName] = handler;
            //    portToHandler[settings.Port] = handler; // port should be unique enough
            //    var httpClient = new HttpClient(handler);
            //    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //    HttpClients[instanceName] = httpClient;
            //}
        }

        async Task InitializeServiceControlAudit(ScenarioContext context, int instancePort)
        {
            var instanceName = Audit.Infrastructure.Settings.Settings.DEFAULT_SERVICE_NAME;
            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(context, instanceName);

            var settings = new Audit.Infrastructure.Settings.Settings(instanceName, transportToUse.TypeName, typeof(Audit.Persistence.InMemory.InMemoryPersistenceConfiguration).AssemblyQualifiedName)
            {
                Port = instancePort,
                TransportConnectionString = transportToUse.ConnectionString,
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                ExposeApi = false,
                ServiceControlQueueAddress = Settings.DEFAULT_SERVICE_NAME,
                MessageFilter = messageContext =>
                {
                    var id = messageContext.NativeMessageId;
                    var headers = messageContext.Headers;

                    var log = LogManager.GetLogger<ServiceControlComponentRunner>();
                    headers.TryGetValue(Headers.MessageId, out var originalMessageId);
                    log.Debug($"OnMessage for message '{id}'({originalMessageId ?? string.Empty}).");

                    //Do not filter out CC, SA and HB messages as they can't be stamped
                    if (headers.TryGetValue(Headers.EnclosedMessageTypes, out var messageTypes)
                        && messageTypes.StartsWith("ServiceControl."))
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

            var excludedAssemblies = new[]
            {
                Path.GetFileName(typeof(Settings).Assembly.Location), // ServiceControl.exe
                "ServiceControl.Persistence.RavenDB.dll",
                typeof(ServiceControlComponentRunner).Assembly.GetName().Name // This project
            };

            customServiceControlAuditSettings(settings);
            SettingsPerInstance[instanceName] = settings;

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {
                var setupBootstrapper = new Audit.Infrastructure.SetupBootstrapper(settings);
                await setupBootstrapper.Run();
            }

            var configuration = new EndpointConfiguration(instanceName);
            configuration.EnableInstallers();
            var scanner = configuration.AssemblyScanner();

            scanner.ExcludeAssemblies(excludedAssemblies);

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

            customAuditEndpointConfiguration(configuration);

            IHost host;
            using (new DiagnosticTimer($"Initializing Bootstrapper for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new Audit.Infrastructure.Settings.LoggingSettings(settings.ServiceName, logPath: logPath);
                var bootstrapper = new Audit.Infrastructure.Bootstrapper((criticalErrorContext, cancellationToken) =>
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
                },
                settings,
                configuration,
                loggingSettings);

                host = bootstrapper.HostBuilder.Build();

                await host.StartAsync();

                hosts[instanceName] = host;
            }

            using (new DiagnosticTimer($"Initializing AppBuilder for {instanceName}"))
            {
                var app = new AppBuilder();
                var startup = new Startup(host.Services);

                startup.Configuration(app);
                var appFunc = app.Build();

                var handler = new OwinHttpMessageHandler(appFunc)
                {
                    UseCookies = false,
                    AllowAutoRedirect = false
                };
                handlers[instanceName] = handler;
                portToHandler[settings.Port] = handler; // port should be unique enough
                var httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpClients[instanceName] = httpClient;
            }
        }

        public override async Task Stop()
        {
            foreach (var instanceAndSettings in SettingsPerInstance)
            {
                var instanceName = instanceAndSettings.Key;
                var settings = instanceAndSettings.Value;
                using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
                {
                    if (hosts.ContainsKey(instanceName))
                    {
                        var host = hosts[instanceName];
                        await host.StopAsync();
                        host.Dispose();
                    }
                    HttpClients[instanceName].Dispose();
                    handlers[instanceName].Dispose();
                    try
                    {
                        DirectoryDeleter.Delete(settings.DbPath);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            await databaseLease.DisposeAsync();
            portLeases?.Dispose();

            hosts.Clear();
            HttpClients.Clear();
            portToHandler.Clear();
            handlers.Clear();
        }

        Dictionary<string, IHost> hosts = [];

        Dictionary<string, OwinHttpMessageHandler> handlers = [];
        Dictionary<int, HttpMessageHandler> portToHandler = [];
        ITransportIntegration transportToUse;
        DataStoreConfiguration dataStoreConfiguration;
        Action<EndpointConfiguration> customEndpointConfiguration;
        Action<EndpointConfiguration> customAuditEndpointConfiguration;
        Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings;
        Action<Settings> customServiceControlSettings;

        static readonly PortPool portPool = new PortPool(33335);
        DatabaseLease databaseLease = SharedDatabaseSetup.LeaseDatabase();
        PortLease portLeases = portPool.GetLease();

        class ForwardingHandler : DelegatingHandler
        {
            public ForwardingHandler(Dictionary<int, HttpMessageHandler> portsToHttpMessageHandlers)
            {
                this.portsToHttpMessageHandlers = portsToHttpMessageHandlers;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
            {
                var delegatingHandler = portsToHttpMessageHandlers[request.RequestUri.Port];
                InnerHandler = delegatingHandler;
                return base.SendAsync(request, cancellationToken);
            }

            Dictionary<int, HttpMessageHandler> portsToHttpMessageHandlers;
        }
    }
}