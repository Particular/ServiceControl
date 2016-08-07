namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.NetworkInformation;
    using System.Reflection;
    using System.Security.AccessControl;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Owin.Builder;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
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
    using ServiceBus.Management.Infrastructure.Extensions;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.SignalR;
    using AppBuilderExtensions = ServiceBus.Management.Infrastructure.Extensions.AppBuilderExtensions;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;
    using LogManager = NServiceBus.Logging.LogManager;

    [TestFixture]
    public abstract class AcceptanceTest
    {
        private static readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new UnderscoreMappingResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters =
            {
                new IsoDateTimeConverter
                {
                    DateTimeStyles = DateTimeStyles.RoundtripKind
                },
                new StringEnumConverter
                {
                    CamelCaseText = true
                }
            }
        };

        private Bootstrapper bootstrapper;
        protected Action<BusConfiguration> CustomConfiguration = _ => { };
        private ExposeBus exposeBus;
        protected OwinHttpMessageHandler Handler;

        private HttpClient httpClient;
        private int port;
        private string ravenPath;
        private ScenarioContext scenarioContext = new ConsoleContext();

        protected Action<Settings> SetSettings = _ => { };

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
            port = FindAvailablePort(33333);
            SetSettings = _ => { };
            CustomConfiguration = _ => { };

            transportToUse = GetTransportIntegrationFromEnvironmentVar();
            Console.Out.WriteLine($"Using transport {transportToUse.Name}");
            Console.Out.WriteLine($"Using port {port}");

            Conventions.EndpointNamingConvention = t =>
            {
                var baseNs = typeof(AcceptanceTest).Namespace;
                var testName = GetType().Name;
                return t.FullName.Replace($"{baseNs}.", string.Empty).Replace($"{testName}+", string.Empty);
            };

            ravenPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        }

        [TearDown]
        public void Dispose()
        {
            using (new DiagnosticTimer("Test TearDown"))
            {
                bootstrapper.Stop();
                httpClient.Dispose();
                Delete(ravenPath);
            }
        }

        private static void Delete(string path)
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

        protected void ExecuteWhen(Func<bool> execute, Action<IBus> action)
        {
            var timeout = TimeSpan.FromSeconds(1);

            Task.Run(() =>
            {
                while (!SpinWait.SpinUntil(execute, timeout))
                {
                }

                action(exposeBus.GetBus());
            });
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>() where T : ScenarioContext, new()
        {
            Func<T> instance = () => new T();
            return Define(instance);
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(T context) where T : ScenarioContext, new()
        {
            return Define(() => context);
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(Func<T> contextFactory) where T : ScenarioContext, new()
        {
            scenarioContext = contextFactory();
            scenarioContext.SessionId = Guid.NewGuid().ToString();

            InitializeServiceControl(scenarioContext);

            return new ScenarioWithContext<T>(() => (T) scenarioContext);
        }

        private async Task<T> Get<T>(string url) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{port}{url}";
            }

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

        protected bool TryGetMany<T>(string url, out List<T> response, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            response = Get<List<T>>(url).GetAwaiter().GetResult();

            if (response == null || !response.Any(m => condition(m)))
            {
                Task.Delay(1000).GetAwaiter().GetResult();
                return false;
            }

            return true;
        }

        protected HttpStatusCode Patch<T>(string url, T payload = null) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            var response = httpClient.PatchAsync(url, new StringContent(json, null, "application/json")).GetAwaiter().GetResult();

            Console.WriteLine($"PATCH - {url} - {(int) response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase} - {body}");
            }

            return response.StatusCode;
        }

        protected bool TryGet<T>(string url, out T response, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            response = Get<T>(url).GetAwaiter().GetResult();

            if (response == null || !condition(response))
            {
                Task.Delay(1000).GetAwaiter().GetResult();
                return false;
            }

            return true;
        }

        protected bool TryGetSingle<T>(string url, out T item, Predicate<T> condition = null) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = Get<List<T>>(url).GetAwaiter().GetResult();
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

        protected void Post<T>(string url, T payload = null) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, serializerSettings);
            var response = httpClient.PostAsync(url, new StringContent(json, null, "application/json")).GetAwaiter().GetResult();

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int) response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        protected byte[] DownloadData(string url, HttpStatusCode successCode = HttpStatusCode.OK)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{port}/api{url}";
            }

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

        private void InitializeServiceControl(ScenarioContext context)
        {
            LogManager.Use<NLogFactory>();
            NLog.LogManager.Configuration = SetupLogging(Settings.DEFAULT_SERVICE_NAME);

            var settings = new Settings
            {
                Port = port,
                DbPath = ravenPath,
                ForwardErrorMessages = false,
                ForwardAuditMessages = false,
                TransportType = transportToUse.TypeName,
                TransportConnectionString = transportToUse.ConnectionString,
                ProcessRetryBatchesFrequency = TimeSpan.FromSeconds(2),
                MaximumConcurrencyLevel = 2,
                HttpDefaultConnectionLimit = int.MaxValue
            };

            SetSettings(settings);

            var configuration = new BusConfiguration();
            configuration.TypesToScan(GetTypesScopedByTestClass(transportToUse).Concat(new[]
            {
                typeof(MessageMapperInterceptor),
                typeof(RegisterWrappers),
                typeof(SessionCopInBehavior),
                typeof(SessionCopInBehaviorForMainPipe)
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

            CustomConfiguration(configuration);

            exposeBus = new ExposeBus();

            using (new DiagnosticTimer("Initializing Bootstrapper"))
            {
                bootstrapper = new Bootstrapper(settings, configuration, exposeBus);
            }
            using (new DiagnosticTimer("Initializing AppBuilder"))
            {
                var app = new AppBuilder();
                var cts = new CancellationTokenSource();
                app.Properties[AppBuilderExtensions.HostOnAppDisposing] = cts.Token;
                bootstrapper.WebApp = new Disposable(() => cts.Cancel(false));
                bootstrapper.Startup.Configuration(app);
                var appFunc = app.Build();

                Handler = new OwinHttpMessageHandler(appFunc)
                {
                    UseCookies = false,
                    AllowAutoRedirect = false
                };
                httpClient = new HttpClient(Handler);
            }
        }

        private class Disposable : MarshalByRefObject, IDisposable
        {
            private readonly Action dispose;

            public Disposable(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                dispose();
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