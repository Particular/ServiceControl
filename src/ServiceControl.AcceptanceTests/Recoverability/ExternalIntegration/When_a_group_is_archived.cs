namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using ServiceBus.Management.Infrastructure.Settings;
    using NUnit.Framework;
    using ServiceControl.Contracts;
    using ServiceControl.MessageFailures;
    using TestSupport.EndpointTemplates;
    using Newtonsoft.Json;

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
                    var failedMessages = await this.TryGetMany<FailedMessage>($"/api/recoverability/groups/{ctx.GroupId}/errors").ConfigureAwait(false);
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

            var deserializedEvent = JsonConvert.DeserializeObject<FailedMessagesArchived>(context.Event);
            Assert.IsNotNull(deserializedEvent.FailedMessagesIds);
            CollectionAssert.Contains(deserializedEvent.FailedMessagesIds, context.FailedMessageId.ToString());
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(FailedMessagesArchived).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata => { publisherMetadata.RegisterPublisherFor<FailedMessagesArchived>(Settings.DEFAULT_SERVICE_NAME); });
            }

            public class FailureHandler : IHandleMessages<FailedMessagesArchived>
            {
                public Context Context { get; set; }

                public Task Handle(FailedMessagesArchived message, IMessageHandlerContext context)
                {
                    var serializedMessage = JsonConvert.SerializeObject(message);
                    Context.Event = serializedMessage;
                    Context.EventDelivered = true;
                    return Task.FromResult(0);
                }
            }
        }
    }
}