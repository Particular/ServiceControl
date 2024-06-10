namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Logging;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using ServiceControl.Transports;

    [TestFixture]
    class TransportTestFixture
    {
        [SetUp]
        public virtual async Task Setup()
        {
            LogManager.UseFactory(new TestContextAppenderFactory());
            configuration = new TransportTestsConfiguration();
            testCancellationTokenSource = Debugger.IsAttached ? new CancellationTokenSource() : new CancellationTokenSource(TestTimeout);
            registrations = [];
            QueueSuffix = $"-{System.IO.Path.GetRandomFileName().Replace(".", string.Empty)}";

            Conventions.EndpointNamingConvention = t =>
            {
                var classAndEndpoint = t.FullName.Split('.').Last();

                var endpointBuilder = classAndEndpoint.Split('+').Last();

                return endpointBuilder + QueueSuffix;
            };

            await configuration.Configure();

            dispatcherTransportInfrastructure = await CreateDispatcherTransportInfrastructure();
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
                await queueIngestor.StopReceive();
            }

            if (transportInfrastructure != null)
            {
                await transportInfrastructure.Shutdown();
            }

            await dispatcherTransportInfrastructure.Shutdown();

            if (configuration != null)
            {
                await configuration.Cleanup();
            }

            testCancellationTokenSource.Dispose();
        }

        protected IMessageDispatcher Dispatcher => dispatcherTransportInfrastructure.Dispatcher;
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

            transportInfrastructure = await configuration.TransportCustomization.CreateTransportInfrastructure(
                queueName,
                transportSettings,
                onMessage,
                onError,
                (_, __) =>
                {
                    Assert.Fail("There should be no critical errors");
                    return Task.CompletedTask;
                });

            queueIngestor = transportInfrastructure.Receivers[queueName];

            await queueIngestor.StartReceive();
        }

        protected Task ProvisionQueues(string queueName, string errorQueue, IEnumerable<string> additionalQueues)
        {
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = queueName,
                ErrorQueue = errorQueue,
                MaxConcurrency = 1
            };

            return configuration.TransportCustomization.ProvisionQueues(transportSettings, additionalQueues);
        }

        protected Task CreateTestQueue(string queueName)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = queueName,
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            return configuration.TransportCustomization.ProvisionQueues(transportSettings, []);
        }

        protected static TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

        async Task<TransportInfrastructure> CreateDispatcherTransportInfrastructure()
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = "TransportTestDispatcher",
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            return await configuration.TransportCustomization.CreateTransportInfrastructure("TransportTestDispatcher", transportSettings);
        }

        CancellationTokenSource testCancellationTokenSource;
        List<CancellationTokenRegistration> registrations;
        IProvideQueueLength queueLengthProvider;
        IMessageReceiver queueIngestor;
        TransportInfrastructure transportInfrastructure;
        TransportInfrastructure dispatcherTransportInfrastructure;
    }
}