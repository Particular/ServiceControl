namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.IO;
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

    class When_a_message_fails_twice_with_different_exceptions : AcceptanceTest
    {
        [Test]
        public async Task Only_the_second_groups_should_apply()
        {
            FailedMessage originalMessage = null;
            FailedMessage retriedMessage = null;

            await Define<MeowContext>()
                .WithEndpoint<MeowReceiver>(b => b.When(bus => bus.SendLocal(new Meow()))
                    .DoNotFailOnErrorMessages())
                .Done(async ctx =>
                {
                    if (string.IsNullOrWhiteSpace(ctx.UniqueMessageId))
                    {
                        return false;
                    }

                    if (!ctx.Retrying)
                    {
                        var result = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}");
                        FailedMessage originalMessageTemp = result;
                        if (result)
                        {
                            originalMessage = originalMessageTemp;
                            ctx.Retrying = true;
                            await this.Post<object>($"/api/errors/{ctx.UniqueMessageId}/retry");
                        }
                    }
                    else
                    {
                        var retriedMessageResult = await this.TryGet<FailedMessage>($"/api/errors/{ctx.UniqueMessageId}", err => err.ProcessingAttempts.Count == 2);
                        retriedMessage = retriedMessageResult;
                        return retriedMessageResult;
                    }

                    return false;
                })
                .Run(TimeSpan.FromMinutes(3));

            Assert.IsNotNull(originalMessage, "Original message was not received");
            Assert.IsNotNull(retriedMessage, "Retried message was not received");

            Assert.IsNotNull(originalMessage.FailureGroups, "The original message has no failure groups");
            Assert.IsNotNull(retriedMessage.FailureGroups, "The retried message has no failure groups");

            var originalExceptionAndStackTraceFailureGroupIds = originalMessage.FailureGroups.Where(x => x.Type == ExceptionTypeAndStackTraceFailureClassifier.Id).Select(x => x.Id).ToArray();
            var retriedExceptionAndStackTraceFailureGroupIds = retriedMessage.FailureGroups.Where(x => x.Type == ExceptionTypeAndStackTraceFailureClassifier.Id).Select(x => x.Id).ToArray();

            Assert.That(originalExceptionAndStackTraceFailureGroupIds.Any(), Is.True, "The original message was not classified");
            Assert.That(retriedExceptionAndStackTraceFailureGroupIds.Any(), Is.True, "The retried message was not classified");

            Assert.AreEqual(originalMessage.FailureGroups.Single(x => x.Type == MessageTypeFailureClassifier.Id).Id, retriedMessage.FailureGroups.Single(x => x.Type == MessageTypeFailureClassifier.Id).Id, $"{MessageTypeFailureClassifier.Id} FailureGroup Ids changed");

            foreach (var failureId in originalExceptionAndStackTraceFailureGroupIds)
            {
                Console.WriteLine($"failureId: {failureId}");
                Assert.That(retriedExceptionAndStackTraceFailureGroupIds.Contains(failureId), Is.False, $"Failure Group {failureId} is still set on retried message");
            }
        }

        public class MeowReceiver : EndpointConfigurationBuilder
        {
            public MeowReceiver() => EndpointSetup<DefaultServerWithoutAudit>(c => { c.NoDelayedRetries(); });

            public class FailingMessageHandler(MeowContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<Meow>
            {
                public Task Handle(Meow message, IMessageHandlerContext context)
                {
                    var messageId = context.MessageId.Replace(@"\", "-");

                    var uniqueMessageId = DeterministicGuid.MakeId(messageId, settings.EndpointName()).ToString();
                    scenarioContext.UniqueMessageId = uniqueMessageId;

                    if (scenarioContext.Retrying)
                    {
                        throw new IOException("The disk is full");
                    }

                    throw new Exception("The website is not responding");
                }
            }
        }

        public class Meow : ICommand;

        public class MeowContext : ScenarioContext
        {
            public bool Retrying { get; set; }
            public string UniqueMessageId { get; set; }
        }
    }
}