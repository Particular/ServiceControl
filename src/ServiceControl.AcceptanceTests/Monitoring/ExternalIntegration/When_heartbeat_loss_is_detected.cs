namespace ServiceControl.AcceptanceTests.Monitoring.ExternalIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Contracts;
    using Contracts.HeartbeatMonitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;
    using TestSupport.EndpointTemplates;

    /// <summary>
    /// The test simulates the heartbeat subsystem by publishing EndpointFailedToHeartbeat event.
    /// </summary>
    [TestFixture]

    class When_heartbeat_loss_is_detected : AcceptanceTest
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
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<HeartbeatStopped>();

                    if (c.HasNativePubSubSupport)
                    {
                        externalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.NotificationDelivered)
                .Run();

            Assert.IsTrue(context.NotificationDelivered);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<HeartbeatStopped>(Settings.DEFAULT_SERVICE_NAME); });

            public class FailureHandler : IHandleMessages<HeartbeatStopped>
            {
                readonly MyContext testContext;

                public FailureHandler(MyContext testContext) => this.testContext = testContext;

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
        }
    }
}