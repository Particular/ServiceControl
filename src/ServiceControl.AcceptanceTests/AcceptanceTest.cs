namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure;
    using Infrastructure.Settings;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.DomainEvents;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [TestFixture]
    public abstract class AcceptanceTest : IAcceptanceTestInfrastructureProvider
    {
        protected AcceptanceTest()
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            ServicePointManager.MaxServicePoints = int.MaxValue;
            ServicePointManager.UseNagleAlgorithm = false; // Improvement for small tcp packets traffic, get buffered up to 1/2-second. If your storage communication is for small (less than ~1400 byte) payloads, this setting should help (especially when dealing with things like Azure Queues, which tend to have very small messages).
            ServicePointManager.Expect100Continue = false; // This ensures tcp ports are free up quicker by the OS, prevents starvation of ports
            ServicePointManager.SetTcpKeepAlive(true, 5000, 1000); // This is good for Azure because it reuses connections
        }

        public Dictionary<string, HttpClient> HttpClients => instanceBehavior.HttpClients;
        public JsonSerializerSettings SerializerSettings => instanceBehavior.SerializerSettings;
        public Dictionary<string, Settings> SettingsPerInstance => instanceBehavior.SettingsPerInstance;
        public Dictionary<string, OwinHttpMessageHandler> Handlers => instanceBehavior.Handlers;
        public Dictionary<string, BusInstance> Busses => instanceBehavior.Busses;

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
            ConfigurationManager.GetSection("X");
#endif

            var logfilesPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "logs");
            Directory.CreateDirectory(logfilesPath);
            var logFile = Path.Combine(logfilesPath, $"{TestContext.CurrentContext.Test.ID}-{TestContext.CurrentContext.Test.Name}.txt");
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }

            textWriterTraceListener = new TextWriterTraceListener(logFile);
            Trace.Listeners.Add(textWriterTraceListener);

            Conventions.EndpointNamingConvention = t =>
            {
                var baseNs = typeof(AcceptanceTest).Namespace;
                var testName = GetType().Name;
                return t.FullName?.Replace($"{baseNs}.", string.Empty).Replace($"{testName}+", string.Empty);
            };
        }

        [TearDown]
        public void Teardown()
        {
            Trace.Listeners.Remove(textWriterTraceListener);
        }

        static void RemoveOtherTransportAssemblies(string name)
        {
            var assembly = Type.GetType(name, true).Assembly;

            var otherAssemblies = Directory.EnumerateFiles(Path.GetDirectoryName(assembly.Location), "ServiceControl.Transports.*.dll")
                .Where(transportAssembly => transportAssembly != assembly.Location);

            foreach (var transportAssembly in otherAssemblies)
            {
                File.Delete(transportAssembly);
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
            if (instanceNames.Length == 0)
            {
                instanceNames = defaultInstanceNames;
            }

            // once custom settings have been applied in the previous run we have to force a new instance
            if (forceRefresh || previousForceRefresh || currentInstanceNames != null && !currentInstanceNames.SequenceEqual(instanceNames))
            {
                Stop().GetAwaiter().GetResult();
                
                var transportToUse = (ITransportIntegration)TestSuiteConstraints.Current.CreateTransportConfiguration();
                TestContext.WriteLine("Refreshing Service Control Instances with:");
                TestContext.WriteLine($"- Transport: {transportToUse.Name}");
                TestContext.WriteLine($"- Instances: Previous '{string.Join(";", currentInstanceNames ?? Array.Empty<string>())}' / New '{string.Join(";", instanceNames)}'");
                TestContext.WriteLine($"- Refresh Flags: Previous '{previousForceRefresh}'/ ForceRefresh '{forceRefresh}'");

                RemoveOtherTransportAssemblies(transportToUse.TypeName);

                instanceBehavior = new ServiceControlComponentBehavior(instanceNames, transportToUse, s => SetSettings(s), (i, s) => SetInstanceSettings(i, s), s => CustomConfiguration(s), (i, c) => CustomInstanceConfiguration(i, c));
                currentInstanceNames = instanceNames;
            }

            previousForceRefresh = forceRefresh;
            forceRefresh = false;

            return Scenario.Define(contextInitializer).WithComponent(instanceBehavior);
        }

        protected void SetCustomConfiguration(Action<EndpointConfiguration> action)
        {
            forceRefresh = true;
            CustomConfiguration = action;
        }

        protected void SetCustomInstanceConfiguration(Action<string, EndpointConfiguration> action)
        {
            forceRefresh = true;
            CustomInstanceConfiguration = action;
        }

        protected void SetSetSettings(Action<Settings> action)
        {
            forceRefresh = true;
            SetSettings = action;
        }

        protected void SetSetInstanceSettings(Action<string, Settings> action)
        {
            forceRefresh = true;
            SetInstanceSettings = action;
        }

        public static async Task Stop()
        {
            if (instanceBehavior != null)
            {
                await instanceBehavior.Stop();
                instanceBehavior = null;
            }
        }

        Action<EndpointConfiguration> CustomConfiguration = _ => { };

        Action<string, EndpointConfiguration> CustomInstanceConfiguration = (i, c) => { };

        Action<Settings> SetSettings = _ => { };

        Action<string, Settings> SetInstanceSettings = (i, s) => { };

        TextWriterTraceListener textWriterTraceListener;

        static ServiceControlComponentBehavior instanceBehavior;

        static readonly string[] defaultInstanceNames = {Settings.DEFAULT_SERVICE_NAME};

        static bool forceRefresh = true;

        static bool previousForceRefresh;

        static string[] currentInstanceNames;
    }
}