namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NUnit.Framework;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.Operations;

    [TestFixture]
    public class When_a_message_has_custom_checks : AcceptanceTest
    {
        [Test]
        public void Notification_should_be_published_on_the_bus()
        {
            var context = new MyContext();

            CustomConfiguration = config => config.OnEndpointSubscribed(s =>
            {
                if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                {
                    context.ExternalProcessorSubscribed = true;
                }
            });

            ExecuteWhen(() => context.ExternalProcessorSubscribed, bus =>
            {
                bus.Publish(new ServiceControl.Contracts.CustomChecks.CustomCheckSucceeded
                {
                    Category = "Testing",
                    CustomCheckId = "Success custom check",
                    OriginatingEndpoint = new EndpointDetails
                    {
                        Host = "MyHost",
                        HostId = Guid.Empty,
                        Name = "Testing"
                    },
                    SucceededAt = DateTime.Now,
                });
                bus.Publish(new ServiceControl.Contracts.CustomChecks.CustomCheckFailed
                {
                    Category = "Testing",
                    CustomCheckId = "Fail custom check",
                    OriginatingEndpoint = new EndpointDetails
                    {
                        Host = "MyHost",
                        HostId = Guid.Empty,
                        Name = "Testing"
                    },
                    FailedAt = DateTime.Now,
                    FailureReason = "Because I can",
                });
            });

            Define(context)
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) =>
                {
                    bus.Subscribe<CustomCheckSucceeded>();
                    bus.Subscribe<CustomCheckFailed>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.CustomCheckFailedReceived && c.CustomCheckSucceededReceived)
                .Run();

            Assert.IsTrue(context.CustomCheckFailedReceived);
            Assert.IsTrue(context.CustomCheckSucceededReceived);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JsonServer>();
            }

            public class CustomCheckSucceededHandler : IHandleMessages<CustomCheckSucceeded>
            {
                public MyContext Context { get; set; }

                public void Handle(CustomCheckSucceeded message)
                {
                    Context.CustomCheckSucceededReceived = true;
                }
            }

            public class CustomCheckFailedHandler : IHandleMessages<CustomCheckFailed>
            {
                public MyContext Context { get; set; }

                public void Handle(CustomCheckFailed message)
                {
                    Context.CustomCheckFailedReceived = true;
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
            public bool CustomCheckSucceededReceived { get; set; }
            public bool CustomCheckFailedReceived { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
        }
    }
}