namespace ServiceControl.Transport.Tests
{
    using System.Collections.Generic;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Transports;
    using NUnit.Framework.Internal;

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

        protected async Task StartQueueLengthProvider(string queueName, Action<QueueLengthEntry> onQueueLengthReported)
        {
            queueLengthProvider = configuration.InitializeQueueLengthProvider(queueName, onQueueLengthReported);

            queueLengthProvider.TrackEndpointInputQueue(new EndpointToQueueMapping(queueName, queueName));

            await queueLengthProvider.Start().ConfigureAwait(false);
        }

        protected static TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

        CancellationTokenSource testCancellationTokenSource;
        List<CancellationTokenRegistration> registrations;
        IProvideQueueLength queueLengthProvider;
    }
}