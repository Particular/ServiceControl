namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using Contexts;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
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
                .WithEndpoint<ExternalIntegrationsManagementEndpoint>(builder => builder.AppConfig(PathToAppConfig))
                .WithEndpoint<FailingReceiver>(b => b.When(c => c.ExternalProcessorSubscribed, bus => bus.SendLocal(new MyMessage { Body = "Faulty message" })))
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) =>
                {
                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                        return;
                    }

                    bus.Subscribe<MessageFailed>();
                }))
                .Done(c => c.EventDelivered)
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<MessageFailed>(context.Event);

            Assert.AreEqual("Faulty message", deserializedEvent.FailureDetails.Exception.Message);
            //These are important so check it they are set
            Assert.IsNotNull(deserializedEvent.MessageDetails.MessageId);
            Assert.IsNotNull(deserializedEvent.SendingEndpoint.Name);
            Assert.IsNotNull(deserializedEvent.ProcessingEndpoint.Name);
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

        public class FailingReceiver : EndpointConfigurationBuilder
        {
            public FailingReceiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    });
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
                EndpointSetup<JsonServer>();
            }

            public class FailureHandler : IHandleMessages<MessageFailed>
            {
                public MyContext Context { get; set; }

                public void Handle(MessageFailed message)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message);
                    Context.Event = serializedMessage;
                    Context.EventDelivered = true;
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

        [Serializable]
        public class MyMessage : ICommand
        {
            public string Body { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool ExternalProcessorSubscribed { get; set; }
            public string Event { get; set; }
            public bool EventDelivered { get; set; }
        }
    }
}