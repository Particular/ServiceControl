namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using Audit.Monitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport;

    class When_importing_a_message_resolved_by_a_retry : AcceptanceTest
    {
        [Test]
        public async Task Should_set_status_to_resolved()
        {
            CustomConfiguration = endpointConfiguration =>
            {
                endpointConfiguration.Pipeline.Register(typeof(InterceptMessagesDestinedToServiceControl),
                    "Intercepts messages destined to ServiceControl");
            };

            MessagesView auditedMessage = null;

            var messageId = Guid.NewGuid().ToString();
            await Define<InterceptedMessagesScenarioContext>()
                .WithEndpoint<Receiver>(b => b.When(s =>
                {
                    var options = new SendOptions();

                    options.SetHeader("ServiceControl.Retry.UniqueMessageId", "CAN BE ANYTHING");
                    options.RouteToThisEndpoint();
                    options.SetMessageId(messageId);
                    return s.Send(new MyMessage(), options);
                }))
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages", m => m.MessageId == messageId);

                    auditedMessage = result;

                    return result;
                })
                .Run();

            Assert.That(auditedMessage.Status, Is.EqualTo(MessageStatus.ResolvedSuccessfully));
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