namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using Raven.Client;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.ExternalIntegrations;
    using ServiceControl.Infrastructure.DomainEvents;


    /// <summary>
    /// The test simulates the heartbeat subsystem by publishing EndpointFailedToHeartbeat event.
    /// </summary>
    [TestFixture]
    public class When_encountered_an_error : AcceptanceTest
    {
        [Test]
        public async Task Dispatched_thread_is_restarted()
        {
            var externalProcessorSubscribed = false;

            CustomConfiguration = config =>
            {
                config.OnEndpointSubscribed<MyContext>((s, ctx) =>
                {
                    if (s.SubscriberReturnAddress.Contains("ExternalProcessor"))
                    {
                        externalProcessorSubscribed = true;
                    }
                });

                config.RegisterComponents(cc => cc.ConfigureComponent<FaultyPublisher>(DependencyLifecycle.SingleInstance));
            };

            ExecuteWhen(() => externalProcessorSubscribed, domainEvents => domainEvents.Raise(new EndpointFailedToHeartbeat
            {
                DetectedAt = new DateTime(2013, 09, 13, 13, 14, 13),
                LastReceivedAt = new DateTime(2013, 09, 13, 13, 13, 13),
                Endpoint = new EndpointDetails
                {
                    Host = "UnluckyHost",
                    HostId = Guid.NewGuid(),
                    Name = "UnluckyEndpoint"
                }

            }).GetAwaiter().GetResult());

            var context = await Define<MyContext>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (messageSession, c) =>
                {
                    await messageSession.Subscribe<HeartbeatStopped>();

                    if (c.HasNativePubSubSupport)
                    {
                        externalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.NotificationDelivered)
                .Run();

            Assert.IsTrue(context.NotificationDelivered);
            Assert.IsTrue(context.Failed);
        }

        private class FaultyPublisher : IEventPublisher
        {
            bool failed;

            public MyContext Context { get; set; }

            public bool Handles(IDomainEvent @event)
            {
                return false;
            }

            public object CreateDispatchContext(IDomainEvent @event)
            {
                return null;
            }

            public Task<IEnumerable<object>> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IAsyncDocumentSession session)
            {
                if (!failed)
                {
                    failed = true;
                    Context.Failed = true;
                    throw new Exception("Simulated exception");
                }

                return Task.FromResult(Enumerable.Empty<object>());
            }
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JsonServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata =>
                {
                    publisherMetadata.RegisterPublisherFor<HeartbeatStopped>(Settings.DEFAULT_SERVICE_NAME);
                });
            }

            public class FailureHandler : IHandleMessages<HeartbeatStopped>
            {
                public MyContext Context { get; set; }

                public Task Handle(HeartbeatStopped message, IMessageHandlerContext context)
                {
                    Context.NotificationDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool NotificationDelivered { get; set; }
            public bool Failed { get; set; }
        }
    }
}