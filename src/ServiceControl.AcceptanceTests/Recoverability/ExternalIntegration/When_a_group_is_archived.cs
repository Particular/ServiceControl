namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Text.Json;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts;
    using ServiceControl.MessageFailures;

    class When_a_group_is_archived : ExternalIntegrationAcceptanceTest
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
                    await bus.Subscribe<FailedMessagesArchived>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Do("WaitUntilGroupsContainsFailedMessages", async ctx =>
                {
                    // Don't retry until the message has been added to a group
                    var groups = await this.TryGetMany<FailedMessage.FailureGroup>("/api/recoverability/groups/");
                    if (!groups)
                    {
                        return false;
                    }

                    ctx.GroupId = groups.Items[0].Id;
                    return true;
                })
                .Do("WaitForExternalProcessorToSubscribe",
                    ctx => Task.FromResult(ctx.ExternalProcessorSubscribed))
                .Do("WaitUntilGroupContainsBothMessages", async ctx =>
                {
                    var failedMessages = await this.TryGetMany<FailedMessage>($"/api/recoverability/groups/{ctx.GroupId}/errors");
                    return failedMessages && failedMessages.Items.Count == 1;
                })
                .Do("Archive", async ctx => { await this.Post<object>($"/api/recoverability/groups/{ctx.GroupId}/errors/archive"); })
                .Do("EnsureArchived", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Archived);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonSerializer.Deserialize<FailedMessagesArchived>(context.Event);
            Assert.That(deserializedEvent.FailedMessagesIds, Is.Not.Null);
            Assert.That(deserializedEvent.FailedMessagesIds, Has.Member(context.FailedMessageId.ToString()));
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(FailedMessagesArchived).Assembly, Settings.DEFAULT_INSTANCE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<FailedMessagesArchived>(Settings.DEFAULT_INSTANCE_NAME); });

            public class FailureHandler(Context testContext) : IHandleMessages<FailedMessagesArchived>
            {
                public Task Handle(FailedMessagesArchived message, IMessageHandlerContext context)
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