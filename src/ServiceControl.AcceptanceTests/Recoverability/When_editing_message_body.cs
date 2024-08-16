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

    class When_editing_message_body : AcceptanceTest
    {
        [Test]
        public async Task A_new_message_with_edited_body_is_sent()
        {
            var context = await Define<EditMessageContext>()
                .WithEndpoint<EditedMessageReceiver>(e => e
                    .DoNotFailOnErrorMessages()
                    .When(c => c.SendLocal(new EditMessage { SomeProperty = "StarTrek rocks" })))
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
                        var editedMessage = JsonSerializer.Serialize(new EditMessage
                        {
                            SomeProperty = "StarWars rocks"
                        });
                        var editModel = new EditMessageModel
                        {
                            MessageBody = editedMessage,
                            MessageHeaders = failedMessage.Item.ProcessingAttempts.Last().Headers
                        };
                        await this.Post($"/api/edit/{ctx.UniqueMessageId}", editModel);
                        return false;
                    }

                    if (ctx.EditedMessageProperty == null)
                    {
                        return false;
                    }

                    ctx.OriginalMessageFailure = (await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}")).Item;
                    return true;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(context.EditedMessageProperty, Is.EqualTo("StarWars rocks"));
                Assert.That(context.EditedMessageId, Is.Not.EqualTo(context.OriginalMessageId));
                Assert.That(context.OriginalMessageFailure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
            });
            CollectionAssert.DoesNotContain(context.EditedMessageHeaders, "NServiceBus.ExceptionInfo.StackTrace");
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

                    testContext.EditedMessageProperty = message.SomeProperty;
                    testContext.EditedMessageId = context.MessageId;
                    testContext.EditedMessageHeaders = context.MessageHeaders.Keys.ToHashSet();
                    return Task.CompletedTask;
                }
            }
        }

        class EditMessageContext : ScenarioContext
        {
            public bool EditedMessage { get; set; }
            public string UniqueMessageId { get; set; }
            public string EditedMessageProperty { get; set; }
            public string OriginalMessageId { get; set; }
            public string EditedMessageId { get; set; }
            public FailedMessage OriginalMessageFailure { get; set; }
            public HashSet<string> EditedMessageHeaders { get; set; }
        }

        class EditMessage : IMessage
        {
            public string SomeProperty { get; set; }
        }
    }
}