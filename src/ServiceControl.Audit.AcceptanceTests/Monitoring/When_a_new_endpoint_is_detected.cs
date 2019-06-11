namespace ServiceControl.Audit.AcceptanceTests.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using Contracts.EndpointControl;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;

    class When_a_new_endpoint_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_raise_integration_event()
        {
            CustomConfiguration = config => config.Pipeline.Register(typeof(NewEndpointDetectedSpy), "Captures the event");

            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(c => c.EventRaised)
                .Run();

            Assert.True(context.EventRaised);
        }

        public class NewEndpointDetectedSpy : Behavior<IOutgoingPublishContext>
        {
            public MyContext TestContext { get; set; }

            public override Task Invoke(IOutgoingPublishContext context, Func<Task> next)
            {
                if (context.Message.Instance is NewEndpointDetected)
                {
                    TestContext.EventRaised = true;
                }

                return next();
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