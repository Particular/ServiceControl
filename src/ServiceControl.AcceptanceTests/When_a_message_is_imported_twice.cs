namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
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
        public async Task Should_register_a_new_endpoint()
        {
            var context = new MyContext();
            EndpointsView endpoint = null;

            await Define(context)
                .WithEndpoint<Sender>(b => b.Given((bus, c) =>
                {
                    bus.Send(new MyMessage());
                }))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    var result = await TryGetSingle<EndpointsView>("/api/endpoints", m => m.Name == c.EndpointNameOfSendingEndpoint);
                    endpoint = result;
                    if (!result)
                    {
                        return false;
                    }

                    return true;

                })
                .Run(TimeSpan.FromSeconds(30));

            Assert.AreEqual(context.EndpointNameOfSendingEndpoint, endpoint.Name);
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
                EndpointSetup<DefaultServerWithAudit>()
                    .WithConfig<UnicastBusConfig>(c =>
                    {
                        c.ForwardReceivedMessagesTo = "audit";
                    });
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