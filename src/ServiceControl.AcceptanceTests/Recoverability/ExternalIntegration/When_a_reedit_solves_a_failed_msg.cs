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

    class When_a_reedit_solves_a_failed_msg : AcceptanceTest
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

                        if (c.HasNativePubSubSupport)
                        {
                            c.ExternalProcessorSubscribed = true;
                        }
                    }).When(c => c.SendLocal(new EditResolutionMessage() { MessageAttempt = 0 }))
                    .DoNotFailOnErrorMessages())
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

                    if (!ctx.FirstEdit)
                    {
                        var failedMessagedId = await this.GetOnlyFailedUnresolvedMessageId();
                        if (failedMessagedId == null)
                        {
                            return false;
                        }

                        ctx.OriginalMessageFailureId = failedMessagedId;
                        ctx.FirstEdit = true;

                        string editedMessage =
                            JsonSerializer.Serialize(new EditResolutionMessage { MessageAttempt = 1 });

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

                    if (ctx.EditedMessageEditOf1 == null || !ctx.FirstEditHandled)
                    {
                        return false;
                    }

                    if (!ctx.SecondEdit)
                    {
                        var failedMessagedId = await this.GetOnlyFailedUnresolvedMessageId();
                        if (failedMessagedId == null || failedMessagedId == ctx.OriginalMessageFailureId)
                        {
                            return false;
                        }

                        ctx.SecondMessageFailureId = failedMessagedId;
                        ctx.SecondEdit = true;

                        string editedMessage =
                            JsonSerializer.Serialize(new EditResolutionMessage { MessageAttempt = 2 });

                        SingleResult<FailedMessage> failedMessage =
                            await this.TryGet<FailedMessage>($"/api/errors/{ctx.SecondMessageFailureId}");

                        var editModel = new EditMessageModel
                        {
                            MessageBody = editedMessage,
                            MessageHeaders = failedMessage.Item.ProcessingAttempts.Last().Headers
                        };
                        await this.Post($"/api/edit/{ctx.SecondMessageFailureId}", editModel);
                        return false;
                    }

                    if (!ctx.SecondEditHandled || ctx.EditedMessageEditOf2 == null)
                    {
                        return false;
                    }

                    if (!ctx.MessageResolved)
                    {
                        return false;
                    }

                    return true;
                }).Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.ResolvedMessageId, Is.EqualTo(context.SecondMessageFailureId));
                Assert.That(context.EditedMessageEditOf1, Is.EqualTo(context.OriginalMessageFailureId));
                Assert.That(context.EditedMessageEditOf2, Is.EqualTo(context.SecondMessageFailureId));
            });
        }


        public class EditMessageResolutionContext : ScenarioContext
        {
            public bool OriginalMessageHandled { get; set; }
            public string OriginalMessageFailureId { get; set; }
            public bool SecondEditHandled { get; set; }
            public string ResolvedMessageId { get; set; }
            public bool MessageResolved { get; set; }
            public bool FirstEditHandled { get; set; }
            public bool FirstEdit { get; set; }
            public bool SecondEdit { get; set; }
            public string SecondMessageFailureId { get; set; }
            public string EditedMessageEditOf2 { get; set; }
            public string EditedMessageEditOf1 { get; set; }
            public bool ExternalProcessorSubscribed { get; set; }
        }

        public class MessageReceiver : EndpointConfigurationBuilder
        {
            public MessageReceiver() => EndpointSetup<DefaultServerWithoutAudit>(c => c.NoRetries());


            public class EditMessageResolutionHandler(EditMessageResolutionContext testContext)
                : IHandleMessages<EditResolutionMessage>, IHandleMessages<MessageFailureResolvedByRetry>
            {
                public Task Handle(EditResolutionMessage message, IMessageHandlerContext context)
                {
                    switch (message.MessageAttempt)
                    {
                        case 0:
                            testContext.OriginalMessageHandled = true;
                            throw new SimulatedException();
                        case 1:
                            testContext.EditedMessageEditOf1 = context.MessageHeaders["ServiceControl.EditOf"];
                            testContext.FirstEditHandled = true;
                            throw new SimulatedException();
                        case 2:
                            testContext.EditedMessageEditOf2 = context.MessageHeaders["ServiceControl.EditOf"];
                            testContext.SecondEditHandled = true;
                            return Task.CompletedTask;
                        default:
                            return Task.CompletedTask;
                    }
                }

                public Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
                {
                    testContext.ResolvedMessageId = message.FailedMessageId;
                    testContext.MessageResolved = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class EditResolutionMessage : IMessage
        {
            public int MessageAttempt { get; init; }
        }
    }
}