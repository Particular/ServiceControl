namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Contracts.Operations;

    /// <summary>
    /// The test simulates the heartbeat subsystem by publishing EndpointHeartbeatRestored event.
    /// </summary>
    [TestFixture]
    public class When_heartbeat_is_restored : AcceptanceTest
    {
        [Test]
        public async Task Notification_is_published_on_a_bus()
        {
            var externalProcessorSubscribed = false;
            
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.Contains("ExternalProcessor"))
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

            }).GetAwaiter().GetResult());

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

            Assert.IsTrue(context.NotificationDelivered);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JsonServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                });
            }

            public class FailureHandler : IHandleMessages<HeartbeatRestored>
            {
                public MyContext Context { get; set; }

                public Task Handle(HeartbeatRestored message, IMessageHandlerContext context)
                {
                    Context.NotificationDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool NotificationDelivered { get; set; }
        }
    }
}