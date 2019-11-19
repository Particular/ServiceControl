namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.AcceptanceTesting;
    using ServiceControl.AcceptanceTests;
    using ServiceControl.Infrastructure;
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
                        var newHeaders = EditMessageHelper.TryRestoreOriginalHeaderKeys(failedMessage.Item.ProcessingAttempts.Last().Headers);
                        newHeaders.Add(new KeyValuePair<string, string>("AcceptanceTest.NewHeader", "42"));
                        var editModel = new EditMessageModel
                        {
                            MessageBody = JsonConvert.SerializeObject(new EditMessage()),
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

            Assert.AreNotEqual(context.OriginalMessageId, context.EditedMessageId);
            Assert.AreEqual(FailedMessageStatus.Resolved, context.OriginalMessageFailure.Status);
            CollectionAssert.AreEqual("42", context.EditedMessageHeaders["AcceptanceTest.NewHeader"]);
            CollectionAssert.AreEqual(context.UniqueMessageId, context.EditedMessageHeaders["ServiceControl.EditOf"]);
        }

        class EditedMessageReceiver : EndpointConfigurationBuilder
        {
            public EditedMessageReceiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoRetries();
                });
            }

            class EditedMessageHandler : IHandleMessages<EditMessage>
            {
                public EditedMessageHandler(EditMessageContext testContext, ReadOnlySettings settings)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                }

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

                EditMessageContext testContext;
                ReadOnlySettings settings;
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

        class EditMessage : IMessage
        {
        }
    }
}