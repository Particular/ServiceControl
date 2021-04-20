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

    class When_a_failed_message_is_resolved_by_retry : When_a_message_failed
    {
        [Test]
        public async Task Should_publish_notification()
        {
            CustomConfiguration = config => config.OnEndpointSubscribed<Context>((s, ctx) =>
            {
                ctx.ExternalProcessorSubscribed = s.SubscriberReturnAddress.Contains(nameof(ExternalProcessor));
            });

            var context = await Define<Context>()
                .WithEndpoint<ErrorSender>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<MessageFailureResolvedByRetry>();

                    if (c.HasNativePubSubSupport)
                    {
                        c.ExternalProcessorSubscribed = true;
                    }
                }))
                .Do("WaitUntilErrorsContainsFailedMessage", async ctx =>
                {
                   return await this.TryGet<FailedMessage>($"/api/errors/{ErrorSender.FailedMessageId}") != null;
                })
                .Do("WaitForExternalProcessorToSubscribe", ctx => Task.FromResult(ctx.ExternalProcessorSubscribed))
                .Do("Retry", async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ErrorSender.FailedMessageId}/retry");
                })
                .Do("EnsureRetried", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ErrorSender.FailedMessageId}",
                        e => e.Status == FailedMessageStatus.Resolved);
                })
                .Done(ctx => ctx.EventDelivered) //Done when sequence is finished
                .Run();

            var deserializedEvent = JsonConvert.DeserializeObject<MessageFailureResolvedByRetry>(context.Event);
            Assert.IsTrue(deserializedEvent?.FailedMessageId == ErrorSender.FailedMessageId.ToString());
        }
    }
}