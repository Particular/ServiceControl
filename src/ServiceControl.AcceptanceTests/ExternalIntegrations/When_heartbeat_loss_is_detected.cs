namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NUnit.Framework;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.HeartbeatMonitoring;
    using ServiceControl.Contracts.Operations;


    /// <summary>
    /// The test simulates the heartbeat subsystem by publishing EndpointFailedToHeartbeat event.
    /// </summary>
    [TestFixture]
    public class When_heartbeat_loss_is_detected : AcceptanceTest
    {
        [Test]
        public void Notification_is_published_on_a_bus()
        {
            var context = new MyContext();

            CustomConfiguration = config => config.OnEndpointSubscribed(s =>
            {
                if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                {
                    context.ExternalProcessorSubscribed = true;
                }
            });

            ExecuteWhen(() => context.ExternalProcessorSubscribed, bus => bus.Publish(new EndpointFailedToHeartbeat
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

            Define(context)
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) =>
                {
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

            public class FailureHandler : IHandleMessages<HeartbeatStopped>
            {
                public MyContext Context { get; set; }

                public void Handle(HeartbeatStopped message)
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