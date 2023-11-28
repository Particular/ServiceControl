﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using TestSupport.EndpointTemplates;

    class When_a_failed_message_is_pending_retry : AcceptanceTest
    {
        [Test]
        public async Task Should_status_retryissued_after_retry_is_sent()
        {
            FailedMessage failedMessage = null;

            await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    failedMessage = result;
                    return result;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                }).DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (ctx.Retried)
                    {
                        failedMessage = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                        return true;
                    }

                    return false;
                })
                .Run();

            Assert.AreEqual(failedMessage.Status, FailedMessageStatus.RetryIssued, "Status was not set to RetryIssued");
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.GetSettings().Get<TransportDefinition>().TransportTransactionMode =
                        TransportTransactionMode.ReceiveOnly;
                    c.EnableFeature<Outbox>();
                    c.DisableFeature<PlatformRetryNotifications>();
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(s => s.NumberOfRetries(1));
                    recoverability.Delayed(s => s.NumberOfRetries(0));
                });
            }

            class StartFeature : Feature
            {
                public StartFeature()
                {
                    EnableByDefault();
                }

                protected override void Setup(FeatureConfigurationContext context)
                {
                    context.RegisterStartupTask(new SendMessageAtStart());
                }

                class SendMessageAtStart : FeatureStartupTask
                {
                    protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
                    {
                        return session.SendLocal(new MyMessage(), cancellationToken);
                    }

                    protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default)
                    {
                        return Task.CompletedTask;
                    }
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyMessageHandler(Context scenarioContext, IReadOnlySettings settings)
                {
                    this.scenarioContext = scenarioContext;
                    this.settings = settings;
                }

                readonly Context scenarioContext;
                readonly IReadOnlySettings settings;

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (scenarioContext.AboutToSendRetry)
                    {
                        scenarioContext.Retried = true;
                    }
                    else
                    {
                        scenarioContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, settings.EndpointName()).ToString();
                        throw new Exception("Simulated Exception");
                    }

                    return Task.CompletedTask;
                }
            }
        }

        public class Context : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public bool Retried { get; set; }
            public bool AboutToSendRetry { get; set; }
        }

        public class MyMessage : ICommand
        {
        }
    }
}