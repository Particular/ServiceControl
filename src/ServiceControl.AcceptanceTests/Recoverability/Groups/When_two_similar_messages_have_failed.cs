namespace ServiceBus.Management.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
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
    using ServiceControl.Recoverability;

    public class When_two_similar_messages_have_failed : AcceptanceTest
    {
        [Test]
        public async Task They_should_be_grouped_together()
        {
            var context = new MyContext();

            List<FailureGroupView> exceptionTypeAndStackTraceGroups = null;
            List<FailureGroupView> messageTypeGroups = null;
            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;

            await Define(context)
                .WithEndpoint<Receiver>(b => b.Given(bus =>
                {
                    bus.SendLocal<MyMessage>(m => m.IsFirst = true);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    bus.SendLocal<MyMessage>(m => m.IsFirst = false);
                }))
                .Done(async c =>
                {
                    if (!c.FirstDone || !c.SecondDone)
                        return false;

                    var result = await TryGetMany<FailureGroupView>("/api/recoverability/groups/");
                    exceptionTypeAndStackTraceGroups = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (exceptionTypeAndStackTraceGroups.Any(x => x.Count != 2))
                    {
                        return false;
                    }

                    messageTypeGroups = await TryGetMany<FailureGroupView>("/api/recoverability/groups/Message%20Type");

                    var firstFailureResult = await TryGet<FailedMessage>("/api/errors/" + c.FirstMessageId);
                    firstFailure = firstFailureResult;
                    if (!firstFailureResult)
                    {
                        return false;
                    }

                    var secondFailureResult = await TryGet<FailedMessage>("/api/errors/" + c.SecondMessageId);
                    secondFailure = secondFailureResult;
                    if (!secondFailureResult)
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            Assert.IsNotNull(exceptionTypeAndStackTraceGroups, "Exception Type And Stack Trace Group should be created");
            Assert.IsNotNull(messageTypeGroups, "Message Type Group should be created");
            Assert.IsNotNull(firstFailure, "The first failure message should be created");
            Assert.IsNotNull(secondFailure, "The second failure message should be created");

            Assert.AreEqual(1, exceptionTypeAndStackTraceGroups.Count, "There should only be one Exception Type And Stack Trace Group");
            Assert.AreEqual(1, messageTypeGroups.Count, "There should only be one Message Type Group");

            var failureGroup = exceptionTypeAndStackTraceGroups.First();
            Assert.AreEqual(2, failureGroup.Count, "Exception Type And Stack Trace Group should have both messages in it");

            Assert.AreEqual(2, messageTypeGroups.First().Count, "Message Type Group should have both messages in it");

            var failureTimes = firstFailure.ProcessingAttempts
                        .Union(secondFailure.ProcessingAttempts)
                        .Where(x => x.FailureDetails != null)
                        .Select(x => x.FailureDetails.TimeOfFailure)
                        .ToList();

            Assert.AreEqual(failureTimes.Min(), failureGroup.First, "Failure Group should start when the earliest failure occurred");
            Assert.AreEqual(failureTimes.Max(), failureGroup.Last, "Failure Group should end when the latest failure occurred");
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c =>
                    {
                        c.MaxRetries = 0;
                    });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {

                    var messageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, Settings.LocalAddress().Queue).ToString();

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
