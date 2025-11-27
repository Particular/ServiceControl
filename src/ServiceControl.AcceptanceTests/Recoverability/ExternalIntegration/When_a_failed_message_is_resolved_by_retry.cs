namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
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

    class When_a_failed_message_is_resolved_by_retry : ExternalIntegrationAcceptanceTest
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
                .Do("Retry", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ctx.FailedMessageId}/retry");
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
                    routing.RouteToEndpoint(typeof(FailedMessagesArchived).Assembly, PrimaryOptions.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<FailedMessagesArchived>(PrimaryOptions.DEFAULT_INSTANCE_NAME); });

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