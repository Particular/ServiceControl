namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_pending_retry_is_retried_by_queue_and_timeframe : AcceptanceTest
    {
        [Test]
        public async Task Should_succeed()
        {
            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("DetectFailure", async ctx =>
                {
                    return ctx.UniqueMessageId != null
                           && await this.TryGet<FailedMessage>($"/errors/{ctx.UniqueMessageId}");
                })
                .Do("Retry", async ctx => { await this.Post<object>($"/errors/{ctx.UniqueMessageId}/retry"); })
                .Do("WaitForRetryIssued", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/errors/{ctx.UniqueMessageId}",
                        msg => msg.Status == FailedMessageStatus.RetryIssued);
                })
                .Do("WaitForIndex", async ctx =>
                {
                    return await this.TryGet<FailedMessage[]>("/errors",
                        allErrors => allErrors.Any(fm => fm.Id == ctx.UniqueMessageId));
                })
                .Do("RetryPending", async ctx =>
                {
                    await this.Post<object>("/pendingretries/queues/retry", new
                    {
                        queueaddress = ctx.FromAddress,
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.AddSeconds(10).ToString("o")
                    });
                })
                .Done(ctx => ctx.RetryCount == 2)
                .Run();
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
                    if (Context.Step == 0)
                    {
                        Context.FromAddress = Settings.LocalAddress();
                        Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, Settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    Context.RetryCount++;
                    Context.Retried = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class Context : ScenarioContext, ISequenceContext
        {
            public string UniqueMessageId { get; set; }
            public bool Retried { get; set; }
            public int RetryCount { get; set; }
            public string FromAddress { get; set; }
            public int Step { get; set; }
        }

        public class MyMessage : ICommand
        {
        }
    }
}