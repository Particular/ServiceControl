namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
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

        [Test]
        public void Can_be_retried_again()
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

                    if (!ctx.RetrySent)
                    {
                        ctx.RetrySent = true;
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
                    return false;
                }, (bus, ctx) =>
                {
                    Post<object>("/api/pendingretries/retry", new List<string>
                    {
                        ctx.UniqueMessageId
                    });
                }))
                .Done(ctx => ctx.RetryCount == 2)
                .Run();
        }

        [Test]
        public void Can_be_retried_again_by_queue_and_timeframe()
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

                    if (!ctx.RetrySent)
                    {
                        ctx.RetrySent = true;
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        return false;
                    }

                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
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

        [Test]
        public void Can_be_marked_as_resolved_by_selection()
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

                    if (!ctx.RetrySent)
                    {
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                        return false;
                    }

                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
                    return false;
                }, (bus, ctx) =>
                {
                    Patch("/api/pendingretries/resolve", new
                    {
                        uniquemessageids = new List<string>
                        {
                            ctx.UniqueMessageId
                        }
                    });
                }))
                .Done(ctx =>
                {
                    TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);

                    if (failedMessage.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
                    return false;
                })
                .Run();
        }

        [Test]
        public void Can_be_marked_as_resolved_by_timeframe()
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

                    if (!ctx.RetrySent)
                    {
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                        return false;
                    }

                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
                    return false;
                }, (bus, ctx) =>
                {
                    Patch("/api/pendingretries/resolve", new
                    {
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.ToString("o")
                    });
                }))
                .Done(ctx =>
                {
                    TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);

                    if (failedMessage.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
                    return false;
                })
                .Run();
        }

        [Test]
        public void Can_be_marked_as_resolved_by_queue_and_timeframe()
        {
            FailedMessage failedMessage;

            var context = Define<Context>()
                .WithEndpoint<DecoyFailingEndpoint>(b => b.Given(bus =>
                {
                    bus.SendLocal(new MyMessage());
                }))
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

                    if (!ctx.RetrySent)
                    {
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                        return false;
                    }

                    if (failedMessage.Status == FailedMessageStatus.RetryIssued)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
                    return false;
                }, (bus, ctx) =>
                {
                    Patch("/api/pendingretries/queues/resolve", new
                    {
                        queueaddress = ctx.FromAddress,
                        from = DateTime.UtcNow.AddHours(-1).ToString("o"),
                        to = DateTime.UtcNow.ToString("o")
                    });
                }))
                .Done(ctx =>
                {
                    TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);

                    if (failedMessage.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    Thread.Sleep(1000);
                    return false;
                })
                .Run();

            Assert.False(context.DecoyRetried, "Decoy was retried");
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
                    if (Context.RetrySent)
                    {
                        Context.RetryCount++;
                        Context.Retried = true;
                    }
                    else
                    {
                        Context.FromAddress = Settings.LocalAddress().ToString();
                        Context.UniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id.Replace(@"\", "-"), Settings.LocalAddress().ToString()).ToString();
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

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }
                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Console.WriteLine("Message Handled");
                    if (Context.RetrySent)
                    {
                        Context.DecoyRetried = true;
                    }
                    else
                    {
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
            public bool DecoyRetried { get; set; }
        }

        public class MyMessage : ICommand
        { }
    }
}
