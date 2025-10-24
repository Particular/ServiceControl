namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Contracts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.MessageFailures;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    class When_a_failed_message_is_resolved_by_edit_and_retry : ExternalIntegrationAcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<Context>((s, ctx) =>
            {
                ctx.ExternalProcessorSubscribed = s.SubscriberReturnAddress.Contains(nameof(ExternalProcessor));
            });

            var context = await Define<Context>()
                .WithEndpoint<ErrorSender>(b => b.When(session => Task.CompletedTask).DoNotFailOnErrorMessages())
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<MessageFailureResolvedByRetry>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Do("WaitUntilErrorsContainsFailedMessage",
                    async ctx => await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}") != null)
                .Do("WaitForExternalProcessorToSubscribe",
                    ctx => Task.FromResult(ctx.ExternalProcessorSubscribed))
                .Do("EditAndRetry", async ctx =>
                {
                    // Prepare the edit payload that conforms to the web API's snake_case format
                    // Based on AMessage structure from the base class
                    var editPayload = new
                    {
                        message_body = "{}",  // Empty JSON body for AMessage (ICommand with no properties)
                        message_headers = new Dictionary<string, string>
                        {
                            ["NServiceBus.MessageId"] = Guid.NewGuid().ToString(),
                            ["NServiceBus.MessageIntent"] = "Send",
                            ["NServiceBus.ConversationId"] = Guid.NewGuid().ToString(),
                            ["NServiceBus.CorrelationId"] = Guid.NewGuid().ToString(),
                            ["NServiceBus.ReplyToAddress"] = "TestEndpoint",
                            ["NServiceBus.OriginatingMachine"] = "TestMachine",
                            ["NServiceBus.OriginatingEndpoint"] = "TestEndpoint",
                            ["NServiceBus.ContentType"] = "application/json",
                            ["NServiceBus.EnclosedMessageTypes"] = typeof(AMessage).AssemblyQualifiedName,
                            ["NServiceBus.Version"] = "8.0.0",
                            ["NServiceBus.TimeSent"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss:ffffff Z"),
                            ["NServiceBus.ProcessingMachine"] = "TestMachine",
                            ["NServiceBus.ProcessingEndpoint"] = nameof(ErrorSender)
                        }
                    };

                    await this.Post($"/api/edit/{ctx.FailedMessageId}", editPayload);
                })
                .Do("EnsureRetried", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Resolved);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonSerializer.Deserialize<MessageFailureResolvedByRetry>(context.Event);
            Assert.That(deserializedEvent?.FailedMessageId, Is.EqualTo(context.FailedMessageId.ToString()));
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MessageFailureResolvedByRetry).Assembly, Settings.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<MessageFailureResolvedByRetry>(Settings.DEFAULT_INSTANCE_NAME); });

            public class FailureHandler(Context testContext) : IHandleMessages<MessageFailureResolvedByRetry>
            {
                public Task Handle(MessageFailureResolvedByRetry message, IMessageHandlerContext context)
                {
                    var serializedMessage = JsonSerializer.Serialize(message);
                    testContext.Event = serializedMessage;
                    testContext.EventDelivered = true;
                    return Task.CompletedTask;
                }
            }
        }
    }
}