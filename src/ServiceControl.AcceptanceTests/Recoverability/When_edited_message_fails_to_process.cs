namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using AcceptanceTests;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;

    class When_edited_message_fails_to_process : AcceptanceTest
    {
        [Test]
        public async Task A_new_message_failure_is_created()
        {
            var context = await Define<EditMessageFailureContext>()
                .WithEndpoint<FailingEditedMessageReceiver>(e => e
                    .When(c => c.SendLocal(new FailingMessage()))
                    .DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.OriginalMessageFailureId == null)
                    {
                        return false;
                    }

                    if (!ctx.EditedMessage)
                    {
                        var failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.OriginalMessageFailureId}");
                        if (!failedMessage.HasResult)
                        {
                            return false;
                        }

                        ctx.EditedMessage = true;
                        var editedMessage = JsonSerializer.Serialize(new FailingMessage
                        {
                            HasBeenEdited = true
                        });
                        var editModel = new EditMessageModel
                        {
                            MessageBody = editedMessage,
                            MessageHeaders = failedMessage.Item.ProcessingAttempts.Last().Headers
                        };
                        await this.Post($"/api/edit/{ctx.OriginalMessageFailureId}", editModel);

                        return false;
                    }

                    if (ctx.EditedMessageFailureId == null)
                    {
                        return false;
                    }

                    var failedEditedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.EditedMessageFailureId}");
                    if (!failedEditedMessage.HasResult)
                    {
                        return false;
                    }

                    ctx.OriginalMessageFailure = (await this.TryGet<FailedMessage>($"/api/errors/{ctx.OriginalMessageFailureId}")).Item;
                    ctx.EditedMessageFailure = (await this.TryGet<FailedMessage>($"/api/errors/{ctx.EditedMessageFailureId}")).Item;
                    return true;
                })
                .Run();

            Assert.That(context.OriginalMessageFailure.Id, Is.Not.EqualTo(context.EditedMessageFailure.Id));
            Assert.That(context.OriginalMessageFailure.UniqueMessageId, Is.Not.EqualTo(context.EditedMessageFailure.UniqueMessageId));
            Assert.That(context.OriginalMessageFailure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
            Assert.That(context.EditedMessageFailure.Status, Is.EqualTo(FailedMessageStatus.Unresolved));
            Assert.That(
                "FailedMessages/" + context.EditedMessageFailure.ProcessingAttempts.Last().Headers["ServiceControl.EditOf"],
                Is.EqualTo(context.OriginalMessageFailure.Id));
        }

        class EditMessageFailureContext : ScenarioContext
        {
            public string OriginalMessageFailureId { get; set; }
            public bool EditedMessage { get; set; }
            public string EditedMessageFailureId { get; set; }
            public FailedMessage OriginalMessageFailure { get; set; }
            public FailedMessage EditedMessageFailure { get; set; }
        }

        class FailingEditedMessageReceiver : EndpointConfigurationBuilder
        {
            public FailingEditedMessageReceiver() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            class FailingMessageHandler(EditMessageFailureContext testContext, IReadOnlySettings settings)
                : IHandleMessages<FailingMessage>
            {
                public Task Handle(FailingMessage message, IMessageHandlerContext context)
                {
                    if (message.HasBeenEdited)
                    {
                        testContext.EditedMessageFailureId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                    }
                    else
                    {
                        testContext.OriginalMessageFailureId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                    }

                    throw new SimulatedException();
                }
            }
        }

        class FailingMessage : IMessage
        {
            public bool HasBeenEdited { get; set; }
        }
    }
}