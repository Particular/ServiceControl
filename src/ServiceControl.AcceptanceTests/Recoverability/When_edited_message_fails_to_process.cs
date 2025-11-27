namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using AcceptanceTests;
    using Contracts.MessageFailures;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class When_edited_message_fails_to_process : AcceptanceTest
    {
        [Test]
        public async Task A_new_message_failure_is_created()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<EditMessageFailureContext>((s, ctx) =>
            {
                ctx.ExternalProcessorSubscribed = s.SubscriberReturnAddress.Contains(nameof(FailingEditedMessageReceiver));
            });

            var context = await Define<EditMessageFailureContext>()
                .WithEndpoint<FailingEditedMessageReceiver>(b => b.When(async (bus, c) =>
                    {
                        await bus.Subscribe<ServiceControl.Contracts.MessageFailed>();

                        if (c.HasNativePubSubSupport)
                        {
                            c.ExternalProcessorSubscribed = true;
                        }
                    }).When(c => c.SendLocal(new FailingMessage()))
                    .DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (!ctx.OriginalMessageHandled)
                    {
                        return false;
                    }

                    if (!ctx.EditedMessage)
                    {
                        var failedMessageId = await this.GetOnlyFailedUnresolvedMessageId();
                        if (failedMessageId == null)
                        {
                            return false;
                        }

                        ctx.OriginalMessageFailureId = failedMessageId;

                        ctx.EditedMessage = true;
                        var editedMessageInternalId = Guid.NewGuid().ToString();
                        ctx.EditedMessageInternalId = editedMessageInternalId;
                        var editedMessage = JsonSerializer.Serialize(new FailingMessage
                        {
                            HasBeenEdited = true,
                            MessageInternalId = editedMessageInternalId
                        });

                        var failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.OriginalMessageFailureId}");

                        var editModel = new EditMessageModel
                        {
                            MessageBody = editedMessage,
                            MessageHeaders = failedMessage.Item.ProcessingAttempts.Last().Headers
                        };
                        await this.Post($"/api/edit/{ctx.OriginalMessageFailureId}", editModel);

                        return false;
                    }

                    if (!ctx.EditedMessageHandled || !ctx.MessageFailedHandled)
                    {
                        return false;
                    }

                    var failedMessageIdAfterEdit = await this.GetOnlyFailedUnresolvedMessageId();
                    if (failedMessageIdAfterEdit == null)
                    {
                        return false;
                    }

                    if (failedMessageIdAfterEdit == ctx.OriginalMessageFailureId)
                    {
                        return false;
                    }

                    ctx.EditedMessageFailureId = failedMessageIdAfterEdit;

                    ctx.OriginalMessageFailure = (await this.TryGet<FailedMessage>($"/api/errors/{ctx.OriginalMessageFailureId}")).Item;
                    ctx.EditedMessageFailure = (await this.TryGet<FailedMessage>($"/api/errors/{ctx.EditedMessageFailureId}")).Item;
                    return true;
                })
                .Run();

            var editedMessageBody = JsonSerializer.Deserialize<FailingMessage>(context.EditedMessageFailure.ProcessingAttempts.Last().MessageMetadata["MsgFullText"].ToString());

            Assert.Multiple(() =>
            {
                Assert.That(context.OriginalMessageFailure.Id, Is.Not.EqualTo(context.EditedMessageFailure.Id));
                Assert.That(context.OriginalMessageFailure.UniqueMessageId, Is.Not.EqualTo(context.EditedMessageFailure.UniqueMessageId));
                Assert.That(context.OriginalMessageFailure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
                Assert.That(context.EditedMessageFailure.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
                Assert.That(
                    "FailedMessages/" + context.EditedMessageFailure.ProcessingAttempts.Last().Headers["ServiceControl.EditOf"],
                    Is.EqualTo(context.OriginalMessageFailure.Id));
                Assert.That(editedMessageBody.MessageInternalId, Is.EqualTo(context.EditedMessageInternalId));
            });
        }

        class EditMessageFailureContext : ScenarioContext
        {
            public bool OriginalMessageHandled { get; set; }
            public bool EditedMessage { get; set; }
            public bool EditedMessageHandled { get; set; }
            public FailedMessage OriginalMessageFailure { get; set; }
            public FailedMessage EditedMessageFailure { get; set; }

            public string OriginalMessageFailureId { get; set; }
            public string EditedMessageFailureId { get; set; }
            public string EditedMessageInternalId { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
            public string FirstMessageFailedId { get; set; }
            public string SecondMessageFailedId { get; set; }
            public bool MessageFailedHandled { get; set; }
        }

        class FailingEditedMessageReceiver : EndpointConfigurationBuilder
        {
            public FailingEditedMessageReceiver() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            class FailingMessageHandler(EditMessageFailureContext testContext)
                : IHandleMessages<FailingMessage>, IHandleMessages<ServiceControl.Contracts.MessageFailed>
            {
                public Task Handle(FailingMessage message, IMessageHandlerContext context)
                {
                    if (message.HasBeenEdited)
                    {
                        testContext.EditedMessageHandled = true;
                    }
                    else
                    {
                        testContext.OriginalMessageHandled = true;
                    }

                    throw new SimulatedException();
                }

                public Task Handle(ServiceControl.Contracts.MessageFailed message, IMessageHandlerContext context)
                {
                    if (testContext.FirstMessageFailedId == null)
                    {
                        testContext.FirstMessageFailedId = message.FailedMessageId;
                    }
                    else
                    {
                        testContext.SecondMessageFailedId = message.FailedMessageId;
                        testContext.MessageFailedHandled = true;
                    }
                    return Task.CompletedTask;
                }
            }
        }

        class FailingMessage : IMessage
        {
            public bool HasBeenEdited { get; init; }
            public string MessageInternalId { get; init; }
        }
    }
}