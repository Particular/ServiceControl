namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Owin.Builder;
    using Newtonsoft.Json;
    using NLog;
    using NLog.Config;
    using NLog.Filters;
    using NLog.Targets;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using Particular.ServiceControl;
    using ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Recoverability;
    using LogManager = NServiceBus.Logging.LogManager;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        private Dictionary<string, Bootstrapper> bootstrappers = new Dictionary<string, Bootstrapper>();
        private Dictionary<string, BusInstance> busses = new Dictionary<string, BusInstance>();
        private Dictionary<string, HttpClient> httpClients = new Dictionary<string, HttpClient>();
        private Dictionary<int, HttpMessageHandler> portToHandler = new Dictionary<int, HttpMessageHandler>();
        private ITransportIntegration transportToUse;
        private Action<string, Settings> setInstanceSettings;
        private Action<Settings> setSettings;
        private Action<EndpointConfiguration> customConfiguration;
        private Action<string, EndpointConfiguration> customInstanceConfiguration;
        private string[] instanceNames;

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";

        public ServiceControlComponentRunner(string[] instanceNames, ITransportIntegration transportToUse, Action<Settings> setSettings, Action<string, Settings> setInstanceSettings, Action<EndpointConfiguration> customConfiguration, Action<string, EndpointConfiguration> customInstanceConfiguration)
        {
            this.instanceNames = instanceNames;
            this.transportToUse = transportToUse;
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
            this.setInstanceSettings = setInstanceSettings;
            this.customInstanceConfiguration = customInstanceConfiguration;
        }

        public Task Initialize(RunDescriptor run)
        {
            return InitializeServiceControl(run.ScenarioContext, instanceNames);
        }

        private static int FindAvailablePort(int startPort)
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

        private LoggingConfiguration SetupLogging(string endpointname)
        {
            var logDir = ".\\logfiles\\";

            Directory.CreateDirectory(logDir);

            var logFile = Path.Combine(logDir, $"{endpointname}.txt");

            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            var logLevel = "WARN";

            var nlogConfig = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                FileName = logFile,
                Layout = "${longdate}|${level:uppercase=true}|${threadid}|${logger}|${message}${onexception:${newline}${exception:format=tostring}}"
            };

            nlogConfig.LoggingRules.Add(MakeFilteredLoggingRule(fileTarget, LogLevel.Error, "Raven.*"));
            nlogConfig.LoggingRules.Add(new LoggingRule("*", LogLevel.FromString(logLevel), fileTarget));
            nlogConfig.AddTarget("debugger", fileTarget);

            return nlogConfig;
        }

        private static LoggingRule MakeFilteredLoggingRule(Target target, LogLevel logLevel, string text)
        {
            var rule = new LoggingRule(text, LogLevel.Info, target)
            {
                Final = true
            };

            rule.Filters.Add(new ConditionBasedFilter
            {
                Action = FilterResult.Ignore,
                Condition = $"level < LogLevel.{logLevel.Name}"
            });

            return rule;
        }

        private async Task InitializeServiceControl(ScenarioContext context, string[] instanceNames)
        {
            if (instanceNames.Length == 0)
            {
                instanceNames = new[] { Settings.DEFAULT_SERVICE_NAME };
            }

            // how to deal with the statics here?
            LogManager.Use<NLogFactory>();
            NLog.LogManager.Configuration = SetupLogging(Settings.DEFAULT_SERVICE_NAME);

            var startPort = 33333;
            foreach (var instanceName in instanceNames)
            {
                var instancePort = FindAvailablePort(startPort++);
                var maintenancePort = FindAvailablePort(startPort++);
                var settings = new Settings(instanceName)
                {
                    Port = instancePort,
                    DatabaseMaintenancePort = maintenancePort,
                    DbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                    ForwardErrorMessages = false,
                    ForwardAuditMessages = false,
                    TransportType = Type.GetType(transportToUse.TypeName),
                    TransportConnectionString = transportToUse.ConnectionString,
                    ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                    MaximumConcurrencyLevel = 2,
                    HttpDefaultConnectionLimit = int.MaxValue
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

                if (instanceName == Settings.DEFAULT_SERVICE_NAME)
                {
                    customConfiguration(configuration);
                }

                customInstanceConfiguration(instanceName, configuration);

                Bootstrapper bootstrapper;
                using (new DiagnosticTimer($"Initializing Bootstrapper for {instanceName}"))
                {
                    var loggingSettings = new LoggingSettings(settings.ServiceName);
                    bootstrapper = new Bootstrapper(() => { }, settings, configuration, loggingSettings);
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
                    httpClients[instanceName] = httpClient;
                }

                using (new DiagnosticTimer($"Creating and starting Bus for {instanceName}"))
                {
                    busses[instanceName] = await bootstrapper.Start(true).ConfigureAwait(false);
                }
            }

            // how to deal with the statics here?
            ArchivingManager.ArchiveOperations = new Dictionary<string, InMemoryArchive>();
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();
        }

        public override Task Stop()
        {
            foreach (var instanceAndSettings in SettingsPerInstance)
            {
                var instanceName = instanceAndSettings.Key;
                var settings = instanceAndSettings.Value;
                using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
                {
                    bootstrappers[instanceName].Stop();
                    httpClients[instanceName].Dispose();
                    Handlers[instanceName].Dispose();
                    DeleteFolder(settings.DbPath);
                }
            }

            return Task.FromResult(0);
        }

        private static void DeleteFolder(string path)
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
                    process.WaitForExit();
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

        private HttpClient HttpClientFactory()
        {
            var httpClient = new HttpClient(new ForwardingHandler(portToHandler));
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }

        class ForwardingHandler : DelegatingHandler
        {
            private Dictionary<int, HttpMessageHandler> portsToHttpMessageHandlers;

            public ForwardingHandler(Dictionary<int, HttpMessageHandler> portsToHttpMessageHandlers)
            {
                this.portsToHttpMessageHandlers = portsToHttpMessageHandlers;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var delegatingHandler = portsToHttpMessageHandlers[request.RequestUri.Port];
                InnerHandler = delegatingHandler;
                return base.SendAsync(request, cancellationToken);
            }
        }


        public Dictionary<string, HttpClient> HttpClients { get; } = new Dictionary<string, HttpClient>();
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializer.CreateDefault();
        public Dictionary<string, Settings> SettingsPerInstance { get; } = new Dictionary<string, Settings>();
        public Dictionary<string, OwinHttpMessageHandler> Handlers { get; } = new Dictionary<string, OwinHttpMessageHandler>();
    }
}