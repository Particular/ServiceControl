namespace ServiceBus.Management.AcceptanceTests.Migrations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_retry_is_inflight_before_split_with_new_uniquemessageid_retry_header : AcceptanceTest
    {
        const string NewRetryUniqueMessageIdHeader = "ServiceControl.Retry.UniqueMessageId";
        const string OldRetryUniqueMessageIdHeader = "ServiceControl.RetryId";

        [Test]
        public async Task It_should_be_added_as_attempt_to_failedmessage_when_it_fails_with_new_header()
        {
            var context = await Define<Context>(ctx =>
                {
                    ctx.HeaderKey = NewRetryUniqueMessageIdHeader;
                })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .DoNotFailOnErrorMessages()
                    .When(async ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }

                        return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    }, async (bus, ctx) =>
                    {
                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(async ctx =>
                {
                    if (!ctx.Retried) return false;

                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}", m => m.ProcessingAttempts.Count == 2);
                })
                .Run();

            Assert.AreEqual(context.RetryUniqueMessageId, context.OriginalUniqueMessageId);
        }

        [Test]
        public async Task It_should_mark_failedmessage_resolved_when_it_succeeds_with_new_header()
        {
            var context = await Define<Context>(ctx =>
                {
                    ctx.HeaderKey = NewRetryUniqueMessageIdHeader;
                    ctx.HasSuccessInTheEnd = true;
                })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .DoNotFailOnErrorMessages()
                    .When(async ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }

                        return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    }, async (bus, ctx) =>
                    {
                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(async ctx =>
                {
                    if (!ctx.Retried) return false;

                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}", m => m.Status == FailedMessageStatus.Resolved);
                })
                .Run();

            Assert.AreEqual(context.RetryUniqueMessageId, context.OriginalUniqueMessageId);
        }

        [Test]
        public async Task It_should_be_added_as_attempt_to_failedmessage_when_it_fails_with_old_header()
        {
            var context = await Define<Context>(ctx =>
            {
                ctx.HeaderKey = OldRetryUniqueMessageIdHeader;
            })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .DoNotFailOnErrorMessages()
                    .When(async ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }

                        return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    }, async (bus, ctx) =>
                    {
                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(async ctx =>
                {
                    if (!ctx.Retried) return false;

                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}", m => m.ProcessingAttempts.Count == 2);
                })
                .Run();

            Assert.AreEqual(context.RetryUniqueMessageId, context.OriginalUniqueMessageId);
        }

        [Test]
        public async Task It_should_mark_failedmessage_resolved_when_it_succeeds_with_old_header()
        {
            var context = await Define<Context>(ctx =>
            {
                ctx.HeaderKey = OldRetryUniqueMessageIdHeader;
                ctx.HasSuccessInTheEnd = true;
            })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .DoNotFailOnErrorMessages()
                    .When(async ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }

                        return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                    }, async (bus, ctx) =>
                    {
                        await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(async ctx =>
                {
                    if (!ctx.Retried) return false;

                    return await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}", m => m.Status == FailedMessageStatus.Resolved);
                })
                .Run();

            Assert.AreEqual(context.RetryUniqueMessageId, context.OriginalUniqueMessageId);
        }

        protected class FailingMessage : ICommand
        {
        }

        protected class FakePublisher : EndpointConfigurationBuilder
        {
            public FakePublisher()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    c.Recoverability().Delayed(x => x.NumberOfRetries(0));
                });
            }
        }

        protected class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.Recoverability().Delayed(x => x.NumberOfRetries(0));
                });
            }

            public class FailingMessageHandler : IHandleMessages<FailingMessage>
            {
                public Context TestContext { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public Task Handle(FailingMessage message, IMessageHandlerContext context)
                {
                    TestContext.ProcessingAttemptCount++;
                    Console.WriteLine($"Handling message {context.MessageId} attempt {TestContext.ProcessingAttemptCount}");

                    if (context.MessageHeaders.ContainsKey(TestContext.HeaderKey))
                    {
                        Console.WriteLine($"Retrying message {context.MessageId}");
                        TestContext.RetryUniqueMessageId = context.MessageHeaders[TestContext.HeaderKey];
                        TestContext.Retried = true;
                    }
                    else if (TestContext.ProcessingAttemptCount == 1)
                    {
                        TestContext.MessageId = context.MessageId;
                        TestContext.ReplyToAddress = context.ReplyToAddress;

                        TestContext.UniqueMessageId = DeterministicGuid.MakeId(context.MessageId, Settings.EndpointName()).ToString();
                    }

                    if (TestContext.Retried && TestContext.HasSuccessInTheEnd)
                    {
                        return Task.FromResult(0);
                    }

                    throw new Exception("Simulated Exception");
                }
            }
        }

        protected class Context : ScenarioContext
        {
            public string MessageId { get; set; }
            public string ReplyToAddress { get; set; }

            public string OriginalUniqueMessageId => string.IsNullOrEmpty(ReplyToAddress)
                ? ""
                : DeterministicGuid.MakeId(MessageId, ReplyToAddress).ToString();

            public string HeaderKey { get; set; }
            public string RetryUniqueMessageId { get; internal set; }
            public bool Retried { get; internal set; }
            public string UniqueMessageId { get; internal set; }

            public bool HasSuccessInTheEnd { get; set; }

            public bool RetrySent { get; set; }

            public int ProcessingAttemptCount { get; set; }
        }

        protected class RetryUniqueMessageIdMutator : IMutateIncomingTransportMessages
        {
            public Context TestContext { get; set; }

            public Task MutateIncoming(MutateIncomingTransportMessageContext context)
            {
                Console.WriteLine($"Mutating message {context.Headers[Headers.MessageId]}");

                if (context.Headers.ContainsKey(NewRetryUniqueMessageIdHeader))
                {
                    Console.WriteLine($"Found retry uniquemessageid header {TestContext.HeaderKey}: {context.Headers[NewRetryUniqueMessageIdHeader]}. Using {TestContext.OriginalUniqueMessageId}");

                    context.Headers.Remove(NewRetryUniqueMessageIdHeader);
                    context.Headers.Add(TestContext.HeaderKey, TestContext.OriginalUniqueMessageId);
                }

                return Task.FromResult(0);
            }
        }
    }
}
