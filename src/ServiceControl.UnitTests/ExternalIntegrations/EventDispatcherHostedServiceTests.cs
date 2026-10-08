namespace ServiceControl.UnitTests.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using NServiceBus.Testing;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Persistence;
    using ServiceControl.UnitTests.Operations;

    [TestFixture]
    public class EventDispatcherHostedServiceTests
    {
        [Test]
        public async Task Events_are_dispatched_to_sinks_and_published()
        {
            var (dispatch, sink, session) = await Start(disablePublishing: false, "event-1", "event-2");

            await dispatch(["context"], CancellationToken.None);

            Assert.That(sink.Batches.Single(), Is.EqualTo(new[] { "event-1", "event-2" }));
            Assert.That(session.PublishedMessages.Select(m => m.Message), Is.EqualTo(new[] { "event-1", "event-2" }));
        }

        [Test]
        public async Task Events_are_dispatched_to_sinks_when_bus_publishing_is_disabled()
        {
            var (dispatch, sink, session) = await Start(disablePublishing: true, "event-1");

            await dispatch(["context"], CancellationToken.None);

            Assert.That(sink.Batches.Single(), Is.EqualTo(new[] { "event-1" }));
            Assert.That(session.PublishedMessages, Is.Empty);
        }

        [Test]
        public async Task Sinks_are_not_called_without_events()
        {
            var (dispatch, sink, _) = await Start(disablePublishing: false);

            await dispatch(["context"], CancellationToken.None);

            Assert.That(sink.Batches, Is.Empty);
        }

        [Test]
        public async Task Sink_failures_propagate_so_the_batch_is_retried_before_anything_is_published()
        {
            var (dispatch, sink, session) = await Start(disablePublishing: false, "event-1");
            sink.Failure = new InvalidOperationException("database unavailable");

            Assert.ThrowsAsync<InvalidOperationException>(() => dispatch(["context"], CancellationToken.None));
            Assert.That(session.PublishedMessages, Is.Empty);
        }

        static async Task<(Func<object[], CancellationToken, Task> Dispatch, RecordingSink Sink, TestableMessageSession Session)> Start(bool disablePublishing, params object[] events)
        {
            var store = new CapturingStore();
            var sink = new RecordingSink();
            var session = new TestableMessageSession();
            var service = new EventDispatcherHostedService(
                store,
                new FakeDomainEvents(),
                [new StaticPublisher(events)],
                [sink],
                session,
                new Settings { DisableExternalIntegrationsPublishing = disablePublishing },
                NullLogger<EventDispatcherHostedService>.Instance);

            await service.StartAsync();

            return (store.Callback, sink, session);
        }

        class CapturingStore : IExternalIntegrationRequestsDataStore
        {
            public Func<object[], CancellationToken, Task> Callback { get; private set; }

            public void Subscribe(Func<object[], CancellationToken, Task> callback) => Callback = callback;

            public Task StoreDispatchRequest(IEnumerable<ExternalIntegrationDispatchRequest> dispatchRequests, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        class StaticPublisher(object[] events) : IEventPublisher
        {
            public bool Handles(IDomainEvent @event) => false;

            public object CreateDispatchContext(IDomainEvent @event) => null;

            public Task<IEnumerable<object>> PublishEventsForOwnContexts(IEnumerable<object> allContexts, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<object>>(events);
        }

        class RecordingSink : IIntegrationEventSink
        {
            public List<object[]> Batches { get; } = [];

            public Exception Failure { get; set; }

            public Task Dispatch(IReadOnlyCollection<object> integrationEvents, CancellationToken cancellationToken = default)
            {
                if (Failure != null)
                {
                    throw Failure;
                }

                Batches.Add([.. integrationEvents]);
                return Task.CompletedTask;
            }
        }
    }
}
