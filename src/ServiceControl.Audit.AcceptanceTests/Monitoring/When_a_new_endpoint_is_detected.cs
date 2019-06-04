namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;

    class When_a_new_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_raise_integration_event()
        {
            CustomConfiguration = config => config.RegisterComponents(c => c.ConfigureComponent<DomainEventSpy>(DependencyLifecycle.SingleInstance));

            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(c => c.EventRaised)
                .Run();

            Assert.True(context.EventRaised);
        }

        public class DomainEventSpy : IDomainHandler<NewEndpointDetected>
        {
            public MyContext TestContext { get; set; }

            public Task Handle(NewEndpointDetected domainEvent)
            {
                TestContext.EventRaised = true;
                return Task.CompletedTask;
            }
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

        public class MyContext : ScenarioContext
        {
            public bool EventRaised { get; internal set; }
        }
    }
}