namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Collections.Generic;
    using Contexts;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using ServiceControl.Contracts;

    [TestFixture]
    public class When_a_message_has_failed : AcceptanceTest
    {

        [Test]
        public void Notification_should_be_published_on_the_bus()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ExternalIntegrationsManagementEndpoint>(b => b.Given((bus, c) => Subscriptions.OnEndpointSubscribed(s =>
                {
                    if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                })).AppConfig(PathToAppConfig))
                .WithEndpoint<FailingReceiver>(b => b.When(c => c.ExternalProcessorSubscribed, bus => bus.SendLocal(new MyMessage { Body = "Faulty message" })))
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) => bus.Subscribe<HeartbeatStopped>()))
                .Done(c => c.EventsDelivered.Count >= 1)
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<MessageFailed>(context.EventsDelivered[0]);

            Assert.AreEqual("Faulty message", deserializedEvent.FailureDetails.Exception.Message);
            //These are important so check it they are set
            Assert.IsNotNull(deserializedEvent.MessageDetails.MessageId);
            Assert.IsNotNull(deserializedEvent.SendingEndpoint.Name);
            Assert.IsNotNull(deserializedEvent.ProcessingEndpoint.Name);
        }

        [Test]
        [Explicit]
        public void Performance_test()
        {
            const int MessageCount = 100;

            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ExternalIntegrationsManagementEndpoint>(b => b.Given((bus, c) => Subscriptions.OnEndpointSubscribed(s =>
                {
                    if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                })).AppConfig(PathToAppConfig))
                .WithEndpoint<FailingReceiver>(b => b.When(c => c.ExternalProcessorSubscribed, bus =>
                {
                    for (var i = 0; i < MessageCount; i++)
                    {
                        bus.SendLocal(new MyMessage { Body = i.ToString() });
                    }
                }))
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) => bus.Subscribe<MessageFailed>()))
                .Done(c => c.LastEventDeliveredAt.HasValue && c.LastEventDeliveredAt.Value.Add(TimeSpan.FromSeconds(10)) < DateTime.Now) //Wait 10 seconds from last event
                .Run();

            Console.WriteLine("Delivered {0} messages", context.EventsDelivered.Count);
        }


        [Serializable]
        public class Subscriptions
        {
            public static Action<Action<SubscriptionEventArgs>> OnEndpointSubscribed = actionToPerform =>
            {
                if (Feature.IsEnabled<MessageDrivenSubscriptions>())
                {
                    Configure.Instance.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed +=
                        (sender, args) =>
                        {
                            actionToPerform(args);
                        };
                }
            };

        }

        public class ExternalIntegrationsManagementEndpoint : EndpointConfigurationBuilder
        {
            public ExternalIntegrationsManagementEndpoint()
            {
                EndpointSetup<ExternalIntegrationsManagementEndpointSetup>();
            }
        }

        public class FailingReceiver : EndpointConfigurationBuilder
        {
            public FailingReceiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => Configure.Features.Disable<SecondLevelRetries>());
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    throw new Exception(message.Body);
                }
            }
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JSonServer>();
            }

            public class FailureHandler : IHandleMessages<MessageFailed>
            {
                public MyContext Context { get; set; }

                public void Handle(MessageFailed message)
                {
                    var serialized = JsonConvert.SerializeObject(message);
                    Context.RegisteredDeliveredEvent(serialized);
                    Context.LastEventDeliveredAt = DateTime.Now;
                }
            }

            public class UnicastOverride : IProvideConfiguration<UnicastBusConfig>
            {
                public UnicastBusConfig GetConfiguration()
                {
                    var config = new UnicastBusConfig();
                    var serviceControlMapping = new MessageEndpointMapping
                    {
                        Messages = "ServiceControl.Contracts",
                        Endpoint = "Particular.ServiceControl"
                    };
                    config.MessageEndpointMappings.Add(serviceControlMapping);
                    return config;
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public string Body { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            private readonly  List<string> eventsDelivered = new List<string>();
            public bool ExternalProcessorSubscribed { get; set; }
            public DateTime? LastEventDeliveredAt { get; set; }

            public List<String> EventsDelivered
            {
                get { return eventsDelivered; }
            }

            public void RegisteredDeliveredEvent(string jsonSerializedEvent)
            {
                eventsDelivered.Add(jsonSerializedEvent);
            }
        }
    }
}