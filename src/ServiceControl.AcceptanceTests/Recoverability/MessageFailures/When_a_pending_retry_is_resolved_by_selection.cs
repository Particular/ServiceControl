﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport.EndpointTemplates;

    class When_a_pending_retry_is_resolved_by_selection : AcceptanceTest
    {
        [Test]
        public async Task Should_succeed()
        {
            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Do("DetectFailure", async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                })
                .Do("Retry", async ctx => { await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry"); })
                .Do("WaitForRetryIssued", async ctx => { return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}", msg => msg.Status == FailedMessageStatus.RetryIssued); })
                .Do("ResolvePending", async ctx =>
                {
                    await this.Patch("/api/pendingretries/resolve", new
                    {
                        uniquemessageids = new List<string>
                        {
                            ctx.UniqueMessageId
                        }
                    });
                })
                .Do("WaitForResolved", async ctx =>
                {
                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}",
                        message => message.Status == FailedMessageStatus.Resolved);
                })
                .Done(ctx => true) //We're done once the sequence is finished
                .Run();
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //Do not inform SC that the message has been already successfully handled
                    c.DisableFeature<PlatformRetryNotifications>();
                    c.NoRetries();
                    c.NoOutbox();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context scenarioContext;
                readonly IReadOnlySettings settings;
                readonly ReceiveAddresses receiveAddresses;

                public MyMessageHandler(Context scenarioContext, IReadOnlySettings settings, ReceiveAddresses receiveAddresses)
                {
                    this.scenarioContext = scenarioContext;
                    this.settings = settings;
                    this.receiveAddresses = receiveAddresses;
                }

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

        public class MyMessage : ICommand
        {
        }
    }
}