namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_pending_retry_is_retried_by_queue_and_timeframe : AcceptanceTest
    {
        [Test]
        public async Task Should_succeed()
        {
            FailedMessage failedMessage;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    if (ctx.State == State.Begin)
                    {
                        var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                        failedMessage = result;
                        if (result)
                        {
                            ctx.State = State.FailedMessageDetected;
                        }
                        return false;
                    }

                    if (ctx.State == State.FailedMessageDetected)
                    {
                        ctx.State = State.RetryAboutToBeSent;
                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    if (ctx.State == State.RetryAboutToBeSent)
                    {
                        var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                        failedMessage = result;
                        if (result && result.Item.Status == FailedMessageStatus.RetryIssued)
                        {
                            var failedMessagesResult = await this.TryGet<FailedMessage[]>("/api/errors");
                            FailedMessage[] failedMessages = failedMessagesResult;
                            if (failedMessagesResult)
                            {
                                var messagePresent = failedMessages.Any(fm => fm.Id == ctx.UniqueMessageId);
                                if (messagePresent)
                                {
                                    ctx.State = State.SecondRetryAboutToBeSent;
                                    await this.Post<object>("/api/pendingretries/queues/retry", new
                                    {
                                        queueaddress = ctx.FromAddress,
                                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                                        to = DateTime.UtcNow.AddSeconds(10).ToString("o")
                                    });
                                }
                            }
                        }
                        return false;
                    }

                    return ctx.RetryCount == 2;
                })
                .Run();
        }

        public enum State
        {
            Begin,
            FailedMessageDetected,
            RetryAboutToBeSent,
            SecondRetryAboutToBeSent,
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.NoRetries();
                    c.NoOutbox();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.State != State.Begin)
                    {
                        Context.RetryCount++;
                        Context.Retried = true;
                    }
                    else
                    {
                        Context.FromAddress = Settings.LocalAddress();
                        Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, Settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    return Task.FromResult(0);
                }
            }
        }

        public class Context : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public bool Retried { get; set; }
            public State State { get; set; }
            public int RetryCount { get; set; }
            public string FromAddress { get; set; }
        }

        public class MyMessage : ICommand
        { }
    }
}