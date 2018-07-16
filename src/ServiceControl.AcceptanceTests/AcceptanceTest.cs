namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Logging;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.DomainEvents;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    [TestFixture]
    public abstract class AcceptanceTest : IAcceptanceTestInfrastructureProvider
    {
        protected Action<EndpointConfiguration> CustomConfiguration = _ => { };
        protected Action<string, EndpointConfiguration> CustomInstanceConfiguration = (i, c) => { };
        protected Action<Settings> SetSettings = _ => { };
        protected Action<string, Settings> SetInstanceSettings = (i, s) => { };

        protected AcceptanceTest()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            ServicePointManager.MaxServicePoints = int.MaxValue;
            ServicePointManager.UseNagleAlgorithm = false; // Improvement for small tcp packets traffic, get buffered up to 1/2-second. If your storage communication is for small (less than ~1400 byte) payloads, this setting should help (especially when dealing with things like Azure Queues, which tend to have very small messages).
            ServicePointManager.Expect100Continue = false; // This ensures tcp ports are free up quicker by the OS, prevents starvation of ports
            ServicePointManager.SetTcpKeepAlive(true, 5000, 1000); // This is good for Azure because it reuses connections
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            Scenario.GetLoggerFactory = ctx => new StaticLoggerFactory(ctx);
        }

        [SetUp]
        public void Setup()
        {
            SetSettings = _ => { };
            SetInstanceSettings = (i, s) => { };
            CustomConfiguration = _ => { };
            CustomInstanceConfiguration = (i, c) => { };

#if !NETCOREAPP2_0
            System.Configuration.ConfigurationManager.GetSection("X");
#endif

            var transportToUse = (ITransportIntegration)TestSuiteConstraints.Current.CreateTransportConfiguration();
            Console.Out.WriteLine($"Using transport {transportToUse.Name}");

            AssertTransportNotExplicitlyIgnored(transportToUse);

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(transportToUse, s => SetSettings(s), (i, s) => SetInstanceSettings(i, s), s => CustomConfiguration(s), (i, c) => CustomInstanceConfiguration(i, c));

            RemoveOtherTransportAssemblies(transportToUse.TypeName);

            Conventions.EndpointNamingConvention = t =>
            {
                var baseNs = typeof(AcceptanceTest).Namespace;
                var testName = GetType().Name;
                return t.FullName.Replace($"{baseNs}.", string.Empty).Replace($"{testName}+", string.Empty);
            };
        }

        private void RemoveOtherTransportAssemblies(string name)
        {
            var assembly = Type.GetType(name, true).Assembly;

            var otherAssemblies = Directory.EnumerateFiles(Path.GetDirectoryName(assembly.Location), "ServiceControl.Transports.*.dll")
                .Where(transportAssembly => transportAssembly != assembly.Location);

            foreach (var transportAssembly in otherAssemblies)
            {
                File.Delete(transportAssembly);
            }
        }

        private static string ignoreTransportsKey = nameof(IgnoreTransportsAttribute).Replace("Attribute", "");
        private ServiceControlComponentBehavior serviceControlRunnerBehavior;

        private void AssertTransportNotExplicitlyIgnored(ITransportIntegration transportToUse)
        {
            if (!TestContext.CurrentContext.Test.Properties.ContainsKey(ignoreTransportsKey))
            {
                return;
            }

            var ignoredTransports = (string[])TestContext.CurrentContext.Test.Properties[ignoreTransportsKey].First();
            if (ignoredTransports.Contains(transportToUse.Name))
            {
                Assert.Inconclusive($"Transport {transportToUse.Name} has been explicitly ignored for test {TestContext.CurrentContext.Test.Name}");
            }
        }

        protected void ExecuteWhen(Func<bool> execute, Func<IDomainEvents, Task> action, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            var timeout = TimeSpan.FromSeconds(1);

            Task.Run(async () =>
            {
                while (!SpinWait.SpinUntil(execute, timeout))
                {
                }

                await action(Busses[instanceName].DomainEvents);
            });
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(params string[] instanceNames) where T : ScenarioContext, new()
        {
            return Define<T>(c => { }, instanceNames);
        }

        protected IScenarioWithEndpointBehavior<T> Define<T>(Action<T> contextInitializer, params string[] instanceNames) where T : ScenarioContext, new()
        {
            serviceControlRunnerBehavior.Initialize(instanceNames);
            return Scenario.Define(contextInitializer)
                .WithComponent(serviceControlRunnerBehavior);
        }

        public Dictionary<string, HttpClient> HttpClients => serviceControlRunnerBehavior.HttpClients;
        public JsonSerializerSettings SerializerSettings => serviceControlRunnerBehavior.SerializerSettings;
        public Dictionary<string, Settings> SettingsPerInstance => serviceControlRunnerBehavior.SettingsPerInstance;
        public Dictionary<string, OwinHttpMessageHandler> Handlers => serviceControlRunnerBehavior.Handlers;
        public Dictionary<string, BusInstance> Busses => serviceControlRunnerBehavior.Busses;
    }

    class StaticLoggerFactory : ILoggerFactory
    {
        public static ScenarioContext CurrentContext;

        public StaticLoggerFactory(ScenarioContext currentContext)
        {
            CurrentContext = currentContext;
        }

        public ILog GetLogger(Type type)
        {
            return GetLogger(type.FullName);
        }

        public ILog GetLogger(string name)
        {
            return new StaticContextAppender();
        }
    }

    class StaticContextAppender : ILog
    {
        public bool IsDebugEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Debug;
        public bool IsInfoEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Info;
        public bool IsWarnEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Warn;
        public bool IsErrorEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Error;
        public bool IsFatalEnabled => StaticLoggerFactory.CurrentContext.LogLevel <= LogLevel.Fatal;


        public void Debug(string message)
        {
            Log(message, LogLevel.Debug);
        }

        public void Debug(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Debug);
        }

        public void DebugFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Debug);
        }

        public void Info(string message)
        {
            Log(message, LogLevel.Info);
        }


        public void Info(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Info);
        }

        public void InfoFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Info);
        }

        public void Warn(string message)
        {
            Log(message, LogLevel.Warn);
        }

        public void Warn(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Warn);
        }

        public void WarnFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Warn);
        }

        public void Error(string message)
        {
            Log(message, LogLevel.Error);
        }

        public void Error(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Error);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Error);
        }

        public void Fatal(string message)
        {
            Log(message, LogLevel.Fatal);
        }

        public void Fatal(string message, Exception exception)
        {
            var fullMessage = $"{message} {exception}";
            Log(fullMessage, LogLevel.Fatal);
        }

        public void FatalFormat(string format, params object[] args)
        {
            var fullMessage = string.Format(format, args);
            Log(fullMessage, LogLevel.Fatal);
        }

        void Log(string message, LogLevel messageSeverity)
        {
            if (StaticLoggerFactory.CurrentContext.LogLevel > messageSeverity)
            {
                return;
            }

            Trace.WriteLine(message);
            StaticLoggerFactory.CurrentContext.Logs.Enqueue(new ScenarioContext.LogItem
            {
                Endpoint =  (string) typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic).GetValue(StaticLoggerFactory.CurrentContext),
                Level = messageSeverity,
                Message = message
            });
        }
}

    public static class HttpExtensions
    {
        public static async Task Put<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            if (requestHasFailed == null)
            {
                requestHasFailed = statusCode => statusCode != HttpStatusCode.OK && statusCode != HttpStatusCode.Accepted;
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.PutAsync(url, new StringContent(json, null, "application/json"));

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (requestHasFailed(response.StatusCode))
            {
                throw new Exception($"Expected status code not received, instead got {response.StatusCode}.");
            }
        }

        public static Task<HttpResponseMessage> GetRaw(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            return httpClient.GetAsync(url);
        }

        public static async Task<ManyResult<T>> TryGetMany<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<List<T>>(url, instanceName).ConfigureAwait(false);

            if (response == null || !response.Any(m => condition(m)))
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return ManyResult<T>.Empty;
            }

            return ManyResult<T>.New(true, response);
        }

        public static  async Task<HttpStatusCode> Patch<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.PatchAsync(url, new StringContent(json, null, "application/json")).ConfigureAwait(false);

            Console.WriteLine($"PATCH - {url} - {(int) response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase} - {body}");
            }

            return response.StatusCode;
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<T>(url, instanceName).ConfigureAwait(false);

            if (response == null || !condition(response))
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGet<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Func<T, Task<bool>> condition, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            var response = await provider.GetInternal<T>(url, instanceName).ConfigureAwait(false);

            if (response == null || !await condition(response).ConfigureAwait(false))
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return SingleResult<T>.Empty;
            }

            return SingleResult<T>.New(response);
        }

        public static async Task<SingleResult<T>> TryGetSingle<T>(this IAcceptanceTestInfrastructureProvider provider, string url, Predicate<T> condition = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (condition == null)
            {
                condition = _ => true;
            }

            var response = await provider.GetInternal<List<T>>(url, instanceName);
            T item = null;
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
                await Task.Delay(1000).ConfigureAwait(false);
                return SingleResult<T>.New(item);
            }

            return SingleResult<T>.Empty;
        }

        public static async Task<HttpStatusCode> Get(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.GetAsync(url).ConfigureAwait(false);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            return response.StatusCode;
        }

        public static async Task Post<T>(this IAcceptanceTestInfrastructureProvider provider, string url, T payload = null, Func<HttpStatusCode, bool> requestHasFailed = null, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var json = JsonConvert.SerializeObject(payload, provider.SerializerSettings);
            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.PostAsync(url, new StringContent(json, null, "application/json")).ConfigureAwait(false);

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
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        public static async Task Delete(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.DeleteAsync(url);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Call failed: {(int)response.StatusCode} - {response.ReasonPhrase} - {body}");
            }
        }

        public static async Task<byte[]> DownloadData(this IAcceptanceTestInfrastructureProvider provider, string url, HttpStatusCode successCode = HttpStatusCode.OK, string instanceName = Settings.DEFAULT_SERVICE_NAME)
        {
            if (!url.StartsWith("http://"))
            {
                url = $"http://localhost:{provider.SettingsPerInstance[instanceName].Port}/api{url}";
            }

            var httpClient = provider.HttpClients[instanceName];
            var response = await httpClient.GetAsync(url);
            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int) response.StatusCode}");
            if (response.StatusCode != successCode)
            {
                throw new Exception($"Expected status code of {successCode}, but instead got {response.StatusCode}.");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }

        private static async Task<T> GetInternal<T>(this IAcceptanceTestInfrastructureProvider provider, string url, string instanceName = Settings.DEFAULT_SERVICE_NAME) where T : class
        {
            var response = await provider.GetRaw(url, instanceName).ConfigureAwait(false);

            Console.WriteLine($"{response.RequestMessage.Method} - {url} - {(int) response.StatusCode}");

            //for now
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                return null;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Call failed: {(int) response.StatusCode} - {response.ReasonPhrase}");
            }

            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                var serializer = JsonSerializer.Create(provider.SerializerSettings);

                return serializer.Deserialize<T>(new JsonTextReader(new StreamReader(stream)));
            }
        }
    }
}