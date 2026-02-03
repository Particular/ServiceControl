namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;

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

            Assert.That(failedMessage.Status, Is.EqualTo(FailedMessageStatus.RetryIssued), "Status was not set to RetryIssued");
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.GetSettings().Get<TransportDefinition>().TransportTransactionMode =
                        TransportTransactionMode.ReceiveOnly;
                    c.EnableFeature<Outbox>();
                    c.RegisterStartupTask(new SendMessageAtStart());
                    c.DisableFeature<PlatformRetryNotifications>();
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(s => s.NumberOfRetries(1));
                    recoverability.Delayed(s => s.NumberOfRetries(0));
                });

            class SendMessageAtStart : FeatureStartupTask
            {
                protected override Task OnStart(IMessageSession session, CancellationToken cancellationToken = default) => session.SendLocal(new MyMessage(), cancellationToken);

                protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
            }

            public class MyMessageHandler(Context scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
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

        public class MyMessage : ICommand;
    }
}