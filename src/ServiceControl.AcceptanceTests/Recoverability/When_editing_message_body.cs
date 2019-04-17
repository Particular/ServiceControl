namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    //TODO: when edited message has been successfully processed
    //TODO: when edited message fails to process
    class When_editing_message_body : AcceptanceTest
    {
        [Test]
        public async Task A_new_message_with_edited_body_is_sent()
        {
            var context = await Define<EditMessageContext>()
                .WithEndpoint<EditedMessageReceiver>(e => e
                    .DoNotFailOnErrorMessages()
                    .When(c => c.SendLocal(new EditMessage(){SomeProperty = "StarTrek rocks" })))
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
                        await this.Post($"/api/errors/{ctx.UniqueMessageId}/editandretry", editedMessage, "application/json");
                        return false;
                    }

                    return ctx.EditedMessageProperty != null;
                })
                .Run();

            Assert.AreEqual("StarWars rocks", context.EditedMessageProperty);
            Assert.AreNotEqual(context.OriginalMessageId, context.EditedMessageId);
            //TODO: test message is no longer shown as failed
            //TODO: should not contain exception error headers
        }

        class EditedMessageReceiver : EndpointConfigurationBuilder
        {
            public EditedMessageReceiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                });
            }

            class EditedMessageHandler : IHandleMessages<EditMessage>
            {
                EditMessageContext testContext;
                ReadOnlySettings settings;

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

                    return Task.CompletedTask;
                }
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
    }

    class EditMessage : IMessage
    {
        public string SomeProperty { get; set; }
    }
}