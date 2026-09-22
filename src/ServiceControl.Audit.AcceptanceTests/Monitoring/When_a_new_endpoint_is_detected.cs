namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts.EndpointControl;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;

    class When_a_new_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_notify_service_control()
        {
            SetSettings = settings => settings.ServiceControlQueueAddress = Conventions.EndpointNamingConvention(typeof(ServiceControlSpy));

            var context = await Define<Context>()
                .WithEndpoint<ServiceControlSpy>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(c => c.SentRegisterEndpointCommands.Any(command => command.Endpoint.Name == Conventions.EndpointNamingConvention(typeof(Receiver))))
                .Run();

            Assert.That(context.SentRegisterEndpointCommands.Select(command => command.Endpoint.Name),
                Does.Contain(Conventions.EndpointNamingConvention(typeof(Receiver))));
        }

        public class Context : ScenarioContext
        {
            public ConcurrentBag<RegisterNewEndpoint> SentRegisterEndpointCommands { get; } = [];
        }

        public class ServiceControlSpy : EndpointConfigurationBuilder
        {
            public ServiceControlSpy() => EndpointSetup<DefaultServerWithoutAudit>();

            [Handler]
            public class RegisterNewEndpointHandler(Context testContext) : IHandleMessages<RegisterNewEndpoint>
            {
                public Task Handle(RegisterNewEndpoint message, IMessageHandlerContext context)
                {
                    testContext.SentRegisterEndpointCommands.Add(message);
                    return Task.CompletedTask;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>();

            [Handler]
            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }

        public class MyMessage : ICommand;
    }
}