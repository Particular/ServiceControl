namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NServiceBus.Features;
    using NServiceBus.Unicast.Subscriptions;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;

    public class When_an_event_is_enabled_for_external_integrations : AcceptanceTest
    {
        [Test]
        public void Should_be_published_on_the_bus()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(b => b.Given((bus, c) => Subscriptions.OnEndpointSubscribed(s =>
                {
                    if (s.SubscriberReturnAddress.Queue.Contains("ExternalProcessor"))
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                })).AppConfig(PathToAppConfig))
                .WithEndpoint<FailingReceiver>(b => b.When(c => c.ExternalProcessorSubscribed, bus => bus.SendLocal(new MyMessage())))
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) => bus.Subscribe<ServiceControl.Contracts.Failures.MessageFailed>()))
                .Done(c => c.MessageDelivered)
                .Run();

            Assert.IsTrue(context.MessageDelivered);
            Assert.AreEqual(context.MessageId, context.MessageIdDeliveredToExternalProcessor);
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
                    Context.EndpointName = Configure.EndpointName;
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JSonServer>();
            }

            public class FailureHandler : IHandleMessages<ServiceControl.Contracts.Failures.MessageFailed>
            {
                public MyContext Context { get; set; }

                public void Handle(ServiceControl.Contracts.Failures.MessageFailed message)
                {
                    Context.MessageIdDeliveredToExternalProcessor = message.MessageId;
                    Context.MessageDelivered = true;
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

        public class JSonServer : DefaultServer
        {
            public override void SetSerializer(Configure configure)
            {
                Configure.Serialization.Json();
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public bool MessageDelivered { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }

            public string MessageId { get; set; }
            public string EndpointName { get; set; }

            public string MessageIdDeliveredToExternalProcessor { get; set; }
            public string EndpointNameDeliveredToExternalProcessor { get; set; }

            public string UniqueMessageId
            {
                get
                {
                    return DeterministicGuid.MakeId(MessageId, EndpointName).ToString();
                }
            }
        }
    }
}