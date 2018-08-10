namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Logging;
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

        public Dictionary<string, ServiceControlInstanceReference> Instances => serviceControlRunnerBehavior.ServiceControlInstances;

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

            var transportToUse = (ITransportIntegration)TestSuiteConstraints.Current.CreateTransportConfiguration();
            TestContext.WriteLine($"Using transport {transportToUse.Name}");

            serviceControlRunnerBehavior = new ServiceControlComponentBehavior(transportToUse, s => SetSettings(s), (i, s) => SetInstanceSettings(i, s), s => CustomConfiguration(s), (i, c) => CustomInstanceConfiguration(i, c));

            RemoveOtherTransportAssemblies(transportToUse.TypeName);

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

        void RemoveOtherTransportAssemblies(string name)
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

                await action(null);
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

        protected Action<EndpointConfiguration> CustomConfiguration = _ => { };
        protected Action<string, EndpointConfiguration> CustomInstanceConfiguration = (i, c) => { };
        protected Action<Settings> SetSettings = _ => { };
        protected Action<string, Settings> SetInstanceSettings = (i, s) => { };
        ServiceControlComponentBehavior serviceControlRunnerBehavior;
        TextWriterTraceListener textWriterTraceListener;
    }

    class StaticLoggerFactory : ILoggerFactory
    {
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

        public static ScenarioContext CurrentContext;
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
                Endpoint = (string)typeof(ScenarioContext).GetProperty("CurrentEndpoint", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(StaticLoggerFactory.CurrentContext),
                Level = messageSeverity,
                Message = message
            });
        }
    }
}