namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using EventLog;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport;

    class When_a_retry_for_a_failed_message_is_successful : AcceptanceTest
    {
        [Test]
        [CancelAfter(120_000)]
        public async Task Should_show_up_as_resolved_in_the_eventlog(CancellationToken cancellationToken = default)
        {
            FailedMessage failure = null;
            List<EventLogItem> eventLogItems = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("Wait for the message to fail", async ctx => (failure = await GetFailedMessage(ctx)) != null)
                .Do("Retry the message", ctx => IssueRetry(ctx, () => this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry")))
                .Do("Wait for it to be resolved", async ctx => await IsResolved(ctx, result => failure = result))
                .Do("Read the event log", async _ =>
                {
                    var result = await this.TryGetMany<EventLogItem>("/api/eventlogitems", item => item.Description.StartsWith("Failed message resolved by retry"));
                    eventLogItems = result;
                    return result;
                })
                .Done()
                .Run(cancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(failure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
                Assert.That(eventLogItems.Any(item => item.Description.Equals("Failed message resolved by retry") && item.RelatedTo.Contains("/message/" + failure.UniqueMessageId)), Is.True);
            }
        }

        [Test]
        [CancelAfter(120_000)]
        public async Task Should_show_up_as_resolved_when_doing_a_multi_retry(CancellationToken cancellationToken = default)
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("Wait for the message to fail", async ctx => (failure = await GetFailedMessage(ctx)) != null)
                .Do("Retry the message by id", ctx => IssueRetry(ctx, () => this.Post("/api/errors/retry", new List<string> { ctx.UniqueMessageId })))
                .Do("Wait for it to be resolved", async ctx => await IsResolved(ctx, result => failure = result))
                .Done()
                .Run(cancellationToken);

            Assert.That(failure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        }

        [Test]
        [CancelAfter(120_000)]
        public async Task Should_show_up_as_resolved_when_doing_a_retry_all(CancellationToken cancellationToken = default)
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("Wait for the message to fail", async ctx => (failure = await GetFailedMessage(ctx)) != null)
                .Do("Retry everything", ctx => IssueRetry(ctx, () => this.Post<object>("/api/errors/retry/all")))
                .Do("Wait for it to be resolved", async ctx => await IsResolved(ctx, result => failure = result))
                .Done()
                .Run(cancellationToken);

            Assert.That(failure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        }

        [Test]
        [CancelAfter(120_000)]
        public async Task Acknowledging_the_retry_should_be_successful(CancellationToken cancellationToken = default)
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("Wait for the message to fail", async ctx => (failure = await GetFailedMessage(ctx)) != null)
                .Do("Retry the group it belongs to", ctx => IssueRetry(ctx, () => this.Post<object>($"/api/recoverability/groups/{failure.FailureGroups.First().Id}/errors/retry")))
                .Do("Wait for it to be resolved", async ctx => await IsResolved(ctx, result => failure = result))
                .Done()
                .Run(cancellationToken);
        }

        [Test]
        [CancelAfter(120_000)]
        public async Task Should_show_up_as_resolved_when_doing_a_retry_all_for_the_given_endpoint(CancellationToken cancellationToken = default)
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("Wait for the message to fail", async ctx => (failure = await GetFailedMessage(ctx)) != null)
                .Do("Retry everything for the endpoint", ctx => IssueRetry(ctx, () => this.Post<object>($"/api/errors/{ctx.EndpointNameOfReceivingEndpoint}/retry/all")))
                .Do("Wait for it to be resolved", async ctx => await IsResolved(ctx, result => failure = result))
                .Done()
                .Run(cancellationToken);

            Assert.That(failure.Status, Is.EqualTo(FailedMessageStatus.Resolved));
        }

        Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c)
        {
            if (c.MessageId == null)
            {
                return Task.FromResult(SingleResult<FailedMessage>.Empty);
            }

            return this.TryGet<FailedMessage>("/api/errors/" + c.UniqueMessageId);
        }

        async Task<bool> IsResolved(MyContext c, Action<FailedMessage> capture)
        {
            var result = await GetFailedMessage(c);

            if (!result)
            {
                return false;
            }

            capture(result);

            return result.Item.Status == FailedMessageStatus.Resolved;
        }

        // The handler reads this to decide whether to throw, so it has to be set before the retry
        // reaches the endpoint.
        Task IssueRetry(MyContext c, Func<Task> retryAction)
        {
            c.RetryIssued = true;

            return retryAction();
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                });

            [Handler]
            public class MyMessageHandler(
                MyContext scenarioContext,
                IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    scenarioContext.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!scenarioContext.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand;

        public class MyContext : ScenarioContext, ISequenceContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool RetryIssued { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();

            public int Step { get; set; }
        }
    }
}
