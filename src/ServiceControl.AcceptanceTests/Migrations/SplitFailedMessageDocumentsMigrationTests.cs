namespace ServiceBus.Management.AcceptanceTests.Migrations
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NServiceBus.MessageMutator;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_retry_is_inflight_before_split_with_new_uniquemessageid_retry_header : AcceptanceTest
    {
        const string NewRetryUniqueMessageIdHeader = "ServiceControl.Retry.UniqueMessageId";
        const string OldRetryUniqueMessageIdHeader = "ServiceControl.RetryId";

        [Test]
        public void It_should_be_added_as_attempt_to_failedmessage_when_it_fails_with_new_header()
        {
            var context = Define(new Context
                {
                    HeaderKey = NewRetryUniqueMessageIdHeader
                })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .When(ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }
                        FailedMessage failedMessage;
                        return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);
                    }, (bus, ctx) =>
                    {
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(ctx =>
                {
                    if (!ctx.Retried) return false;

                    FailedMessage failedMessage;
                    return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage, m => m.ProcessingAttempts.Count == 2);
                })
                .Run();

            Assert.AreEqual(context.RetryUniqueMessageId, context.OriginalUniqueMessageId);
        }

        [Test]
        public void It_should_mark_failedmessage_resolved_when_it_succeeds_with_new_header()
        {
            var context = Define(new Context
                {
                    HeaderKey = NewRetryUniqueMessageIdHeader,
                    HasSuccessInTheEnd = true
                })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .When(ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }
                        FailedMessage failedMessage;
                        return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);
                    }, (bus, ctx) =>
                    {
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(ctx =>
                {
                    if (!ctx.Retried) return false;

                    FailedMessage failedMessage;
                    return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage, m => m.Status == FailedMessageStatus.Resolved);
                })
                .Run();

            Assert.AreEqual(context.RetryUniqueMessageId, context.OriginalUniqueMessageId);
        }

        [Test]
        public void It_should_be_added_as_attempt_to_failedmessage_when_it_fails_with_old_header()
        {
            var context = Define(new Context
            {
                HeaderKey = OldRetryUniqueMessageIdHeader
            })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .When(ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }
                        FailedMessage failedMessage;
                        return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);
                    }, (bus, ctx) =>
                    {
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(ctx =>
                {
                    if (!ctx.Retried) return false;

                    FailedMessage failedMessage;
                    return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage, m => m.ProcessingAttempts.Count == 2);
                })
                .Run();

            Assert.AreEqual(context.RetryUniqueMessageId, context.OriginalUniqueMessageId);
        }

        [Test]
        public void It_should_mark_failedmessage_resolved_when_it_succeeds_with_old_header()
        {
            var context = Define(new Context
            {
                HeaderKey = OldRetryUniqueMessageIdHeader,
                HasSuccessInTheEnd = true
            })
                .WithEndpoint<FakePublisher>(builder => builder.When(ctx => ctx.EndpointsStarted, bus => bus.Send("Migrations.FailureEndpoint", new FailingMessage())))
                .WithEndpoint<FailureEndpoint>(b =>
                    b.CustomConfig(config =>
                        config.RegisterComponents(components => components
                        .ConfigureComponent<RetryUniqueMessageIdMutator>(DependencyLifecycle.SingleInstance))
                    )
                    .When(ctx =>
                    {
                        if (ctx.UniqueMessageId == null)
                        {
                            return false;
                        }
                        FailedMessage failedMessage;
                        return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage);
                    }, (bus, ctx) =>
                    {
                        Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        ctx.RetrySent = true;
                    })
                )
                .Done(ctx =>
                {
                    if (!ctx.Retried) return false;

                    FailedMessage failedMessage;
                    return TryGet($"/api/errors/{ctx.UniqueMessageId}", out failedMessage, m => m.Status == FailedMessageStatus.Resolved);
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
                    c.DisableFeature<SecondLevelRetries>();
                });
            }
        }

        protected class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.DisableFeature<SecondLevelRetries>();
                });
            }

            public class FailingMessageHandler : IHandleMessages<FailingMessage>
            {
                public IBus Bus { get; set; }
                public Context TestContext { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(FailingMessage message)
                {
                    TestContext.ProcessingAttemptCount++;
                    Console.WriteLine($"Handling message {Bus.CurrentMessageContext.Id} attempt {TestContext.ProcessingAttemptCount}");

                    if (Bus.CurrentMessageContext.Headers.ContainsKey(TestContext.HeaderKey))
                    {
                        Console.WriteLine($"Retrying message {Bus.CurrentMessageContext.Id}");
                        TestContext.RetryUniqueMessageId = Bus.CurrentMessageContext.Headers[TestContext.HeaderKey];
                        TestContext.Retried = true;
                    }
                    else if (TestContext.ProcessingAttemptCount == 1)
                    {
                        TestContext.MessageId = Bus.CurrentMessageContext.Id;
                        TestContext.ReplyToAddress = Bus.CurrentMessageContext.ReplyToAddress.ToString();

                        TestContext.UniqueMessageId = DeterministicGuid.MakeId(Bus.CurrentMessageContext.Id, Settings.LocalAddress().Queue).ToString();
                    }

                    if (TestContext.Retried && TestContext.HasSuccessInTheEnd)
                    {
                        return;
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
                : DeterministicGuid.MakeId(MessageId, Address.Parse(ReplyToAddress).Queue).ToString();

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

            public void MutateIncoming(TransportMessage transportMessage)
            {
                Console.WriteLine($"Mutating message {transportMessage.Id}");

                if (transportMessage.Headers.ContainsKey(NewRetryUniqueMessageIdHeader))
                {
                    Console.WriteLine($"Found retry uniquemessageid header {TestContext.HeaderKey}: {transportMessage.Headers[NewRetryUniqueMessageIdHeader]}. Using {TestContext.OriginalUniqueMessageId}");

                    transportMessage.Headers.Remove(NewRetryUniqueMessageIdHeader);
                    transportMessage.Headers.Add(TestContext.HeaderKey, TestContext.OriginalUniqueMessageId);
                }
            }
        }
    }
}
