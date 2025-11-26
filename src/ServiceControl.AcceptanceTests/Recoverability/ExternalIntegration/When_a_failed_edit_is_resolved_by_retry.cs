namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    class When_a_failed_edit_is_resolved_by_retry : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<EditMessageResolutionContext>((s, ctx) =>
            {
                ctx.ExternalProcessorSubscribed = s.SubscriberReturnAddress.Contains(nameof(MessageReceiver));
            });

            var context = await Define<EditMessageResolutionContext>()
                .WithEndpoint<MessageReceiver>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<MessageFailureResolvedByRetry>();
                    await bus.Subscribe<MessageFailed>();
                    await bus.Subscribe<MessageEditedAndRetried>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }).When(c => c.SendLocal(new EditResolutionMessage())).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (!ctx.ExternalProcessorSubscribed)
                    {
                        return false;
                    }

                    // second message - edit & retry
                    if (ctx.MessageSentCount == 0 && ctx.MessageHandledCount == 1)
                    {
                        var failedMessagedId = await this.GetOnlyFailedUnresolvedMessageId();
                        if (failedMessagedId == null)
                        {
                            return false;
                        }

                        ctx.OriginalMessageFailureId = failedMessagedId;
                        ctx.MessageSentCount = 1;

                        string editedMessage = JsonSerializer.Serialize(new EditResolutionMessage
                        { });

                        SingleResult<FailedMessage> failedMessage =
                            await this.TryGet<FailedMessage>($"/api/errors/{ctx.OriginalMessageFailureId}");

                        var editModel = new EditMessageModel
                        {
                            MessageBody = editedMessage,
                            MessageHeaders = failedMessage.Item.ProcessingAttempts.Last().Headers
                        };
                        await this.Post($"/api/edit/{ctx.OriginalMessageFailureId}", editModel);
                        return false;
                    }

                    // third message - retry
                    if (ctx.MessageSentCount == 1 && ctx.MessageHandledCount == 2)
                    {
                        var failedMessageIdAfterId = await this.GetOnlyFailedUnresolvedMessageId();
                        if (failedMessageIdAfterId == null)
                        {
                            return false;
                        }

                        ctx.EditedMessageFailureId = failedMessageIdAfterId;
                        ctx.MessageSentCount = 2;

                        await this.Post<object>($"/api/errors/{ctx.EditedMessageFailureId}/retry");
                        return false;
                    }

                    if (ctx.MessageHandledCount != 3)
                    {
                        return false;
                    }

                    if (!ctx.MessageResolved || !ctx.EditAndRetryHandled || !ctx.MessageFailedResolved)
                    {
                        return false;
                    }

                    return true;
                }).Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.ResolvedMessageId, Is.EqualTo(context.EditedMessageFailureId));
                Assert.That(context.EditedMessageEditOf, Is.EqualTo(context.OriginalMessageFailureId));
                Assert.That(context.MessageFailedFailedMessageIds.Count, Is.EqualTo(2));
                Assert.That(context.MessageFailedFailedMessageIds, Is.Unique);
                Assert.That(context.MessageFailedFailedMessageIds, Has.Some.EqualTo(context.OriginalMessageFailureId));
                Assert.That(context.RetryFailedMessageId, Is.EqualTo(context.OriginalMessageFailureId));
                Assert.That(context.MessageFailedFailedMessageIds, Has.Some.EqualTo(context.EditedMessageFailureId));
            });
        }


        public class EditMessageResolutionContext : ScenarioContext
        {
            public string OriginalMessageFailureId { get; set; }
            public int MessageSentCount { get; set; }
            public int MessageHandledCount { get; set; }
            public string ResolvedMessageId { get; set; }
            public string EditedMessageFailureId { get; set; }
            public string EditedMessageEditOf { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
            public bool MessageResolved { get; set; }
            public bool MessageFailedResolved { get; set; }
            public string RetryFailedMessageId { get; set; }
            public bool EditAndRetryHandled { get; set; }
            public List<string> MessageFailedFailedMessageIds { get; } = [];
        }

        public class MessageReceiver : EndpointConfigurationBuilder
        {
            public MessageReceiver() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());


            public class EditMessageResolutionHandler(EditMessageResolutionContext testContext)
                : IHandleMessages<EditResolutionMessage>, IHandleMessages<MessageFailureResolvedByRetry>, IHandleMessages<MessageFailed>, IHandleMessages<MessageEditedAndRetried>
            {
                public Task Handle(EditResolutionMessage message, IMessageHandlerContext context)
                {
                    // First run - supposed to fail
                    if (testContext.MessageSentCount == 0)
                    {
                        testContext.MessageHandledCount = 1;
                        throw new SimulatedException();
                    }

                    // Second run - edit retry - supposed to fail
                    if (testContext.MessageSentCount == 1)
                    {
                        testContext.EditedMessageEditOf = context.MessageHeaders["ServiceControl.EditOf"];
                        testContext.MessageHandledCount = 2;
                        throw new SimulatedException();
                    }

                    // Last run - normal retry - supposed to succeed
                    testContext.MessageHandledCount = 3;
                    return Task.CompletedTask;
                }

                public Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
                {
                    testContext.ResolvedMessageId = message.FailedMessageId;
                    testContext.MessageResolved = true;
                    return Task.CompletedTask;
                }

                public Task Handle(MessageFailed message, IMessageHandlerContext context)
                {
                    testContext.MessageFailedFailedMessageIds.Add(message.FailedMessageId);
                    if (testContext.MessageFailedFailedMessageIds.Count == 2)
                    {
                        testContext.MessageFailedResolved = true;
                    }
                    return Task.CompletedTask;
                }

                public Task Handle(MessageEditedAndRetried message, IMessageHandlerContext context)
                {
                    testContext.RetryFailedMessageId = message.FailedMessageId;
                    testContext.EditAndRetryHandled = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class EditResolutionMessage : IMessage
        {
        }
    }
}

