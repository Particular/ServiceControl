namespace ServiceBus.Management.AcceptanceTests
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
    using Autofac;
    using Infrastructure;
    using Infrastructure.Nancy;
    using Infrastructure.Settings;
    using Microsoft.Owin.Builder;
    using Nancy;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Particular.ServiceControl;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(string[] instanceNames, ITransportIntegration transportToUse, Action<Settings> setSettings, Action<string, Settings> setInstanceSettings, Action<EndpointConfiguration> customConfiguration, Action<string, EndpointConfiguration> customInstanceConfiguration)
        {
            this.instanceNames = instanceNames;
            this.transportToUse = transportToUse;
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
            this.setInstanceSettings = setInstanceSettings;
            this.customInstanceConfiguration = customInstanceConfiguration;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";


        public Dictionary<string, HttpClient> HttpClients { get; } = new Dictionary<string, HttpClient>();
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializer.CreateDefault();
        public Dictionary<string, Settings> SettingsPerInstance { get; } = new Dictionary<string, Settings>();
        public Dictionary<string, OwinHttpMessageHandler> Handlers { get; } = new Dictionary<string, OwinHttpMessageHandler>();
        public Dictionary<string, BusInstance> Busses { get; } = new Dictionary<string, BusInstance>();

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
            if (instanceNames.Length == 0)
            {
                instanceNames = new[] {Settings.DEFAULT_SERVICE_NAME};
            }

            var startPort = 33333;
            foreach (var instanceName in instanceNames)
            {
                typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(context, instanceName);

                var instancePort = FindAvailablePort(startPort++);
                var maintenancePort = FindAvailablePort(startPort++);

                ConfigurationManager.AppSettings.Set("ServiceControl/TransportType", transportToUse.TypeName);

                var settings = new Settings(instanceName)
                {
                    Port = instancePort,
                    DatabaseMaintenancePort = maintenancePort,
                    DbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                    ForwardErrorMessages = false,
                    ForwardAuditMessages = false,
                    TransportCustomizationType = transportToUse.TypeName,
                    TransportConnectionString = transportToUse.ConnectionString,
                    ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                    MaximumConcurrencyLevel = 2,
                    HttpDefaultConnectionLimit = int.MaxValue,
                    RunInMemory = true,
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
                    }
                };

                if (instanceName == Settings.DEFAULT_SERVICE_NAME)
                {
                    setSettings(settings);
                }

                setInstanceSettings(instanceName, settings);
                SettingsPerInstance[instanceName] = settings;

                var configuration = new EndpointConfiguration(instanceName);
                configuration.EnableInstallers();

                configuration.GetSettings().Set("SC.ScenarioContext", context);
                configuration.GetSettings().Set<ScenarioContext>(context);

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

                configuration.AssemblyScanner().ExcludeAssemblies("ServiceBus.Management.AcceptanceTests");

                if (instanceName == Settings.DEFAULT_SERVICE_NAME)
                {
                    customConfiguration(configuration);
                }

                customInstanceConfiguration(instanceName, configuration);

                Bootstrapper bootstrapper;
                using (new DiagnosticTimer($"Initializing Bootstrapper for {instanceName}"))
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
                    }, settings, configuration, loggingSettings, builder => builder.RegisterType<FailedAuditsModule>().As<INancyModule>());
                    bootstrappers[instanceName] = bootstrapper;
                    bootstrapper.HttpClientFactory = HttpClientFactory;
                }

                using (new DiagnosticTimer($"Initializing AppBuilder for {instanceName}"))
                {
                    var app = new AppBuilder();
                    bootstrapper.Startup.Configuration(app);
                    var appFunc = app.Build();

                    var handler = new OwinHttpMessageHandler(appFunc)
                    {
                        UseCookies = false,
                        AllowAutoRedirect = false
                    };
                    Handlers[instanceName] = handler;
                    portToHandler[settings.Port] = handler; // port should be unique enough
                    var httpClient = new HttpClient(handler);
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    HttpClients[instanceName] = httpClient;
                }

                using (new DiagnosticTimer($"Creating and starting Bus for {instanceName}"))
                {
                    Busses[instanceName] = await bootstrapper.Start(true).ConfigureAwait(false);
                }
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
                    await bootstrappers[instanceName].Stop().ConfigureAwait(false);
                    HttpClients[instanceName].Dispose();
                    Handlers[instanceName].Dispose();
                    DeleteFolder(settings.DbPath);
                }
            }

            bootstrappers.Clear();
            HttpClients.Clear();
            Handlers.Clear();
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

        Dictionary<string, Bootstrapper> bootstrappers = new Dictionary<string, Bootstrapper>();
        Dictionary<int, HttpMessageHandler> portToHandler = new Dictionary<int, HttpMessageHandler>();
        ITransportIntegration transportToUse;
        Action<string, Settings> setInstanceSettings;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        Action<string, EndpointConfiguration> customInstanceConfiguration;
        string[] instanceNames;

        class ForwardingHandler : DelegatingHandler
        {
            public ForwardingHandler(Dictionary<int, HttpMessageHandler> portsToHttpMessageHandlers)
            {
                this.portsToHttpMessageHandlers = portsToHttpMessageHandlers;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var delegatingHandler = portsToHttpMessageHandlers[request.RequestUri.Port];
                InnerHandler = delegatingHandler;
                await Task.Yield();
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            Dictionary<int, HttpMessageHandler> portsToHttpMessageHandlers;
        }
    }
}