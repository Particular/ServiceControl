namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using ServiceControl.Transports;

    [TestFixture]
    class TransportTestFixture
    {
        [OneTimeSetUp]
        public static void OneTimeSetup() => Scenario.GetLoggerFactory = ctx => new StaticLoggerFactory(ctx);

        [SetUp]
        public virtual Task Setup()
        {
            configuration = new TransportTestsConfiguration();
            testCancellationTokenSource = Debugger.IsAttached ? new CancellationTokenSource() : new CancellationTokenSource(TestTimeout);
            registrations = new List<CancellationTokenRegistration>();
            QueueSuffix = $"-{System.IO.Path.GetRandomFileName().Replace(".", string.Empty)}";

            Conventions.EndpointNamingConvention = t =>
            {
                var classAndEndpoint = t.FullName.Split('.').Last();

                var endpointBuilder = classAndEndpoint.Split('+').Last();

                return endpointBuilder + QueueSuffix;
            };

#if !NETCOREAPP2_0 // To make the SQS tests work
            ConfigurationManager.GetSection("X");
#endif

            return configuration.Configure();
        }

        [TearDown]
        public virtual async Task Cleanup()
        {
            if (queueLengthProvider != null)
            {
                await queueLengthProvider.Stop();
            }

            if (queueIngestor != null)
            {
                await queueIngestor.Stop();
            }

            if (configuration != null)
            {
                await configuration.Cleanup();
            }

            testCancellationTokenSource.Dispose();
        }

        protected string QueueSuffix { get; private set; }

        protected string GetTestQueueName(string name) => $"{name}-{QueueSuffix}";

        protected TaskCompletionSource<TResult> CreateTaskCompletionSource<TResult>()
        {
            var source = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!Debugger.IsAttached)
            {
                var tokenRegistration = testCancellationTokenSource.Token
                    .Register(state => ((TaskCompletionSource<TResult>)state).TrySetException(new Exception("The test timed out.")), source);
                registrations.Add(tokenRegistration);
            }

            return source;
        }
        protected TransportTestsConfiguration configuration;

        protected Task StartQueueLengthProvider(string queueName, Action<QueueLengthEntry> onQueueLengthReported)
        {
            queueLengthProvider = configuration.TransportCustomization.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(configuration.ConnectionString, (qlt, _) => onQueueLengthReported(qlt.First()));

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(queueName, queueName));

            return queueLengthProvider.Start();
        }

        protected async Task StartQueueIngestor(
            string queueName,
            OnMessage onMessage,
            OnError onError)
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = queueName,
                MaxConcurrency = 1
            };

            queueIngestor = await configuration.TransportCustomization.InitializeQueueIngestor(
                queueName,
                transportSettings,
                onMessage,
                onError,
                (_, __) =>
                {
                    Assert.Fail("There should be no critical errors");
                    return Task.CompletedTask;
                });

            await queueIngestor.Start();
        }

        protected Task ProvisionQueues(string username, string queueName, string errorQueue, IEnumerable<string> additionalQueues)
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = queueName,
                ErrorQueue = errorQueue,
                MaxConcurrency = 1
            };

            return configuration.TransportCustomization.ProvisionQueues(username, transportSettings, additionalQueues);
        }
        protected Task<IMessageDispatcher> CreateDispatcher(string endpointName)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = endpointName,
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            return configuration.TransportCustomization.InitializeDispatcher(endpointName, transportSettings);
        }

        protected Task CreateTestQueue(string queueName)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = queueName,
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            return configuration.TransportCustomization.ProvisionQueues(WindowsIdentity.GetCurrent().Name, transportSettings, new List<string>());
        }

        protected static TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

        CancellationTokenSource testCancellationTokenSource;
        List<CancellationTokenRegistration> registrations;
        IProvideQueueLength queueLengthProvider;
        IQueueIngestor queueIngestor;
    }
}