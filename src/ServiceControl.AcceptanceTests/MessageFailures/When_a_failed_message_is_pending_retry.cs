namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_failed_message_is_pending_retry : AcceptanceTest
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
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.EnableFeature<Outbox>();

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
                    protected override Task OnStart(IMessageSession session)
                    {
                        return session.SendLocal(new MyMessage());
                    }

                    protected override Task OnStop(IMessageSession session)
                    {
                        return Task.FromResult(0);
                    }
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.AboutToSendRetry)
                    {
                        Context.Retried = true;
                    }
                    else
                    {
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
            public bool AboutToSendRetry { get; set; }
        }

        public class MyMessage : ICommand
        {
        }
    }
}