namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport;


    class When_a_successful_retry_from_old_SC_is_detected : AcceptanceTest
    {
        [Test]
        public async Task Should_raise_integration_event()
        {
            CustomConfiguration = endpointConfiguration =>
            {
                endpointConfiguration.Pipeline.Register(typeof(InterceptMessagesDestinedToServiceControl),
                    "Intercepts messages destined to ServiceControl");
            };

            var uniqueMessageIdHeaderName = "ServiceControl.Retry.UniqueMessageId";

            var failedMessageId = Guid.NewGuid().ToString();
            var context = await Define<InterceptedMessagesScenarioContext>()
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader(uniqueMessageIdHeaderName, failedMessageId);
                    options.RouteToThisEndpoint();
                    return s.Send(new MyMessage(), options);
                }))
                .Done(c => c.SentMarkMessageFailureResolvedByRetriesCommands.Any())
                .Run();

            var command = context.SentMarkMessageFailureResolvedByRetriesCommands.Single();
            Assert.That(command.FailedMessageId, Is.EqualTo(failedMessageId));
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