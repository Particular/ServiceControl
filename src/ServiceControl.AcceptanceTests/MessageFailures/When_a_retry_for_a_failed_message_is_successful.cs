namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.EventLog;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.ProductionDebugging.Api;

    public class When_a_retry_for_a_failed_message_is_successful : AcceptanceTest
    {
        [Test]
        public void Should_show_up_as_resolved_when_doing_a_single_retry()
        {
            FailedMessage failure = null;
            MessagesView message = null;
            List<EventLogItem> eventLogItems = null;

            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (!GetFailedMessage(c, out failure))
                    {
                        return false;
                    }
                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return TryGetSingle("/api/messages", out message, m => m.Status == MessageStatus.ResolvedSuccessfully)
                            && TryGetMany("/api/eventlogitems", out eventLogItems);
                    }

                    IssueRetry(c, () => Post<object>(String.Format("/api/errors/{0}/retry", c.UniqueMessageId)));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
            Assert.AreEqual(failure.UniqueMessageId, message.Id);
            Assert.AreEqual(MessageStatus.ResolvedSuccessfully, message.Status);
            Assert.AreEqual("Failed message ServiceBus.Management.AcceptanceTests.When_a_retry_for_a_failed_message_is_successful+MyMessage resolved by retry", eventLogItems.Last().Description);

        }

        [Test]
        public void Should_show_up_as_resolved_when_doing_a_multi_retry()
        {
            FailedMessage failure = null;

            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (!GetFailedMessage(c, out failure))
                    {
                        return false;
                    }
                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    IssueRetry(c, () => Post(String.Format("/api/errors/retry"), new List<string> { c.UniqueMessageId }));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        [Test]
        public void Should_show_up_as_resolved_when_doing_a_retry_all()
        {
            FailedMessage failure = null;

            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (!GetFailedMessage(c, out failure))
                    {
                        return false;
                    }
                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    IssueRetry(c, () => Post<object>(String.Format("/api/errors/retry/all")));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

            Assert.AreEqual(FailedMessageStatus.Resolved, failure.Status);
        }

        [Test]
        public void Should_show_up_as_resolved_when_doing_a_retry_all_for_the_given_endpoint()
        {
            FailedMessage failure = null;

            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (!GetFailedMessage(c, out failure))
                    {
                        return false;
                    }
                    if (failure.Status == FailedMessageStatus.Resolved)
                    {
                        return true;
                    }

                    IssueRetry(c, () => Post<object>(String.Format("/api/errors/{0}/retry/all", c.EndpointNameOfReceivingEndpoint)));

                    return false;
                })
                .Run(TimeSpan.FromMinutes(2));

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

        void IssueRetry(MyContext c, Action retryAction)
        {
            if (c.RetryIssued)
            {
                Thread.Sleep(1000); //todo: add support for a "default" delay when Done() returns false
            }
            else
            {
                c.RetryIssued = true;

                retryAction();
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

                    if (!Context.RetryIssued) //simulate that the exception will be resolved with the retry
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

            public bool RetryIssued { get; set; }

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