namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Raw;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using NUnit.Framework.Internal;
    using ServiceControl.Transports;

    [TestFixture]
    class TransportTestFixture
    {
        [SetUp]
        public virtual Task Setup()
        {
            configuration = new TransportTestsConfiguration();
            testCancellationTokenSource = Debugger.IsAttached ? new CancellationTokenSource() : new CancellationTokenSource(TestTimeout);
            registrations = new List<CancellationTokenRegistration>();

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
        }

        protected string GetTestQueueName(string name)
        {
            return $"{name}-{System.IO.Path.GetRandomFileName().Replace(".", string.Empty)}";
        }

        protected TaskCompletionSource<TResult> CreateTaskCompletionSource<TResult>()
        {
            var source = new TaskCompletionSource<TResult>();

            if (!Debugger.IsAttached)
            {
                var tokenRegistration = testCancellationTokenSource.Token
                    .Register(state => ((TaskCompletionSource<TResult>)state).TrySetException(new Exception("The test timed out.")), source);
                registrations.Add(tokenRegistration);
            }

            return source;
        }
        protected TransportTestsConfiguration configuration;

        protected async Task<IDispatchMessages> StartQueueLengthProvider(string queueName, Action<QueueLengthEntry> onQueueLengthReported)
        {
            var rawEndpoint = await CreateTestDispatcher(queueName);

            queueLengthProvider = configuration.TransportCustomization.CreateQueueLengthProvider();

            queueLengthProvider.Initialize(configuration.ConnectionString, (qlt, _) => onQueueLengthReported(qlt.First()));

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(queueName, queueName));

            await queueLengthProvider.Start();

            return rawEndpoint;
        }

        protected async Task<IDispatchMessages> StartQueueIngestor(
            string queueName,
            Func<MessageContext, Task> onMessage,
            Func<ErrorContext, Task<ErrorHandleResult>> onError)
        {
            var rawEndpoint = await CreateTestDispatcher(queueName);

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

            return rawEndpoint;
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
        protected Task<IDispatchMessages> CreateDispatcher(string enpointName)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = enpointName,
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            return configuration.TransportCustomization.InitializeDispatcher(enpointName, transportSettings);
        }

        async Task<IDispatchMessages> CreateTestDispatcher(string queueName)
        {
            var transportSettings = new TransportSettings
            {
                EndpointName = queueName,
                ConnectionString = configuration.ConnectionString,
                MaxConcurrency = 1
            };

            var endpointForTesting = RawEndpointConfiguration.Create(queueName, (_, __) => throw new NotImplementedException(), transportSettings.ErrorQueue);

            endpointForTesting.AutoCreateQueues(new string[0]);
            configuration.TransportCustomization.CustomizeForQueueIngestion(endpointForTesting, transportSettings);

            return await RawEndpoint.Create(endpointForTesting);
        }

        protected static TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

        CancellationTokenSource testCancellationTokenSource;
        List<CancellationTokenRegistration> registrations;
        IProvideQueueLength queueLengthProvider;
        IQueueIngestor queueIngestor;
    }
}