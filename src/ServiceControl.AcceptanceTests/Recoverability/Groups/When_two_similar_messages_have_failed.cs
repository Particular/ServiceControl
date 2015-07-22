namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    public class When_two_similar_messages_have_failed : AcceptanceTest
    {
        [Test]
        public void They_should_be_grouped_together()
        {
            var context = new MyContext();

            List<FailureGroupView> groups = null;
            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    bus.SendLocal<MyMessage>(m => m.IsFirst = true);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    bus.SendLocal<MyMessage>(m => m.IsFirst = false);
                }))
                .Done(c =>
                {
                    if (!c.FirstDone || !c.SecondDone)
                        return false;

                    if (!(TryGetMany("/api/recoverability/groups/", out groups) && groups.Any()))
                        return false;

                    if (!TryGet("/api/errors/" + c.FirstMessageId, out firstFailure))
                        return false;

                    if (!TryGet("/api/errors/" + c.SecondMessageId, out secondFailure))
                        return false;

                    return true;
                })
                .Run();

            Assert.IsNotNull(groups, "Group should be created");
            Assert.IsNotNull(firstFailure, "The first failure message should be created");
            Assert.IsNotNull(secondFailure, "The second failure message should be created");

            var failureGroup = groups.First();
            Assert.AreEqual(2, failureGroup.Count, "Group should have both messages in it");
            var firstFailureAttempt = firstFailure.ProcessingAttempts.Last();
            var lastFailureAttempt = secondFailure.ProcessingAttempts.Last();

            Assert.AreNotEqual(firstFailureAttempt.AttemptedAt, lastFailureAttempt.AttemptedAt, "Failures should have occurred at different times");
            Assert.AreEqual(firstFailureAttempt.AttemptedAt, failureGroup.First, "Failure Group should start when the first message arrived");
            Assert.AreEqual(lastFailureAttempt.AttemptedAt, failureGroup.Last, "Failure Group should end when the last message arrived");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => Configure.Features.Disable<SecondLevelRetries>())
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

                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Configure.EndpointName).ToString();

                    if (message.IsFirst)
                    {
                        Context.FirstDone = true;
                        Context.FirstMessageId = uniqueMessageId;
                    }
                    else
                    {
                        Context.SecondDone = true;
                        Context.SecondMessageId = uniqueMessageId;
                    }

                    throw new Exception("Simulated exception");
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public bool IsFirst { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public bool FirstDone { get; set; }
            public string FirstMessageId { get; set; }

            public bool SecondDone { get; set; }
            public string SecondMessageId { get; set; }
        }
    }
}
