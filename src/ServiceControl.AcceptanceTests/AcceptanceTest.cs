namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.NetworkInformation;
    using System.Reflection;
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
    using NServiceBus.Configuration.AdvanceExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Hosting.Helpers;
    using NServiceBus.MessageInterfaces;
    using NServiceBus.MessageInterfaces.MessageMapper.Reflection;
    using NUnit.Framework;
    using Particular.ServiceControl;
    using ServiceBus.Management.AcceptanceTests.Contexts.TransportIntegration;
    using ServiceBus.Management.Infrastructure.Nancy;
    using ServiceBus.Management.Infrastructure.Settings;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;
    using LogManager = NServiceBus.Logging.LogManager;
    using ServiceControl.Recoverability;

    [TestFixture]
    public abstract class AcceptanceTest
    {
        private static readonly JsonSerializerSettings serializerSettings = JsonNetSerializer.CreateDefault();
        private Dictionary<string, Bootstrapper> bootstrappers = new Dictionary<string, Bootstrapper>();
        private Dictionary<string, IBus> busses = new Dictionary<string, IBus>();
        private Dictionary<string, HttpClient> httpClients = new Dictionary<string, HttpClient>();
        private Dictionary<int, HttpMessageHandler> portToHandler = new Dictionary<int, HttpMessageHandler>();
        protected Action<BusConfiguration> CustomConfiguration = _ => { };
        protected Action<string, BusConfiguration> CustomInstanceConfiguration = (i, c) => { };
        protected Dictionary<string, OwinHttpMessageHandler> Handlers = new Dictionary<string, OwinHttpMessageHandler>();
        protected Dictionary<string, Settings> SettingsPerInstance = new Dictionary<string, Settings>();

        private ScenarioContext scenarioContext = new ConsoleContext();
        private bool ignored;

        protected Action<Settings> SetSettings = _ => { };

        protected Action<string, Settings> SetInstanceSettings = (i, s) => { };

        private ITransportIntegration transportToUse;


        protected AcceptanceTest()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            ServicePointManager.MaxServicePoints = int.MaxValue;
            ServicePointManager.UseNagleAlgorithm = false; // Improvement for small tcp packets traffic, get buffered up to 1/2-second. If your storage communication is for small (less than ~1400 byte) payloads, this setting should help (especially when dealing with things like Azure Queues, which tend to have very small messages).
            ServicePointManager.Expect100Continue = false; // This ensures tcp ports are free up quicker by the OS, prevents starvation of ports
            ServicePointManager.SetTcpKeepAlive(true, 5000, 1000); // This is good for Azure because it reuses connections
        }

        [SetUp]
        public void Setup()
        {
            SetSettings = _ => { };
            SetInstanceSettings = (i, s) => { };
            CustomConfiguration = _ => { };
            CustomInstanceConfiguration = (i, c) => { };

            transportToUse = GetTransportIntegrationFromEnvironmentVar();
            Console.Out.WriteLine($"Using transport {transportToUse.Name}");
            
            AssertTransportNotExplicitlyIgnored();

            Conventions.EndpointNamingConvention = t =>
            {
                var baseNs = typeof(AcceptanceTest).Namespace;
                var testName = GetType().Name;
                return t.FullName.Replace($"{baseNs}.", string.Empty).Replace($"{testName}+", string.Empty);
            };
        }

        private static string ignoreTransportsKey = nameof(IgnoreTransportsAttribute).Replace("Attribute", "");

        private void AssertTransportNotExplicitlyIgnored()
        {
            if (TestContext.CurrentContext.Test.Properties.Contains(ignoreTransportsKey))
            {
                if (((string[]) TestContext.CurrentContext.Test.Properties[ignoreTransportsKey]).Contains(transportToUse.Name))
                {
                    ignored = true;
                    Assert.Inconclusive($"Transport {transportToUse.Name} has been explicitly ignored for test {TestContext.CurrentContext.Test.Name}");
                }
            }
        }

        [TearDown]
        public void Dispose()
        {
            if (ignored)
            {
                return;
            }

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

        protected void ExecuteWhen(Func<bool> execute, Action<IBus> action, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            var timeout = TimeSpan.FromSeconds(1);

            Task.Run(() =>
            {
                while (!SpinWait.SpinUntil(execute, timeout))
                {
                }

                action(busses[instanceName]);
            });
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(params string[] instanceNames) where T : ScenarioContext, new()
        {
            Func<T> instance = () => new T();
            return Define(instance, instanceNames);
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(T context, params string[] instanceNames) where T : ScenarioContext, new()
        {
            return Define(() => context, instanceNames);
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(Func<T> contextFactory, params string[] instanceNames) where T : ScenarioContext, new()
        {
            var ctx = contextFactory();

            if (ctx == scenarioContext)
            {
                //We have already SC running
                return new ScenarioWithContext<T>(() => (T)scenarioContext);
            }
            scenarioContext = ctx;
            scenarioContext.SessionId = Guid.NewGuid().ToString();

            InitializeServiceControl(scenarioContext, instanceNames);

            return new ScenarioWithContext<T>(() => (T) scenarioContext);
        }

        private async Task<T> GetInternal<T>(string url, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = httpClients[instanceName];
            var response = await httpClient.GetAsync(url);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int) response.StatusCode}");

            //for now
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                await Task.Delay(1000);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase}");
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var serializer = JsonSerializer.Create(serializerSettings);

                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }

        protected bool TryGetMany<T>(string url, out List<T> response, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            response = GetInternal<List<T>>(url, instanceName).GetAwaiter().GetResult();

            if (response == null || !response.Any(m => condition(m)))
            {
                Task.Delay(1000).GetAwaiter().GetResult();
                return false;
            }

            return true;
        }

        protected HttpStatusCode Patch<T>(string url, T payload = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{SettingsPerInstance[instanceName].Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            var httpClient = httpClients[instanceName];
            var response = httpClient.PatchAsync(url, new StringContent(json, null, "application/json")).GetAwaiter().GetResult();

            Console.WriteLine($"PATCH - {url} - {(int) response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase} - {body}");
            }

            return response.StatusCode;
        }

        protected bool TryGet<T>(string url, out T response, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            response = GetInternal<T>(url, instanceName).GetAwaiter().GetResult();

            if (response == null || !condition(response))
            {
                Task.Delay(1000).GetAwaiter().GetResult();
                return false;
            }

            return true;
        }

        protected bool TryGetSingle<T>(string url, out T item, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = GetInternal<List<T>>(url, instanceName).GetAwaiter().GetResult();
            item = null;
            if (response != null)
            {
                var items = response.Where(i => condition(i)).ToList();

                if (items.Count > 1)
                {
                    throw new InvalidOperationException("More than one matching element found");
                }

                item = items.SingleOrDefault();
            }

            if (item != null)
            {
                return true;
            }

            Task.Delay(1000).GetAwaiter().GetResult();

            return false;
        }

        protected HttpStatusCode Get(string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = httpClients[instanceName];
            var response = httpClient.GetAsync(url).GetAwaiter().GetResult();

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            return response.StatusCode;
        }

        protected void Post<T>(string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{SettingsPerInstance[instanceName].Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            var httpClient = httpClients[instanceName];
            var response = httpClient.PostAsync(url, new StringContent(json, null, "application/json")).GetAwaiter().GetResult();

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int) response.StatusCode}");

            if (requestHasFailed != null)
            {
                if (requestHasFailed(response.StatusCode))
                {
                    throw new Exception($"Expected status code not received, instead got {response.StatusCode}.");
                }
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        protected void Delete(string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = httpClients[instanceName];
            var response = httpClient.DeleteAsync(url).GetAwaiter().GetResult();

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        protected void Put<T>(string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{SettingsPerInstance[instanceName].Port}{url}";
            }

            if (requestHasFailed == null)
            {
               requestHasFailed = statusCode => statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.Accepted;
            }

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            var httpClient = httpClients[instanceName];
            var response = httpClient.PutAsync(url, new StringContent(json, null, "application/json")).GetAwaiter().GetResult();

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (requestHasFailed(response.StatusCode))
            {
                throw new Exception($"Expected status code not received, instead got {response.StatusCode}.");
            }
        }

        protected byte[] DownloadData(string url, HttpStatusCode successCode = HttpStatusCode.OK, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{SettingsPerInstance[instanceName].Port}/api{url}";
            }

            var httpClient = httpClients[instanceName];
            var response = httpClient.GetAsync(url).GetAwaiter().GetResult();
            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int) response.StatusCode}");
            if (response.StatusCode != successCode)
            {
                throw new Exception($"Expected status code of {successCode}, but instead got {response.StatusCode}.");
            }

            return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
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

        private static IEnumerable<Type> GetTypesScopedByTestClass(ITransportIntegration transportToUse)
        {
            var assemblies = new AssemblyScanner().GetScannableAssemblies();

            var types = assemblies.Assemblies
                //exclude all test types by default
                .Where(a => a != Assembly.GetExecutingAssembly())
                .Where(a =>
                {
                    if (a == transportToUse.Type.Assembly)
                    {
                        return true;
                    }
                    return !a.GetName().Name.Contains("Transports");
                })
                .Where(a => !a.GetName().Name.StartsWith("ServiceControl.Plugin"))
                .SelectMany(a => a.GetTypes());

            types = types.Union(GetNestedTypeRecursive(transportToUse.GetType()));

            return types;
        }

        private static IEnumerable<Type> GetNestedTypeRecursive(Type rootType)
        {
            yield return rootType;

            foreach (var nestedType in rootType.GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                yield return nestedType;
            }
        }

        public static ITransportIntegration GetTransportIntegrationFromEnvironmentVar()
        {
            ITransportIntegration transportToUse = null;

            var transportToUseString = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.Transport");
            if (transportToUseString != null)
            {
                transportToUse = (ITransportIntegration) Activator.CreateInstance(Type.GetType(typeof(MsmqTransportIntegration).FullName.Replace("Msmq", transportToUseString)) ?? typeof(MsmqTransportIntegration));
            }

            if (transportToUse == null)
            {
                transportToUse = new MsmqTransportIntegration();
            }

            var connectionString = Environment.GetEnvironmentVariable("ServiceControl.AcceptanceTests.ConnectionString");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                transportToUse.ConnectionString = connectionString;
            }

            return transportToUse;
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

        private void InitializeServiceControl(ScenarioContext context, string[] instanceNames)
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
                startPort = FindAvailablePort(startPort);
                var settings = new Settings
                {
                    Port = startPort++,
                    DbPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                    ForwardErrorMessages = false,
                    ForwardAuditMessages = false,
                    TransportType = transportToUse.TypeName,
                    TransportConnectionString = transportToUse.ConnectionString,
                    ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                    MaximumConcurrencyLevel = 2,
                    HttpDefaultConnectionLimit = int.MaxValue
                };

                if (instanceName == Settings.DEFAULT_SERVICE_NAME)
                {
                    SetSettings(settings);
                }

                SetInstanceSettings(instanceName, settings);
                SettingsPerInstance[instanceName] = settings;

                var configuration = new BusConfiguration();
                configuration.TypesToScan(GetTypesScopedByTestClass(transportToUse).Concat(new[]
                {
                    typeof(MessageMapperInterceptor),
                    typeof(RegisterWrappers),
                    typeof(SessionCopInBehavior),
                    typeof(SessionCopInBehaviorForMainPipe),
                    typeof(TraceIncomingBehavior),
                    typeof(TraceOutgoingBehavior)
                }));
                configuration.EnableInstallers();

                configuration.GetSettings().SetDefault("ScaleOut.UseSingleBrokerQueue", true);
                configuration.GetSettings().Set("SC.ScenarioContext", context);

                // This is a hack to ensure ServiceControl picks the correct type for the messages that come from plugins otherwise we pick the type from the plugins assembly and that is not the type we want, we need to pick the type from ServiceControl assembly.
                // This is needed because we no longer use the AppDomain separation.
                configuration.EnableFeature<MessageMapperInterceptor>();
                configuration.RegisterComponents(r => { configuration.GetSettings().Set("SC.ConfigureComponent", r); });

                configuration.RegisterComponents(r =>
                {
                    r.RegisterSingleton(context.GetType(), context);
                    r.RegisterSingleton(typeof(ScenarioContext), context);
                });

                configuration.Pipeline.Register<SessionCopInBehavior.Registration>();
                configuration.Pipeline.Register<SessionCopInBehaviorForMainPipe.Registration>();
                configuration.Pipeline.Register<TraceIncomingBehavior.Registration>();
                configuration.Pipeline.Register<TraceOutgoingBehavior.Registration>();

                if (instanceName == Settings.DEFAULT_SERVICE_NAME)
                {
                    CustomConfiguration(configuration);
                }

                CustomInstanceConfiguration(instanceName, configuration);

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
                    busses[instanceName] = bootstrapper.Start(true);
                }
            }

            // how to deal with the statics here?
            ArchivingManager.ArchiveOperations = new Dictionary<string, InMemoryArchive>();
            RetryingManager.RetryOperations = new Dictionary<string, InMemoryRetry>();
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

        private class MessageMapperInterceptor : Feature
        {
            public MessageMapperInterceptor()
            {
                DependsOn<JsonSerialization>();
            }

            protected override void Setup(FeatureConfigurationContext context)
            {
                context.Container.ConfigureComponent<IMessageMapper>(builder => new MessageMapperWrapper(), DependencyLifecycle.SingleInstance);
            }
        }

        private class MessageMapperWrapper : IMessageMapper
        {
            private static string assemblyName;

            private IMessageMapper messageMapper;

            static MessageMapperWrapper()
            {
                var s = typeof(Bootstrapper).AssemblyQualifiedName;
                assemblyName = s.Substring(s.IndexOf(','));
            }

            public MessageMapperWrapper()
            {
                messageMapper = new MessageMapper();
            }

            public T CreateInstance<T>()
            {
                return messageMapper.CreateInstance<T>();
            }

            public T CreateInstance<T>(Action<T> action)
            {
                return messageMapper.CreateInstance(action);
            }

            public object CreateInstance(Type messageType)
            {
                return messageMapper.CreateInstance(messageType);
            }

            public void Initialize(IEnumerable<Type> types)
            {
                messageMapper.Initialize(types);
            }

            public Type GetMappedTypeFor(Type t)
            {
                if (t.Assembly.GetName().Name.StartsWith("ServiceControl.Plugin"))
                {
                    return Type.GetType($"{t.FullName}{assemblyName}");
                }
                return messageMapper.GetMappedTypeFor(t);
            }

            public Type GetMappedTypeFor(string typeName)
            {
                return messageMapper.GetMappedTypeFor(typeName);
            }
        }

        private class ConsoleContext : ScenarioContext
        {
        }
    }

    public static class HttpClientExtensions
    {
        public static async Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent iContent)
        {
            var method = new HttpMethod("PATCH");
            var request = new HttpRequestMessage(method, requestUri)
            {
                Content = iContent
            };

            var response = await client.SendAsync(request);

            return response;
        }
    }
}