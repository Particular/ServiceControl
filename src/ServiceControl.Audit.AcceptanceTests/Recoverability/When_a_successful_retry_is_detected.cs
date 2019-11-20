namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    class When_a_successful_retry_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_raise_integration_event()
        {
            var failedMessageId = Guid.NewGuid().ToString();
            var context = await Define<InterceptedMessagesScenarioContext>()
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader("ServiceControl.Retry.UniqueMessageId", failedMessageId);
                    options.RouteToThisEndpoint();
                    return s.Send(new MyMessage(), options);
                }))
                .Done(c => c.SentMarkMessageFailureResolvedByRetriesCommands.Any())
                .Run();

            var command = context.SentMarkMessageFailureResolvedByRetriesCommands.Single();
            Assert.AreEqual(failedMessageId, command.FailedMessageId);
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