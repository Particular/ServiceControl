namespace ServiceControl.AcceptanceTests.Recoverability.ExternalIntegration
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using Contracts;
    using ServiceControl.MessageFailures;
    using Newtonsoft.Json;
    using TestSupport.EndpointTemplates;
    using ServiceBus.Management.Infrastructure.Settings;

    class When_a_failed_message_is_archived : When_a_message_failed
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
                    await bus.Subscribe<FailedMessagesArchived>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Do("WaitUntilErrorsContainsFailedMessage", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Unresolved);
                })
                .Do("WaitForExternalProcessorToSubscribe", ctx => Task.FromResult(ctx.ExternalProcessorSubscribed))
                .Do("Archive", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ctx.FailedMessageId}/archive");
                })
                .Do("EnsureMessageIsArchived", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Archived);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<FailedMessagesArchived>(context.Event);
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