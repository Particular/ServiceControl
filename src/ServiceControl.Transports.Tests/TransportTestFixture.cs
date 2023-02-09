namespace ServiceControl.Transport.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
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
                await queueLengthProvider.Stop().ConfigureAwait(false);
            }

            if (configuration != null)
            {
                await configuration.Cleanup().ConfigureAwait(false);
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
            var endpointForTesting = RawEndpointConfiguration.Create(queueName, (_, __) => throw new NotImplementedException(), $"{queueName}error");

            endpointForTesting.AutoCreateQueues(new string[0]);
            configuration.ApplyTransportConfig(endpointForTesting);

            var rawEndpointForQueueLengthTesting = await RawEndpoint.Create(endpointForTesting).ConfigureAwait(false);

            queueLengthProvider = configuration.InitializeQueueLengthProvider((qlt, _) => onQueueLengthReported(qlt.First()));

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(queueName, queueName));

            await queueLengthProvider.Start().ConfigureAwait(false);

            return rawEndpointForQueueLengthTesting;
        }

        protected static TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        CancellationTokenSource testCancellationTokenSource;
        List<CancellationTokenRegistration> registrations;
        IProvideQueueLength queueLengthProvider;
    }
}