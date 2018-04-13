namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NUnit.Framework;
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
            var context = new MyContext();

            CustomConfiguration = config => config.OnEndpointSubscribed(s =>
            {
                if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                {
                    context.ExternalProcessorSubscribed = true;
                }
            });

            ExecuteWhen(() => context.ExternalProcessorSubscribed, domainEvents => domainEvents.Raise(new EndpointHeartbeatRestored
            {
                RestoredAt = new DateTime(2013, 09, 13, 13, 15, 13),
                Endpoint = new EndpointDetails
                {
                    Host = "LuckyHost",
                    HostId = Guid.NewGuid(),
                    Name = "LuckyEndpoint"
                }

            }));

            await Define(context)
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) =>
                {
                    bus.Subscribe<HeartbeatRestored>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
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
                EndpointSetup<JsonServer>();
            }

            public class FailureHandler : IHandleMessages<HeartbeatRestored>
            {
                public MyContext Context { get; set; }

                public void Handle(HeartbeatRestored message)
                {
                    Context.NotificationDelivered = true;
                }
            }

            public class UnicastOverride : IProvideConfiguration<UnicastBusConfig>
            {
                public UnicastBusConfig GetConfiguration()
                {
                    var config = new UnicastBusConfig();
                    var serviceControlMapping = new MessageEndpointMapping
                    {
                        AssemblyName = "ServiceControl.Contracts",
                        Endpoint = "Particular.ServiceControl"
                    };
                    config.MessageEndpointMappings.Add(serviceControlMapping);
                    return config;
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool NotificationDelivered { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
        }
    }
}