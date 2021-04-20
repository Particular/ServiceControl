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
                    return await this.TryGet<FailedMessage>($"/api/errors/{ErrorSender.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Unresolved);
                })
                .Do("WaitForExternalProcessorToSubscribe", ctx => Task.FromResult(ctx.ExternalProcessorSubscribed))
                .Do("Archive", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ErrorSender.FailedMessageId}/archive");
                })
                .Do("EnsureMessageIsArchived", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ErrorSender.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Archived);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<FailedMessagesArchived>(context.Event);
            CollectionAssert.Contains(deserializedEvent.FailedMessagesIds, ErrorSender.FailedMessageId);
        }
    }
}