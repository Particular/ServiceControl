namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts;
    using Contracts.HeartbeatMonitoring;
    using ExternalIntegrations;
    using Infrastructure.DomainEvents;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;

    /// <summary>
    /// The test simulates the heartbeat subsystem by publishing EndpointFailedToHeartbeat event.
    /// </summary>
    [TestFixture]
    class When_encountered_an_error : AcceptanceTest
    {
        [Test]
        public async Task Should_restart_dispatch_thread()
        {
            var externalProcessorSubscribed = false;

            CustomConfiguration = config =>
            {
                config.OnEndpointSubscribed<MyContext>((s, ctx) =>
                {
                    if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        externalProcessorSubscribed = true;
                    }
                });

                config.RegisterComponents(services => services.AddSingleton<IEventPublisher, FaultyPublisher>());
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
            }));

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

            Assert.Multiple(() =>
            {
                Assert.That(context.NotificationDelivered, Is.True);
                Assert.That(context.Failed, Is.True);
            });
        }

        class FaultyPublisher(MyContext context) : IEventPublisher
        {
            public bool Handles(IDomainEvent @event) => false;

            public object CreateDispatchContext(IDomainEvent @event) => null;

            public Task<IEnumerable<object>> PublishEventsForOwnContexts(IEnumerable<object> allContexts)
            {
                if (!failed)
                {
                    failed = true;
                    context.Failed = true;
                    throw new Exception("Simulated exception");
                }

                return Task.FromResult(Enumerable.Empty<object>());
            }

            bool failed;
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, PrimaryOptions.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<HeartbeatStopped>(PrimaryOptions.DEFAULT_INSTANCE_NAME); });

            public class FailureHandler(MyContext testContext) : IHandleMessages<HeartbeatStopped>
            {
                public Task Handle(HeartbeatStopped message, IMessageHandlerContext context)
                {
                    testContext.NotificationDelivered = true;
                    return Task.CompletedTask;
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