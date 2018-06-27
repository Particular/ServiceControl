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
            var machine = new StateMachine<Context, State>()
                .When(State.Begin, async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return State.Begin;
                    }
                    var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    return result ? State.FailedMessageDetected : State.Begin;
                })
                .When(State.FailedMessageDetected, async ctx =>
                {
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    return State.RetryRequested;
                })
                .When(State.RetryRequested, async ctx =>
                {
                    var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}", msg => msg.Status == FailedMessageStatus.RetryIssued);
                    return result ? State.RetryIssued : State.RetryRequested;
                })
                .When(State.RetryIssued, async ctx =>
                {
                    var result = await this.TryGet<FailedMessage[]>("/api/errors", allErrors => allErrors.Any(fm => fm.Id == ctx.UniqueMessageId));
                    return result ? State.IndexUpdated : State.RetryIssued;
                })
                .When(State.IndexUpdated, async ctx =>
                {
                    await this.Post<object>("/api/pendingretries/queues/retry", new
                    {
                        queueaddress = ctx.FromAddress,
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.AddSeconds(10).ToString("o")
                    });
                    return State.SecondRetryIssued;
                });

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.State == State.SecondRetryIssued)
                    {
                        return ctx.RetryCount == 2;
                    }
                    await machine.Step(ctx).ConfigureAwait(false);
                    return false;
                })
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
                    if (Context.State == State.Begin)
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
        public enum State
        {
            Begin,
            FailedMessageDetected,
            RetryRequested,
            RetryIssued,
            IndexUpdated,
            SecondRetryIssued
        }

        public class Context : ScenarioContext, IStateMachineContext<State>
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