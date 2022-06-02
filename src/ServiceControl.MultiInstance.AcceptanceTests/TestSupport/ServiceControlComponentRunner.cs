namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Infrastructure.OWIN;
    using Autofac;
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

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProviderMultiInstance
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, Action<EndpointConfiguration> customEndpointConfiguration, Action<EndpointConfiguration> customAuditEndpointConfiguration, Action<Settings> customServiceControlSettings, Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings)
        {
            this.customServiceControlSettings = customServiceControlSettings;
            this.customServiceControlAuditSettings = customServiceControlAuditSettings;
            this.customAuditEndpointConfiguration = customAuditEndpointConfiguration;
            this.customEndpointConfiguration = customEndpointConfiguration;
            this.transportToUse = transportToUse;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public Dictionary<string, HttpClient> HttpClients { get; } = new Dictionary<string, HttpClient>();
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializerSettings.CreateDefault();
        public Dictionary<string, dynamic> SettingsPerInstance { get; } = new Dictionary<string, dynamic>();

        public async Task Initialize(RunDescriptor run)
        {
            SettingsPerInstance.Clear();

            var startPort = 33334;

            var mainInstancePort = FindAvailablePort(startPort);
            var mainInstanceDbPort = FindAvailablePort(mainInstancePort + 1);
            var auditInstancePort = FindAvailablePort(mainInstanceDbPort + 1);
            var auditInstanceDbPort = FindAvailablePort(auditInstancePort + 1);

            await InitializeServiceControlAudit(run.ScenarioContext, auditInstancePort, auditInstanceDbPort).ConfigureAwait(false);
            await InitializeServiceControl(run.ScenarioContext, mainInstancePort, mainInstanceDbPort, auditInstancePort).ConfigureAwait(false);
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

        async Task InitializeServiceControl(ScenarioContext context, int instancePort, int maintenancePort, int auditInstanceApiPort)
        {
            var instanceName = Settings.DEFAULT_SERVICE_NAME;
            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(context, instanceName);

            ConfigurationManager.AppSettings.Set("ServiceControl/TransportType", transportToUse.TypeName);

            var settings = new Settings(instanceName)
            {
                Port = instancePort,
                DatabaseMaintenancePort = maintenancePort,
                DbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                ForwardErrorMessages = false,
                TransportCustomizationType = transportToUse.TypeName,
                TransportConnectionString = transportToUse.ConnectionString,
                ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                TimeToRestartErrorIngestionAfterFailure = TimeSpan.FromSeconds(2),
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                RunInMemory = true,
                DisableHealthChecks = true,
                ExposeApi = false,
                RemoteInstances = new[]
                {
                    new RemoteInstanceSetting
                    {
                        ApiUri = $"http://localhost:{auditInstanceApiPort}/api" // evil assumption for now
                    }
                },
                MessageFilter = messageContext =>
                {
                    var headers = messageContext.Headers;
                    var id = messageContext.MessageId;
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

            customServiceControlSettings(settings);
            SettingsPerInstance[instanceName] = settings;

            var configuration = new EndpointConfiguration(instanceName);
            var scanner = configuration.AssemblyScanner();
            var excludedAssemblies = new[]
            {
                Path.GetFileName(typeof(Audit.Infrastructure.Settings.Settings).Assembly.CodeBase),
                typeof(ServiceControlComponentRunner).Assembly.GetName().Name
            };
            scanner.ExcludeAssemblies(excludedAssemblies);

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

            customEndpointConfiguration(configuration);

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {
                var setupBootstrapper = new SetupBootstrapper(settings, excludedAssemblies);
                await setupBootstrapper.Run(null);
            }

            IHost host;
            Bootstrapper bootstrapper;
            using (new DiagnosticTimer($"Creating and starting Bus for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new LoggingSettings(settings.ServiceName, logPath: logPath);
                bootstrapper = new Bootstrapper(settings, configuration, loggingSettings)
                {
                    HttpClientFactory = HttpClientFactory
                };

                host = bootstrapper.HostBuilder.Build();
                await host.StartAsync().ConfigureAwait(false);
                hosts[instanceName] = host;
            }

            using (new DiagnosticTimer($"Initializing AppBuilder for {instanceName}"))
            {
                var app = new AppBuilder();
                var lifetimeScope = host.Services.GetRequiredService<ILifetimeScope>();
                var startup = new ServiceBus.Management.Infrastructure.OWIN.Startup(lifetimeScope, bootstrapper.ApiAssemblies);
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

        async Task InitializeServiceControlAudit(ScenarioContext context, int instancePort, int maintenancePort)
        {
            var instanceName = Audit.Infrastructure.Settings.Settings.DEFAULT_SERVICE_NAME;
            typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(context, instanceName);

            ConfigurationManager.AppSettings.Set("ServiceControl.Audit/TransportType", transportToUse.TypeName);

            var settings = new Audit.Infrastructure.Settings.Settings(instanceName)
            {
                Port = instancePort,
                DatabaseMaintenancePort = maintenancePort,
                DbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                TransportCustomizationType = transportToUse.TypeName,
                TransportConnectionString = transportToUse.ConnectionString,
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue,
                RunInMemory = true,
                ExposeApi = false,
                ServiceControlQueueAddress = Settings.DEFAULT_SERVICE_NAME,
                MessageFilter = messageContext =>
                {
                    var id = messageContext.MessageId;
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

            var excludedAssemblies = new[]
            {
                Path.GetFileName(typeof(Settings).Assembly.CodeBase),
                typeof(ServiceControlComponentRunner).Assembly.GetName().Name
            };

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {
                var setupBootstrapper = new Audit.Infrastructure.SetupBootstrapper(settings, excludeAssemblies: excludedAssemblies
                    .Concat(new[] { typeof(IComponentBehavior).Assembly.GetName().Name }).ToArray());
                await setupBootstrapper.Run(null);
            }

            customServiceControlAuditSettings(settings);
            SettingsPerInstance[instanceName] = settings;

            var configuration = new EndpointConfiguration(instanceName);
            configuration.EnableInstallers();
            var scanner = configuration.AssemblyScanner();

            scanner.ExcludeAssemblies(excludedAssemblies);

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

            customAuditEndpointConfiguration(configuration);

            IHost host;
            using (new DiagnosticTimer($"Initializing Bootstrapper for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new Audit.Infrastructure.Settings.LoggingSettings(settings.ServiceName, logPath: logPath);
                var bootstrapper = new Audit.Infrastructure.Bootstrapper(ctx =>
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
                }, settings, configuration, loggingSettings);

                host = bootstrapper.HostBuilder.Build();

                await host.StartAsync().ConfigureAwait(false);

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
                        await hosts[instanceName].StopAsync().ConfigureAwait(false);
                    }
                    HttpClients[instanceName].Dispose();
                    handlers[instanceName].Dispose();
                    DeleteFolder(settings.DbPath);
                }
            }

            hosts.Clear();
            HttpClients.Clear();
            portToHandler.Clear();
            handlers.Clear();
        }

        static void DeleteFolder(string path)
        {
            DirectoryInfo emptyTempDirectory = null;

            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                emptyTempDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
                emptyTempDirectory.Create();
                var arguments = $"\"{emptyTempDirectory.FullName}\" \"{path.TrimEnd('\\')}\" /W:1  /R:1 /FFT /MIR /NFL";
                using (var process = Process.Start(new ProcessStartInfo("robocopy")
                {
                    Arguments = arguments,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                }))
                {
                    process?.WaitForExit();
                }

                using (var windowsIdentity = WindowsIdentity.GetCurrent())
                {
                    var directorySecurity = new DirectorySecurity();
                    directorySecurity.SetOwner(windowsIdentity.User);
                    Directory.SetAccessControl(path, directorySecurity);
                }

                if (!(Directory.GetFiles(path).Any() || Directory.GetDirectories(path).Any()))
                {
                    Directory.Delete(path);
                }
            }
            finally
            {
                emptyTempDirectory?.Delete();
            }
        }

        HttpClient HttpClientFactory()
        {
            var httpClient = new HttpClient(new ForwardingHandler(portToHandler));
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }

        Dictionary<string, IHost> hosts = new Dictionary<string, IHost>();

        Dictionary<string, OwinHttpMessageHandler> handlers = new Dictionary<string, OwinHttpMessageHandler>();
        Dictionary<int, HttpMessageHandler> portToHandler = new Dictionary<int, HttpMessageHandler>();
        ITransportIntegration transportToUse;
        Action<EndpointConfiguration> customEndpointConfiguration;
        Action<EndpointConfiguration> customAuditEndpointConfiguration;
        Action<Audit.Infrastructure.Settings.Settings> customServiceControlAuditSettings;
        Action<Settings> customServiceControlSettings;

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