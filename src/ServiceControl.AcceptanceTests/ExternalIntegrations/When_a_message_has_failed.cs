namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
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
                .WithEndpoint<FailingReceiver>(b => b.When(c => c.ExternalProcessorSubscribed, bus => bus.SendLocal(new MyMessage())))
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) => bus.Subscribe<ServiceControl.Contracts.Failures.MessageFailed>()))
                .Done(c => c.MessagesDelivered == 1)
                .Run();
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
                        bus.SendLocal(new MyMessage());
                    }
                }))
                .WithEndpoint<ExternalProcessor>(b => b.Given((bus, c) => bus.Subscribe<ServiceControl.Contracts.Failures.MessageFailed>()))
                .Done(c => c.LastEventDeliveredAt.HasValue && c.LastEventDeliveredAt.Value.Add(TimeSpan.FromSeconds(10)) < DateTime.Now) //Wait 10 seconds from last event
                .Run();

            Console.WriteLine("Delivered {0} messages", context.MessagesDelivered);
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
                    if (message.FailureDetails.Exception.Message != "Simulated exception")
                    {
                        return;
                    }
                    Context.MessageIdDeliveredToExternalProcessor = message.ProcessingDetails.MessageId;
                    Context.MessagesDelivered++;
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
            public int MessagesDelivered { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }

            public string MessageId { get; set; }
            public string EndpointName { get; set; }
            public DateTime? LastEventDeliveredAt { get; set; }

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