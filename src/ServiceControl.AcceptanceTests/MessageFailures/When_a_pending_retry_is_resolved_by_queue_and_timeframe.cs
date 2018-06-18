namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_pending_retry_is_resolved_by_queue_and_timeframe : AcceptanceTest
    {
        [Test]
        public async Task Should_succeed()
        {
            FailedMessage failedMessage;

            var context = await Define<Context>()
                .WithEndpoint<DecoyFailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())))
                .WithEndpoint<FailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage()))
                .When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    if (!ctx.DecoyProcessed)
                    {
                        return false;
                    }

                    var result = await TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    failedMessage = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (!ctx.RetryAboutToBeSent)
                    {
                        ctx.RetryAboutToBeSent = true;
                        await Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    //We can't just return true here because the index might not have been updated yet.
                    //Following code ensures that the failed message index contains the message
                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        var failedMessagesResult = await TryGet<FailedMessage[]>("/api/errors");
                        FailedMessage[] failedMessages = failedMessagesResult;
                        if (failedMessagesResult)
                        {
                            return failedMessages.Any(fm => fm.Id == ctx.UniqueMessageId);
                        }
                    }

                    return false;
                }, async (bus, ctx) =>
                {
                    await Patch("/api/pendingretries/queues/resolve", new
                    {
                        queueaddress = ctx.FromAddress,
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.ToString("o")
                    });
                }))
                .Done(async ctx =>
                {
                    var result = await TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    failedMessage = result;

                    if (failedMessage.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    return false;
                })
                .Run();

            Assert.False(context.DecoyRetried, "Decoy was retried");
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(x => x.NumberOfRetries(0));
                    recoverability.Delayed(x => x.NumberOfRetries(0));

                    c.DisableFeature<Outbox>();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.RetryAboutToBeSent)
                    {
                        Context.RetryCount++;
                        Context.Retried = true;
                    }
                    else
                    {
                        Context.FromAddress = Settings.LocalAddress();
                        Context.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId.Replace(@"\", "-"), Settings.LocalAddress()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    return Task.FromResult(0);
                }
            }
        }

        public class DecoyFailingEndpoint : EndpointConfigurationBuilder
        {
            public DecoyFailingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                    {
                        c.NoRetries();
                    });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.RetryAboutToBeSent)
                    {
                        Context.DecoyRetried = true;
                    }
                    else
                    {
                        Context.DecoyProcessed = true;
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
            public bool RetryAboutToBeSent { get; set; }
            public int RetryCount { get; set; }
            public string FromAddress { get; set; }
            public bool DecoyProcessed { get; set; }
            public bool DecoyRetried { get; set; }
        }

        public class MyMessage : ICommand
        { }
    }
}