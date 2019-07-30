namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_new_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_notify_service_control()
        {
            var context = await Define<InterceptedMessagesScenarioContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(c => c.SentRegisterEndpointCommands.Any())
                .Run();

            var command = context.SentRegisterEndpointCommands.Single();
            Assert.AreEqual(Conventions.EndpointNamingConvention(typeof(Receiver)), command.Endpoint.Name);
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>();
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
    }
}