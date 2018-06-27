namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_pending_retry_is_resolved_by_selection : AcceptanceTest
    {
        [Test]
        public async Task Should_succeed()
        {
            FailedMessage failedMessage;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages()
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

                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        return true;
                    }

                    return false;
                }, async (bus, ctx) =>
                {
                    await this.Patch("/api/pendingretries/resolve", new
                    {
                        uniquemessageids = new List<string>
                        {
                            ctx.UniqueMessageId
                        }
                    });
                }).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    failedMessage = result;

                    if (failedMessage?.Status == FailedMessageStatus.Resolved)
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
            public bool RetryAboutToBeSent { get; set; }
            public int RetryCount { get; set; }
            public string FromAddress { get; set; }
        }

        public class MyMessage : ICommand
        { }
    }
}
