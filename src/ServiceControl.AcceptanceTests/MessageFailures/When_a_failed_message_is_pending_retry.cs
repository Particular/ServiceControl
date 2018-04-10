namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
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
        public void Should_status_retryissued_after_retry_is_sent()
        {
            FailedMessage failedMessage;

            var context = Define<Context>()
                .WithEndpoint<FailingEndpoint>(b => b.Given(bus =>
                {
                    bus.SendLocal(new MyMessage());
                }).When(ctx =>
                {
                    if (ctx.UniqueMessageId == null)
                    {
                        return false;
                    }

                    return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);
                }, (bus, ctx) =>
                {
                    Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                    ctx.RetrySent = true;
                }))
                .Done(ctx => ctx.Retried)
                .Run();

            TryGet($"/api/errors/{context.UniqueMessageId}", out failedMessage);

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

            public class MyMessageHandler : IHandleMessages<When_a_pending_retry_is_retried_again.MyMessage>
            {
                public When_a_pending_retry_is_retried_again.Context Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(When_a_pending_retry_is_retried_again.MyMessage message)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.RetrySent)
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

        public class DecoyFailingEndpoint : EndpointConfigurationBuilder
        {
            public DecoyFailingEndpoint()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 1;
                    });
            }

            public class MyMessageHandler : IHandleMessages<When_a_pending_retry_is_retried_again.MyMessage>
            {
                public When_a_pending_retry_is_retried_again.Context Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(When_a_pending_retry_is_retried_again.MyMessage message)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.RetrySent)
                    {
                        Context.DecoyRetried = true;
                    }
                    else
                    {
                        Context.DecoyProcessed = true;
                        throw new Exception("Simulated Exception");
                    }
                }
            }
        }

        public class Context : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public bool Retried { get; set; }
            public bool RetrySent { get; set; }
            public int RetryCount { get; set; }
            public string FromAddress { get; set; }
            public bool DecoyProcessed { get; set; }
            public bool DecoyRetried { get; set; }
        }

        public class MyMessage : ICommand
        { }
    }
}