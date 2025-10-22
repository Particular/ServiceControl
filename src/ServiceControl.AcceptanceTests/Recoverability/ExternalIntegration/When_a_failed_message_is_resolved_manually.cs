namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using ServiceBus.Management.Infrastructure.Settings;
    using NUnit.Framework;
    using Contracts;
    using ServiceControl.MessageFailures;
    using System.Collections.Generic;
    using AcceptanceTesting.EndpointTemplates;
    using JsonSerializer = System.Text.Json.JsonSerializer;

    class When_a_failed_message_is_resolved_manually : ExternalIntegrationAcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<Context>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ctx.ExternalProcessorSubscribed = true;
                }
            });

            var context = await Define<Context>()
                .WithEndpoint<ErrorSender>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<MessageFailureResolvedManually>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Do("WaitUntilErrorsContainsFailedMessage",
                    async ctx => await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}") != null)
                .Do("WaitForExternalProcessorToSubscribe",
                    ctx => Task.FromResult(ctx.ExternalProcessorSubscribed))
                .Do("ResolveManually", async ctx =>
                {
                    await this.Patch<object>($"/api/pendingretries/resolve", new
                    {
                        uniquemessageids = new List<string>
                        {
                            ctx.FailedMessageId.ToString()
                        }
                    });
                })
                .Do("EnsureResolved", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Resolved);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonSerializer.Deserialize<MessageFailureResolvedManually>(context.Event);
            Assert.That(deserializedEvent.FailedMessageId, Is.EqualTo(context.FailedMessageId.ToString()));
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MessageFailureResolvedManually).Assembly, PrimaryOptions.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<MessageFailureResolvedManually>(PrimaryOptions.DEFAULT_INSTANCE_NAME); });

            public class FailureHandler(Context testContext) : IHandleMessages<MessageFailureResolvedManually>
            {
                public Task Handle(MessageFailureResolvedManually message, IMessageHandlerContext context)
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