namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.Transport;
    using NUnit.Framework;
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
            queueSuffix = $"-{System.IO.Path.GetRandomFileName().Replace(".", string.Empty)}";

            await configuration.Configure();

            dispatcherTransportInfrastructure = await CreateDispatcherTransportInfrastructure();
        }

        [TearDown]
        public virtual async Task Cleanup()
        {
            if (queueLengthProvider != null)
            {
                await queueLengthProvider.StopAsync(CancellationToken.None);
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

        protected string GetTestQueueName(string name) => $"{name}-{queueSuffix}";

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

        protected async Task<IAsyncDisposable> StartQueueLengthProvider(string queueName, Action<QueueLengthEntry> onQueueLengthReported)
        {
            // The transport test fixture abuses the transport seam as if it was stateless can but it isn't really
            // currently working around by creating a service collection per start call and then disposing the provider
            // as part of the method scope. This could lead to potential problems later once we add disposable resources
            // but this code probably requires a major overhaul anyway.
            var serviceCollection = new ServiceCollection();
            var transportSettings = new TransportSettings
            {
                ConnectionString = configuration.ConnectionString,
                EndpointName = queueName,
                MaxConcurrency = 1
            };
            configuration.TransportCustomization.AddTransportForMonitoring(serviceCollection, transportSettings);
            serviceCollection.AddSingleton<Action<QueueLengthEntry[], EndpointToQueueMapping>>((qlt, _) =>
                onQueueLengthReported(qlt.First()));
            var serviceProvider = serviceCollection.BuildServiceProvider();
            queueLengthProvider = serviceProvider.GetRequiredService<IProvideQueueLength>();

            await queueLengthProvider.StartAsync(CancellationToken.None);

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(queueName, queueName));

            return new QueueLengthProviderScope(serviceProvider);
        }

        sealed class QueueLengthProviderScope(ServiceProvider serviceProvider) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync()
            {
                await serviceProvider.GetRequiredService<IProvideQueueLength>().StopAsync(CancellationToken.None);
                await serviceProvider.DisposeAsync();
            }
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

        string queueSuffix;
        CancellationTokenSource testCancellationTokenSource;
        List<CancellationTokenRegistration> registrations;
        IProvideQueueLength queueLengthProvider;
        IMessageReceiver queueIngestor;
        TransportInfrastructure transportInfrastructure;
        TransportInfrastructure dispatcherTransportInfrastructure;
    }
}