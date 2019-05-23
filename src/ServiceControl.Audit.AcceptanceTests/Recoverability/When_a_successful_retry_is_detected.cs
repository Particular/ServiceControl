namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using Contracts.MessageFailures;
    using Infrastructure.DomainEvents;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;

    class When_a_successful_retry_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_raise_integration_event()
        {
            CustomConfiguration = config => config.RegisterComponents(c => c.ConfigureComponent<DomainEventSpy>(DependencyLifecycle.SingleInstance));

            var failedMessageId = Guid.NewGuid().ToString();
            var context = await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader("ServiceControl.Retry.UniqueMessageId", failedMessageId);
                    options.RouteToThisEndpoint();
                    return s.Send(new MyMessage(), options);
                }))
                .Done(c => c.EventRaised != null)
                .Run();

            Assert.AreEqual(failedMessageId, context.EventRaised.FailedMessageId);
        }

        public class DomainEventSpy : IDomainHandler<MessageFailureResolvedByRetry>
        {
            public MyContext TestContext { get; set; }

            public Task Handle(MessageFailureResolvedByRetry domainEvent)
            {
                TestContext.EventRaised = domainEvent;
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
            public MessageFailureResolvedByRetry EventRaised { get; internal set; }
        }
    }
}