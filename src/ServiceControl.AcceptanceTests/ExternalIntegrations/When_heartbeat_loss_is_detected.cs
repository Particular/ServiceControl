namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
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

            Scenario.Define(context)
                .WithEndpoint<ExternalIntegrationsManagementEndpoint>(b => b.When(c => c.ExternalProcessorSubscribed, bus => bus.Publish(new EndpointFailedToHeartbeat
                {
                    DetectedAt = new DateTime(2013, 09, 13, 13, 14, 13),
                    LastReceivedAt = new DateTime(2013, 09, 13, 13, 13, 13),
                    Endpoint = new EndpointDetails
                    {
                        Host = "UnluckyHost",
                        HostId = Guid.NewGuid(),
                        Name = "UnluckyEndpoint"
                    }

                })).AppConfig(PathToAppConfig))
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) =>
                {
                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                        return;
                    }
                    bus.Subscribe<HeartbeatStopped>();
                }))
                .Done(c => c.NotificationDelivered)
                .Run();

            Assert.IsTrue(context.NotificationDelivered);
        }

        public class ExternalIntegrationsManagementEndpoint : EndpointConfigurationBuilder
        {
            public ExternalIntegrationsManagementEndpoint()
            {
                EndpointSetup<ExternalIntegrationsManagementEndpointSetup>(b => b.OnEndpointSubscribed<MyContext>((s, context) =>
                {
                    if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                    {
                        context.ExternalProcessorSubscribed = true;
                    }
                }));
            }
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