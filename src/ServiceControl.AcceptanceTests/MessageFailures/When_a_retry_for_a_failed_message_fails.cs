namespace ServiceBus.Management.AcceptanceTests
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

    [Serializable]
    public class When_a_retry_for_a_failed_message_fails : AcceptanceTest
    {
        [Test]
        public void It_should_be_marked_as_unresolved()
        {
            var context = new MyContext { Succeed = false };

            FailedMessage failure = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => 
                    b.Given(bus => bus.SendLocal(new MyMessage()))
                     .When(ctx => CheckProcessingAttemptsIs(ctx, 1),
                        (bus, ctx) => IssueRetry(ctx))
                     .When(ctx => CheckProcessingAttemptsIs(ctx, 2),
                        (bus, ctx) => IssueRetry(ctx))
                )  
                .Done(ctx => GetFailedMessage(ctx, out failure, f => f.ProcessingAttempts.Count == 3))
                .Run(TimeSpan.FromMinutes(4));

            Assert.IsNotNull(failure, "Failure should not be null");
            Assert.AreEqual(FailedMessageStatus.Unresolved, failure.Status);
        }

        [Test]
        public void It_should_be_able_to_be_retried_successfully()
        {
            var context = new MyContext { Succeed = false };

            FailedMessage failure = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => 
                    b.Given(bus => bus.SendLocal(new MyMessage()))
                     .When(ctx => CheckProcessingAttemptsIs(ctx, 1),
                          (bus, ctx) => IssueRetry(ctx))
                     .When(ctx => CheckProcessingAttemptsIs(ctx, 2),
                          (bus, ctx) => IssueRetry(ctx))
                     .When(ctx => CheckProcessingAttemptsIs(ctx, 3),
                         (bus, ctx) =>
                         {
                             ctx.Succeed = true;
                             IssueRetry(ctx);
                         })
                    )
                .Done(ctx => GetFailedMessage(ctx, out failure, f => f.Status == FailedMessageStatus.Resolved))
                .Run(TimeSpan.FromMinutes(4));

            Assert.IsNotNull(failure, "Failure should not be null");
            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        bool CheckProcessingAttemptsIs(MyContext ctx, int count)
        {
            FailedMessage failure;
            return GetFailedMessage(ctx, out failure, f => f.ProcessingAttempts.Count == count);
        }

        bool GetFailedMessage(MyContext c, out FailedMessage failure, Predicate<FailedMessage> condition)
        {
            failure = null;
            if (c.UniqueMessageId == null)
            {
                return false;
            }

            return TryGet("/api/errors/" + c.UniqueMessageId, out failure, condition);
        }

        void IssueRetry(MyContext c)
        {
            Post<object>($"/api/errors/{c.UniqueMessageId}/retry");
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                        {
                            c.MaxRetries = 1;
                        })
                    .AuditTo(Address.Parse("audit"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }
                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    if (!Context.Succeed) //simulate that the exception will be resolved with the retry
                    {
                        throw new Exception("Simulated exception");
                    }
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public int RetryCount { get; set; }

            public DateTimeOffset LastRetry { get; set; }

            public bool Succeed { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
        }
    }
}