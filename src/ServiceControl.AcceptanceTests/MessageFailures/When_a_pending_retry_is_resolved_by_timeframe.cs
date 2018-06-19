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

    public class When_a_pending_retry_is_resolved_by_timeframe : AcceptanceTest
    {
        [Test]
        public async Task Should_succeed()
        {
            FailedMessage failedMessage;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage()))
                .When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    failedMessage = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (!ctx.RetryAboutToBeSent)
                    {
                        ctx.RetryAboutToBeSent = true;
                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    //We can't just return true here because the index might not have been updated yet.
                    //Following code ensures that the failed message index contains the message
                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        var failedMessagesResult = await this.TryGet<FailedMessage[]>("/api/errors");
                        FailedMessage[] failedMessages = failedMessagesResult;
                        if (failedMessagesResult)
                        {
                            return failedMessages.Any(fm => fm.Id == ctx.UniqueMessageId);
                        }
                    }

                    return false;
                }, async (bus, ctx) =>
                {
                    await this.Patch("/api/pendingretries/resolve", new
                    {
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.ToString("o")
                    });
                }))
                .Done(async ctx =>
                {
                    var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    failedMessage = result;

                    if (failedMessage.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

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

                public async Task Handle(MyMessage message, IMessageHandlerContext context)
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
        }

        public class MyMessage : ICommand
        { }
    }
}