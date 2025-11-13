namespace ServiceControl.AcceptanceTests.Monitoring.ExternalIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;

    /// <summary>
    /// The test simulates the heartbeat subsystem by publishing EndpointHeartbeatRestored event.
    /// </summary>
    [TestFixture]
    class When_heartbeat_is_restored : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            var externalProcessorSubscribed = false;

            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    externalProcessorSubscribed = true;
                }
            });

            ExecuteWhen(() => externalProcessorSubscribed, domainEvents => domainEvents.Raise(new EndpointHeartbeatRestored
            {
                RestoredAt = new DateTime(2013, 09, 13, 13, 15, 13),
                Endpoint = new EndpointDetails
                {
                    Host = "LuckyHost",
                    HostId = Guid.NewGuid(),
                    Name = "LuckyEndpoint"
                }
            }));

            var context = await Define<MyContext>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<HeartbeatRestored>();

                    if (c.HasNativePubSubSupport)
                    {
                        externalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.NotificationDelivered)
                .Run();

            Assert.That(context.NotificationDelivered, Is.True);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, PrimaryOptions.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<HeartbeatRestored>(PrimaryOptions.DEFAULT_INSTANCE_NAME); });

            public class FailureHandler(MyContext testContext) : IHandleMessages<HeartbeatRestored>
            {
                public Task Handle(HeartbeatRestored message, IMessageHandlerContext context)
                {
                    testContext.NotificationDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool NotificationDelivered { get; set; }
        }
    }
}