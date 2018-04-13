namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Linq;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_pending_retry_is_retried_by_queue_and_timeframe : AcceptanceTest
    {
        [Test]
        public void Should_succeed()
        {
            FailedMessage failedMessage;

            Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.Given(bus =>
                {
                    bus.SendLocal(new MyMessage());
                }).When(ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    if (!TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage))
                    {
                        return false;
                    }

                    if (!ctx.RetryAboutToBeSent)
                    {
                        ctx.RetryAboutToBeSent = true;
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        FailedMessage[] failedMessages;
                        if (TryGet("/api/errors", out failedMessages))
                        {
                            return failedMessages.Any(fm => fm.Id == ctx.UniqueMessageId);
                        }
                    }

                    return false;
                }, (bus, ctx) =>
                {
                    Post<object>("/api/pendingretries/queues/retry", new
                    {
                        queueaddress = ctx.FromAddress,
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.AddSeconds(10).ToString("o")
                    });
                }))
                .Done(ctx => ctx.RetryCount == 2)
                .Run();
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
                    if (Context.RetryAboutToBeSent)
                    {
                        Context.RetryCount++;
                        Context.Retried = true;
                    }
                    else
                    {
                        Context.FromAddress = Settings.LocalAddress().ToString();
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
            public bool RetryAboutToBeSent { get; set; }
            public int RetryCount { get; set; }
            public string FromAddress { get; set; }
        }

        public class MyMessage : ICommand
        { }
    }
}