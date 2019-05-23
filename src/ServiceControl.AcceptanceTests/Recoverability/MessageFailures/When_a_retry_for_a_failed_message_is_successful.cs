namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EventLog;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.MessageFailures;

    class When_a_retry_for_a_failed_message_is_successful : AcceptanceTest
    {
        [Test]
        public async Task Should_show_up_as_resolved_in_the_eventlog()
        {
            FailedMessage failure = null;
            List<EventLogItem> eventLogItems = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var failedMessageResult = await GetFailedMessage(c);
                    failure = failedMessageResult;
                    if (!failedMessageResult)
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        var eventLogItemsResult = await this.TryGetMany<EventLogItem>("/api/eventlogitems", item => item.Description.StartsWith("Failed message resolved by retry"));
                        eventLogItems = eventLogItemsResult;
                        return eventLogItemsResult;
                    }

                    await IssueRetry(c, () => this.Post<object>($"/api/errors/{c.UniqueMessageId}/retry"));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
            Assert.IsTrue(eventLogItems.Any(item => item.Description.Equals("Failed message resolved by retry") && item.RelatedTo.Contains("/message/" + failure.UniqueMessageId)));
        }

        [Test]
        public async Task Should_show_up_as_resolved_when_doing_a_multi_retry()
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var failedMessageResult = await GetFailedMessage(c);
                    failure = failedMessageResult;
                    if (!failedMessageResult)
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    await IssueRetry(c, () => this.Post("/api/errors/retry", new List<string> {c.UniqueMessageId}));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        [Test]
        public async Task Should_show_up_as_resolved_when_doing_a_retry_all()
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var failedMessageResult = await GetFailedMessage(c);
                    failure = failedMessageResult;
                    if (!failedMessageResult)
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    await IssueRetry(c, () => this.Post<object>("/api/errors/retry/all"));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        [Test]
        public async Task Acknowledging_the_retry_should_be_successful()
        {
            FailedMessage failure;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var failedMessageResult = await GetFailedMessage(c);
                    failure = failedMessageResult;
                    if (!failedMessageResult)
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    await IssueRetry(c, () => this.Post<object>($"/api/recoverability/groups/{failure.FailureGroups.First().Id}/errors/retry"));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            // TODO: How did this ever work. The API should be stopped at this point
            //await this.Delete($"/api/recoverability/unacknowledgedgroups/{failure.FailureGroups.First().Id}"); // Exception will throw if 404
        }

        [Test]
        public async Task Should_show_up_as_resolved_when_doing_a_retry_all_for_the_given_endpoint()
        {
            FailedMessage failure = null;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    var failedMessageResult = await GetFailedMessage(c);
                    failure = failedMessageResult;
                    if (!failedMessageResult)
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    await IssueRetry(c, () => this.Post<object>($"/api/errors/{c.EndpointNameOfReceivingEndpoint}/retry/all"));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c)
        {
            if (c.MessageId == null)
            {
                return Task.FromResult(SingleResult<FailedMessage>.Empty);
            }

            return this.TryGet<FailedMessage>("/api/errors/" + c.UniqueMessageId);
        }

        async Task IssueRetry(MyContext c, Func<Task> retryAction)
        {
            if (!c.RetryIssued)
            {
                c.RetryIssued = true;

                await retryAction().ConfigureAwait(false);
            }
        }


        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.NoRetries();
                    c.ReportSuccessfulRetriesToServiceControl();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.Out.WriteLine("Handling message");
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.LocalAddress = Settings.LocalAddress();
                    Context.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!Context.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }

                    return Task.FromResult(0);
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public bool RetryIssued { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
            public string LocalAddress { get; set; }
        }
    }
}