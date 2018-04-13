namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NUnit.Framework;
    using Raven.Client;
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
            var context = new MyContext();

            CustomConfiguration = config =>
            {
                config.OnEndpointSubscribed(s =>
                {
                    if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                    {
                        context.ExternalProcessorSubscribed = true;
                    }
                });

                config.RegisterComponents(cc => cc.ConfigureComponent<FaultyPublisher>(DependencyLifecycle.SingleInstance));
            };

            ExecuteWhen(() => context.ExternalProcessorSubscribed, domainEvents => domainEvents.Raise(new EndpointFailedToHeartbeat
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

            await Define(context)
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) =>
                {
                    bus.Subscribe<HeartbeatStopped>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
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

            public IEnumerable<object> PublishEventsForOwnContexts(IEnumerable<object> allContexts, IDocumentSession session)
            {
                if (!failed)
                {
                    failed = true;
                    Context.Failed = true;
                    throw new Exception("Simulated exception");
                }
                yield break;
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
            public bool Failed { get; set; }
        }
    }
}