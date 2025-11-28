namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
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

    class When_a_failed_msg_is_resolved_by_edit : AcceptanceTest
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

                    if (!ctx.OriginalMessageHandled)
                    {
                        return false;
                    }

                    if (!ctx.EditedMessage)
                    {
                        var failedEditedMessage = await this.GetOnlyFailedUnresolvedMessageId();
                        if (failedEditedMessage == null)
                        {
                            return false;
                        }

                        ctx.OriginalMessageFailureId = failedEditedMessage;

                        ctx.EditedMessage = true;
                        string editedMessage = JsonSerializer.Serialize(new EditResolutionMessage
                        {
                            HasBeenEdited = true
                        });

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

                    if (!ctx.EditedMessageHandled)
                    {
                        return false;
                    }

                    if (!ctx.MessageFailedHandled || !ctx.MessageEdited)
                    {
                        return false;
                    }

                    return true;
                }).Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.EditedMessageId, Is.EqualTo(context.OriginalMessageFailureId));
                Assert.That(context.EditedMessageEditOf, Is.EqualTo(context.OriginalMessageFailureId));
                Assert.That(context.FailedMessageId, Is.EqualTo(context.OriginalMessageFailureId));
            });
        }


        class EditMessageResolutionContext : ScenarioContext
        {
            public bool OriginalMessageHandled { get; set; }
            public bool EditedMessage { get; set; }
            public string OriginalMessageFailureId { get; set; }
            public bool EditedMessageHandled { get; set; }
            public string EditedMessageId { get; set; }
            public string EditedMessageEditOf { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
            public bool MessageEdited { get; set; }
            public string FailedMessageId { get; set; }
            public bool MessageFailedHandled { get; set; }
        }

        class MessageReceiver : EndpointConfigurationBuilder
        {
            public MessageReceiver() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            class EditMessageResolutionHandler(EditMessageResolutionContext testContext) : IHandleMessages<EditResolutionMessage>,
                IHandleMessages<MessageEditedAndRetried>,
                IHandleMessages<MessageFailed>
            {
                public Task Handle(EditResolutionMessage message, IMessageHandlerContext context)
                {
                    if (message.HasBeenEdited)
                    {
                        testContext.EditedMessageEditOf = context.MessageHeaders["ServiceControl.EditOf"];
                        testContext.EditedMessageHandled = true;
                        return Task.CompletedTask;
                    }

                    testContext.OriginalMessageHandled = true;
                    throw new SimulatedException();
                }

                public Task Handle(MessageEditedAndRetried message, IMessageHandlerContext context)
                {
                    testContext.EditedMessageId = message.FailedMessageId;
                    testContext.MessageEdited = true;
                    return Task.CompletedTask;
                }

                public Task Handle(MessageFailed message, IMessageHandlerContext context)
                {
                    testContext.FailedMessageId = message.FailedMessageId;
                    testContext.MessageFailedHandled = true;
                    return Task.CompletedTask;
                }
            }
        }

        class EditResolutionMessage : IMessage
        {
            public bool HasBeenEdited { get; init; }
        }
    }
}