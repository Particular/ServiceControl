namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;

    public class When_a_message_is_imported_twice : AcceptanceTest
    {
        [Test]
        public void Should_register_a_new_endpoint()
        {
            var context = new MyContext();
            EndpointsView auditedMessage = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(c =>
                {
                    if (
                        !TryGetSingle("/api/endpoints", out auditedMessage,
                            m => m.Name == c.EndpointNameOfSendingEndpoint))
                    {
                        return false;
                    }

                    return true;

                })
                .Run(TimeSpan.FromSeconds(30));

            Assert.AreEqual(context.EndpointNameOfSendingEndpoint, auditedMessage.Name);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>()
                    .AddMapping<MyMessage>(typeof(Receiver));
            }

            class SetEndpointName : IWantToRunWhenBusStartsAndStops
            {
                public ReadOnlySettings Settings { get; set; }
                public MyContext Context { get; set; }

                public void Start()
                {
                    Context.EndpointNameOfSendingEndpoint = Settings.EndpointName();
                }

                public void Stop()
                {
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>()
                    .WithConfig<UnicastBusConfig>(c =>
                    {
                        c.ForwardReceivedMessagesTo = "audit";
                    })
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public void Handle(MyMessage message)
                {
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string EndpointNameOfSendingEndpoint { get; set; }

        }
    }
}