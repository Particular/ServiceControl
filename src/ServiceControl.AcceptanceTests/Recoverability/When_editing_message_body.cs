namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System.Collections.Generic;
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

    class When_editing_message_body : AcceptanceTest
    {
        [Test]
        public async Task A_new_message_with_edited_body_is_sent()
        {
            var context = await Define<EditMessageContext>()
                .WithEndpoint<EditedMessageReceiver>(e => e
                    .DoNotFailOnErrorMessages()
                    .When(c => c.SendLocal(new EditMessage {SomeProperty = "StarTrek rocks"})))
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
                        var editedMessage = JsonConvert.SerializeObject(new EditMessage
                        {
                            SomeProperty = "StarWars rocks"
                        });
                        var editModel = new EditMessageModel
                        {
                            MessageBody = editedMessage,
                            MessageHeaders = EditMessageHelper.TryRestoreOriginalHeaderKeys(failedMessage.Item.ProcessingAttempts.Last().Headers)
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

            Assert.AreEqual("StarWars rocks", context.EditedMessageProperty);
            Assert.AreNotEqual(context.OriginalMessageId, context.EditedMessageId);
            Assert.AreEqual(FailedMessageStatus.Resolved, context.OriginalMessageFailure.Status);
            CollectionAssert.DoesNotContain(context.EditedMessageHeaders, "NServiceBus.ExceptionInfo.StackTrace");
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

                    testContext.EditedMessageProperty = message.SomeProperty;
                    testContext.EditedMessageId = context.MessageId;
                    //TODO:RAVEN5 missing API
                    //testContext.EditedMessageHeaders = context.MessageHeaders.Keys.ToHashSet();
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