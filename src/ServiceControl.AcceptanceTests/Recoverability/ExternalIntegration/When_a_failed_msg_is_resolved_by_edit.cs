namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts;
    using Google.Protobuf.Reflection;
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
            var context = await Define<EditMessageResolutionContext>()
                .WithEndpoint<MessageReceiver>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<MessageFailureResolvedByRetry>();
                }).When(c => c.SendLocal(new EditResolutionMessage())).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (!ctx.OriginalMessageHandled)
                    {
                        return false;
                    }

                    if (!ctx.EditedMessage)
                    {
                        var allFailedMessages =
                            await this.TryGet<IList<FailedMessageView>>($"/api/errors/?status=unresolved");
                        if (!allFailedMessages.HasResult)
                        {
                            return false;
                        }

                        if (allFailedMessages.Item.Count != 1)
                        {
                            return false;
                        }

                        ctx.OriginalMessageFailureId = allFailedMessages.Item.First().Id;

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

                    if (!ctx.MessageResolved)
                    {
                        return false;
                    }

                    return true;
                }).Run();

            Assert.That(context.ResolvedMessageId, Is.EqualTo(context.OriginalMessageFailureId));
        }


        class EditMessageResolutionContext : ScenarioContext
        {
            public bool OriginalMessageHandled { get; set; }
            public bool EditedMessage { get; set; }
            public string OriginalMessageFailureId { get; set; }
            public bool EditedMessageHandled { get; set; }
            public string ResolvedMessageId { get; set; }
            public bool MessageResolved { get; set; }
        }

        class MessageReceiver : EndpointConfigurationBuilder
        {
            public MessageReceiver() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());

            class EditMessageResolutionHandler(EditMessageResolutionContext testContext)
                : IHandleMessages<EditResolutionMessage>, IHandleMessages<MessageFailureResolvedByRetry>
            {
                public Task Handle(EditResolutionMessage message, IMessageHandlerContext context)
                {
                    if (message.HasBeenEdited)
                    {
                        testContext.EditedMessageHandled = true;
                        return Task.CompletedTask;
                    }

                    testContext.OriginalMessageHandled = true;
                    throw new SimulatedException();
                }

                public Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
                {
                    testContext.ResolvedMessageId = message.FailedMessageId;
                    testContext.MessageResolved = true;
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