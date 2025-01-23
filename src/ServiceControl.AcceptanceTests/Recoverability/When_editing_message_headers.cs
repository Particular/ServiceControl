namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System.Collections.Generic;
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

    class When_editing_message_headers : AcceptanceTest
    {
        [Test]
        public async Task A_new_message_with_edited_headers_is_sent()
        {
            var context = await Define<EditMessageContext>()
                .WithEndpoint<EditedMessageReceiver>(e => e
                    .DoNotFailOnErrorMessages()
                    .When(c => c.SendLocal(new EditMessage())))
                .Done(async ctx =>
                {
                    if (string.IsNullOrWhiteSpace(ctx.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!ctx.EditedMessage)
                    {
                        var failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                        if (!failedMessage.HasResult)
                        {
                            return false;
                        }

                        ctx.EditedMessage = true;
                        var newHeaders = failedMessage.Item.ProcessingAttempts.Last().Headers.ToDictionary();
                        newHeaders.Add("AcceptanceTest.NewHeader", "42");
                        var editModel = new EditMessageModel
                        {
                            MessageBody = JsonSerializer.Serialize(new EditMessage()),
                            MessageHeaders = newHeaders
                        };

                        await this.Post($"/api/edit/{ctx.UniqueMessageId}", editModel);
                        return false;
                    }

                    if (ctx.EditedMessageHeaders == null)
                    {
                        return false;
                    }

                    ctx.OriginalMessageFailure = (await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}")).Item;
                    return true;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.EditedMessageId, Is.Not.EqualTo(context.OriginalMessageId));
                Assert.That(context.OriginalMessageFailure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
                Assert.That(context.EditedMessageHeaders["AcceptanceTest.NewHeader"], Is.EqualTo("42"));
                Assert.That(context.EditedMessageHeaders["ServiceControl.EditOf"], Is.EqualTo(context.UniqueMessageId));
            });
        }

        class EditedMessageReceiver : EndpointConfigurationBuilder
        {
            public EditedMessageReceiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                });

            class EditedMessageHandler(EditMessageContext testContext, IReadOnlySettings settings)
                : IHandleMessages<EditMessage>
            {
                public Task Handle(EditMessage message, IMessageHandlerContext context)
                {
                    if (!testContext.EditedMessage)
                    {
                        var messageId = context.MessageId.Replace(@"\", "-");
                        testContext.UniqueMessageId = DeterministicGuid.MakeId(messageId, settings.EndpointName()).ToString();
                        testContext.OriginalMessageId = context.MessageId;
                        throw new SimulatedException("message body needs to be changed");
                    }

                    testContext.EditedMessageHeaders = context.MessageHeaders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    testContext.EditedMessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }

        class EditMessageContext : ScenarioContext
        {
            public bool EditedMessage { get; set; }
            public string UniqueMessageId { get; set; }
            public Dictionary<string, string> EditedMessageHeaders { get; set; }
            public string OriginalMessageId { get; set; }
            public string EditedMessageId { get; set; }
            public FailedMessage OriginalMessageFailure { get; set; }
        }

        class EditMessage : IMessage;
    }
}