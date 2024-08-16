namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_new_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_notify_service_control()
        {
            CustomConfiguration = endpointConfiguration =>
            {
                endpointConfiguration.Pipeline.Register(typeof(InterceptMessagesDestinedToServiceControl),
                    "Intercepts messages destined to ServiceControl");
            };

            var context = await Define<InterceptedMessagesScenarioContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(c => c.SentRegisterEndpointCommands.Any())
                .Run();

            var command = context.SentRegisterEndpointCommands.Single();
            Assert.That(command.Endpoint.Name, Is.EqualTo(Conventions.EndpointNamingConvention(typeof(Receiver))));
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>();

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }

        public class MyMessage : ICommand;
    }
}