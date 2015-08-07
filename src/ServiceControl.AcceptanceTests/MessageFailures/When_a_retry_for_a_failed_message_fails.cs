namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_retry_for_a_failed_message_fails : AcceptanceTest
    {
        [Test]
        public void It_should_be_marked_as_unresolved()
        {
            var context = new MyContext { Succeed = false };

            FailedMessage failure = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    FailedMessage localFailure;
                    if (!GetFailedMessage(c, out localFailure))
                        return false;

                    if (localFailure.ProcessingAttempts.Count == 3)
                        failure = localFailure;

                    if (c.RetryCount < 2 && (localFailure.ProcessingAttempts.Count - 1) == c.RetryCount)
                        IssueRetry(c);

                    return failure != null;
                })
                .Run(TimeSpan.FromMinutes(3));

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
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    FailedMessage localFailure;
                    if (!GetFailedMessage(c, out localFailure))
                        return false;

                    if (localFailure.Status == FailedMessageStatus.Resolved) // can't use processing attempts as they don't get updated on success
                        failure = localFailure;

                    if (localFailure.ProcessingAttempts.Count == 3 && c.RetryCount == 2)
                    {
                        c.Succeed = true;
                        IssueRetry(c);
                        return false;
                    }

                    if (c.RetryCount < 2 && (localFailure.ProcessingAttempts.Count - 1) == c.RetryCount)
                        IssueRetry(c);

                    return failure != null;
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.IsNotNull(failure, "Failure should not be null");
            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        bool GetFailedMessage(MyContext c, out FailedMessage failure)
        {
            failure = null;
            if (c.MessageId == null)
            {
                return false;
            }

            if (!TryGet("/api/errors/" + c.UniqueMessageId, out failure))
            {
                return false;
            }
            return true;
        }

        static object lockObj = new object();

        void IssueRetry(MyContext c)
        {
            lock (lockObj)
            {
                var now = DateTimeOffset.UtcNow;
                if (now - c.LastRetry < TimeSpan.FromSeconds(1))
                {
                    return;
                }
                c.LastRetry = now;
                c.RetryCount = c.RetryCount + 1;
                Post<object>(String.Format("/api/errors/{0}/retry", c.UniqueMessageId));
                Console.WriteLine("Retry {0} issued", c.RetryCount);
            }
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServer>(c => Configure.Features.Disable<SecondLevelRetries>())
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

                public void Handle(MyMessage message)
                {
                    Console.Out.WriteLine("Handling message");
                    Context.EndpointNameOfReceivingEndpoint = Configure.EndpointName;
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

            public string UniqueMessageId
            {
                get
                {
                    return DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
                }
            }
        }
    }
}