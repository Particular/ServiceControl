﻿namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTests;
    using Infrastructure;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using TestSupport.EndpointTemplates;

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
                        var editedMessage = JsonConvert.SerializeObject(new FailingMessage
                        {
                            HasBeenEdited = true
                        });
                        var editModel = new EditMessageModel
                        {
                            MessageBody = editedMessage,
                            MessageHeaders = EditMessageHelper.TryRestoreOriginalHeaderKeys(failedMessage.Item.ProcessingAttempts.Last().Headers)
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

            Assert.AreNotEqual(context.EditedMessageFailure.Id, context.OriginalMessageFailure.Id);
            Assert.AreNotEqual(context.EditedMessageFailure.UniqueMessageId, context.OriginalMessageFailure.UniqueMessageId);
            Assert.AreEqual(FailedMessageStatus.Resolved, context.OriginalMessageFailure.Status);
            Assert.AreEqual(FailedMessageStatus.Unresolved, context.EditedMessageFailure.Status);
            Assert.AreEqual(
                context.OriginalMessageFailure.Id,
                "FailedMessages/" + context.EditedMessageFailure.ProcessingAttempts.Last().Headers["service_control.edit_of"]);
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
            public FailingEditedMessageReceiver()
            {
                EndpointSetup<DefaultServer>(c => { c.NoRetries(); });
            }

            class FailingMessageHandler : IHandleMessages<FailingMessage>
            {
                public FailingMessageHandler(EditMessageFailureContext testContext, ReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

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

                EditMessageFailureContext testContext;
                ReadOnlySettings settings;
            }
        }

        class FailingMessage : IMessage
        {
            public bool HasBeenEdited { get; set; }
        }
    }
}