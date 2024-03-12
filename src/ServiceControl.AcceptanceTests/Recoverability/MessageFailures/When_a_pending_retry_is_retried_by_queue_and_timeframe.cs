﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;

    class When_a_pending_retry_is_retried_by_queue_and_timeframe : AcceptanceTest
    {
        [Test]
        public async Task Should_succeed() =>
            await Define<Context>()
                .WithEndpoint<Failing>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("DetectFailure", async ctx =>
                {
                    return ctx.UniqueMessageId != null
                           && await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                })
                .Do("Retry", async ctx => { await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry"); })
                .Do("WaitForRetryIssued", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}",
                        msg => msg.Status == FailedMessageStatus.RetryIssued);
                })
                .Do("WaitForIndex", async ctx =>
                {
                    return await this.TryGet<FailedMessage[]>("/api/errors",
                        allErrors => allErrors.Any(fm => fm.Id == ctx.UniqueMessageId));
                })
                .Do("RetryPending", async ctx =>
                {
                    await this.Post<object>("/api/pendingretries/queues/retry", new
                    {
                        queueaddress = ctx.FromAddress,
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.AddSeconds(10).ToString("o")
                    });
                })
                .Done(ctx => ctx.RetryCount == 2)
                .Run();

        public class Failing : EndpointConfigurationBuilder
        {
            public Failing() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    //Do not inform SC that the message has been already successfully handled
                    c.DisableFeature<PlatformRetryNotifications>();
                    c.NoRetries();
                    c.NoOutbox();
                });

            public class MyMessageHandler(
                Context scenarioContext,
                IReadOnlySettings settings,
                ReceiveAddresses receiveAddresses)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (scenarioContext.Step == 0)
                    {
                        scenarioContext.FromAddress = receiveAddresses.MainReceiveAddress;
                        scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    scenarioContext.RetryCount++;
                    scenarioContext.Retried = true;
                    return Task.CompletedTask;
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

        public class MyMessage : ICommand;
    }
}