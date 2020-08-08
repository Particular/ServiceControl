namespace ServiceControl.AcceptanceTests.TestSupport
{
    using System;
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
    using System.Threading.Tasks;
    using System.Web.Http;
    using AcceptanceTesting;
    using Autofac;
    using Infrastructure.WebApi;
    using Microsoft.Owin.Builder;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Logging;
    using Particular.ServiceControl;
    using Recoverability.MessageFailures;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;

    class ServiceControlComponentRunner : ComponentRunner, IAcceptanceTestInfrastructureProvider
    {
        public ServiceControlComponentRunner(ITransportIntegration transportToUse, Action<Settings> setSettings, Action<EndpointConfiguration> customConfiguration)
        {
            this.transportToUse = transportToUse;
            this.customConfiguration = customConfiguration;
            this.setSettings = setSettings;
        }

        public override string Name { get; } = $"{nameof(ServiceControlComponentRunner)}";
        public Settings Settings { get; set; }
        public OwinHttpMessageHandler Handler { get; set; }
        public BusInstance Bus { get; set; }


        public HttpClient HttpClient { get; set; }
        public JsonSerializerSettings SerializerSettings { get; } = JsonNetSerializerSettings.CreateDefault();
        public string Port => Settings.Port.ToString();

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
            var instancePort = FindAvailablePort(33333);
            var maintenancePort = FindAvailablePort(instancePort + 1);

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

            setSettings(settings);
            Settings = settings;
            var configuration = new EndpointConfiguration(instanceName);

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

            configuration.AssemblyScanner().ExcludeAssemblies(typeof(ServiceControlComponentRunner).Assembly.GetName().Name);

            customConfiguration(configuration);

            using (new DiagnosticTimer($"Initializing Bootstrapper for {instanceName}"))
            {
                var logPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(logPath);

                var loggingSettings = new LoggingSettings(settings.ServiceName, logPath: logPath);
                bootstrapper = new Bootstrapper(settings, configuration, loggingSettings, builder =>
                {
                    builder.RegisterAssemblyTypes(typeof(FailedErrorsController).Assembly)
                        .AssignableTo<ApiController>()
                        .FindConstructorsWith(t => t.GetTypeInfo().DeclaredConstructors.ToArray())
                        .AsSelf();
                });
                bootstrapper.HttpClientFactory = HttpClientFactory;
            }

            using (new DiagnosticTimer($"Initializing AppBuilder for {instanceName}"))
            {
                var app = new AppBuilder();
                bootstrapper.Startup.Configuration(app, typeof(FailedErrorsController).Assembly);
                var appFunc = app.Build();

                Handler = new OwinHttpMessageHandler(appFunc)
                {
                    UseCookies = false,
                    AllowAutoRedirect = false
                };
                var httpClient = new HttpClient(Handler);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpClient = httpClient;
            }

            using (new DiagnosticTimer($"Creating infrastructure for {instanceName}"))
            {
                var setupBootstrapper = new SetupBootstrapper(settings, excludeAssemblies: new []{ typeof(IComponentBehavior).Assembly.GetName().Name });
                await setupBootstrapper.Run(null);
            }

            using (new DiagnosticTimer($"Creating and starting Bus for {instanceName}"))
            {
                Bus = await bootstrapper.Start(true).ConfigureAwait(false);
            }
        }

        public override async Task Stop()
        {
            using (new DiagnosticTimer($"Test TearDown for {instanceName}"))
            {
                await bootstrapper.Stop().ConfigureAwait(false);
                HttpClient.Dispose();
                Handler.Dispose();
                DeleteFolder(Settings.DbPath);
            }

            bootstrapper = null;
            Bus = null;
            HttpClient = null;
            Handler = null;
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
            var httpClient = new HttpClient(Handler);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return httpClient;
        }

        Bootstrapper bootstrapper;
        ITransportIntegration transportToUse;
        Action<Settings> setSettings;
        Action<EndpointConfiguration> customConfiguration;
        string instanceName = Settings.DEFAULT_SERVICE_NAME;
    }
}