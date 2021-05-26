namespace ServiceControl.Audit.AcceptanceTests.Recoverability
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
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
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<MessagesView>("/api/messages", m =>
                    {
                        var storedMessageId = m.Headers.Select(kv => kv.Value.ToString())
                            .FirstOrDefault(v => v == failedMessageId);

                        return storedMessageId == failedMessageId;
                    });

                    return result.HasResult;
                })
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