namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class When_retry_is_confirmed : AcceptanceTest
    {
        [Test]
        public async Task Should_mark_message_as_successfully_resolved()
        {
            var context = await Define<Context>()
                .WithEndpoint<RetryingEndpoint>(b => b
                    .DoNotFailOnErrorMessages()
                    .When(s => s.SendLocal(new RetryMessage())))
                .Do("Wait for failed message", async c =>
                {
                    // wait till the failed message has been ingested
                    var tryGetMany = await this.TryGetMany<FailedMessageView>("/api/errors");
                    return tryGetMany;
                })
                .Do("Retry message", async c =>
                {
                    //trigger retry
                    await this.Post<object>("/api/errors/retry/all");
                })
                .Do("Wait for retry confirmation", async c =>
                {
                    if (!c.ReceivedRetry)
                    {
                        // wait till endpoint processed the retried message
                        return false;
                    }

                    var failedMessages = await this.TryGetMany<FailedMessageView>("/api/errors");
                    if (failedMessages.Items.Any(i => i.Status == FailedMessageStatus.Resolved))
                    {
                        c.MessagesView = failedMessages.Items;
                        return true;
                    }

                    return false;
                })
                .Done(c => true)
                .Run();

            Assert.That(context.MessagesView.Count, Is.EqualTo(1));
            var failedMessage = context.MessagesView.Single();
            Assert.Multiple(() =>
            {
                Assert.That(failedMessage.Status, Is.EqualTo(FailedMessageStatus.Resolved));
                Assert.That(failedMessage.NumberOfProcessingAttempts, Is.EqualTo(1));
            });
        }

        class Context : ScenarioContext, ISequenceContext
        {
            public bool ThrowOnHandler { get; set; } = true;
            public bool ReceivedRetry { get; set; }
            public int Step { get; set; }
            public List<FailedMessageView> MessagesView { get; set; }
        }

        class RetryingEndpoint : EndpointConfigurationBuilder
        {
            public RetryingEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            class RetryMessageHandler(Context testContext) : IHandleMessages<RetryMessage>
            {
                public Task Handle(RetryMessage message, IMessageHandlerContext context)
                {
                    if (testContext.ThrowOnHandler)
                    {
                        testContext.ThrowOnHandler = false;
                        throw new Exception("boom");
                    }

                    testContext.ReceivedRetry = true;
                    return Task.CompletedTask;
                }
            }
        }

        class RetryMessage : IMessage;
    }
}