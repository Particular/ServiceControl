namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_failed_message_is_pending_retry : AcceptanceTest
    {
        [Test]
        public async Task Should_status_retryissued_after_retry_is_sent()
        {
            FailedMessage failedMessage;

            var context = await Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.Given(bus =>
                {
                    bus.SendLocal(new MyMessage());
                }).When(async ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    var result = await TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    failedMessage = result;
                    return result;
                }, async (bus, ctx) =>
                {
                    ctx.AboutToSendRetry = true;
                    await Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                }))
                .Done(ctx => ctx.Retried)
                .Run();

            failedMessage = await TryGet<FailedMessage>($"/api/errors/{context.UniqueMessageId}");

            Assert.AreEqual(failedMessage.Status, FailedMessageStatus.RetryIssued,"Status was not set to RetryIssued");
        }

        public class FailingEndpoint : EndpointConfigurationBuilder
        {
            public FailingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.DisableFeature<SecondLevelRetries>();
                })
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    });
            }

            class CustomConfig : INeedInitialization
            {
                public void Customize(BusConfiguration configuration)
                {
                    configuration.DisableFeature<Outbox>();
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.AboutToSendRetry)
                    {
                        Context.Retried = true;
                    }
                    else
                    {
                        Context.UniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id.Replace(@"\", "-"), Settings.LocalAddress().Queue).ToString();
                        throw new Exception("Simulated Exception");
                    }
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
        { }
    }
}