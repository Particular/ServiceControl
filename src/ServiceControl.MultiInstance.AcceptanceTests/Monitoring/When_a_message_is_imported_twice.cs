namespace ServiceControl.MultiInstance.AcceptanceTests.Monitoring
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Endpoints;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;
    using TestSupport;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_is_imported_twice : AcceptanceTest
    {
        [Test]
        public async Task Should_register_a_new_endpoint()
        {
            var endpointName = Conventions.EndpointNamingConvention(typeof(Sender));

            EndpointsView endpoint = null;

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EndpointsView>("/api/endpoints", m => m.Name == endpointName);
                    endpoint = result;
                    if (!result)
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            Assert.AreEqual(endpointName, endpoint?.Name);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.ForwardReceivedMessagesTo("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}