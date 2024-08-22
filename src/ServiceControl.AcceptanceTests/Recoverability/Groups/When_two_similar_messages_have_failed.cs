namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    class When_two_similar_messages_have_failed : AcceptanceTest
    {
        [Test]
        public async Task They_should_be_grouped_together()
        {
            List<FailureGroupView> exceptionTypeAndStackTraceGroups = null;
            List<FailureGroupView> messageTypeGroups = null;
            FailedMessage firstFailure = null;
            FailedMessage secondFailure = null;

            await Define<MyContext>()
                .WithEndpoint<Receiver>(b => b.When(async bus =>
                {
                    await bus.SendLocal<MyMessage>(m => m.IsFirst = true);
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await bus.SendLocal<MyMessage>(m => m.IsFirst = false);
                }).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (!c.FirstDone || !c.SecondDone)
                    {
                        return false;
                    }

                    var result = await this.TryGetMany<FailureGroupView>("/api/recoverability/groups/");
                    exceptionTypeAndStackTraceGroups = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (exceptionTypeAndStackTraceGroups.Any(x => x.Count != 2))
                    {
                        return false;
                    }

                    messageTypeGroups = await this.TryGetMany<FailureGroupView>("/api/recoverability/groups/Message%20Type");

                    var firstFailureResult = await this.TryGet<FailedMessage>($"/api/errors/{c.FirstMessageId}");
                    firstFailure = firstFailureResult;
                    if (!firstFailureResult)
                    {
                        return false;
                    }

                    var secondFailureResult = await this.TryGet<FailedMessage>($"/api/errors/{c.SecondMessageId}");
                    secondFailure = secondFailureResult;

                    return secondFailureResult;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(exceptionTypeAndStackTraceGroups, Is.Not.Null, "Exception Type And Stack Trace Group should be created");
                Assert.That(messageTypeGroups, Is.Not.Null, "Message Type Group should be created");
                Assert.That(firstFailure, Is.Not.Null, "The first failure message should be created");
                Assert.That(secondFailure, Is.Not.Null, "The second failure message should be created");
            });

            Assert.That(exceptionTypeAndStackTraceGroups.Count, Is.EqualTo(1), "There should only be one Exception Type And Stack Trace Group");
            Assert.That(messageTypeGroups.Count, Is.EqualTo(1), "There should only be one Message Type Group");

            var failureGroup = exceptionTypeAndStackTraceGroups.First();
            Assert.Multiple(() =>
            {
                Assert.That(failureGroup.Count, Is.EqualTo(2), "Exception Type And Stack Trace Group should have both messages in it");

                Assert.That(messageTypeGroups.First().Count, Is.EqualTo(2), "Message Type Group should have both messages in it");
            });

            var failureTimes = firstFailure.ProcessingAttempts
                .Union(secondFailure.ProcessingAttempts)
                .Where(x => x.FailureDetails != null)
                .Select(x => x.FailureDetails.TimeOfFailure)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(failureGroup.First, Is.EqualTo(failureTimes.Min()), "Failure Group should start when the earliest failure occurred");
                Assert.That(failureGroup.Last, Is.EqualTo(failureTimes.Max()), "Failure Group should end when the latest failure occurred");
            });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoRetries(); });

            public class MyMessageHandler(MyContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");
                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, settings.EndpointName()).ToString();

                    if (message.IsFirst)
                    {
                        scenarioContext.FirstDone = true;
                        scenarioContext.FirstMessageId = uniqueMessageId;
                    }
                    else
                    {
                        scenarioContext.SecondDone = true;
                        scenarioContext.SecondMessageId = uniqueMessageId;
                    }

                    throw new Exception("Simulated exception");
                }
            }
        }

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