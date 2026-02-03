namespace ServiceControl.AcceptanceTests.Recoverability.Groups
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
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
        [CancelAfter(180_000)]
        public async Task Only_the_second_groups_should_apply(CancellationToken cancellationToken)
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
                .Run(cancellationToken);

            Assert.Multiple(() =>
            {
                Assert.That(originalMessage, Is.Not.Null, "Original message was not received");
                Assert.That(retriedMessage, Is.Not.Null, "Retried message was not received");
            });

            Assert.That(originalMessage.FailureGroups, Is.Not.Null, "The original message has no failure groups");
            Assert.That(retriedMessage.FailureGroups, Is.Not.Null, "The retried message has no failure groups");

            var originalExceptionAndStackTraceFailureGroupIds = originalMessage.FailureGroups.Where(x => x.Type == ExceptionTypeAndStackTraceFailureClassifier.Id).Select(x => x.Id).ToArray();
            var retriedExceptionAndStackTraceFailureGroupIds = retriedMessage.FailureGroups.Where(x => x.Type == ExceptionTypeAndStackTraceFailureClassifier.Id).Select(x => x.Id).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(originalExceptionAndStackTraceFailureGroupIds.Any(), Is.True, "The original message was not classified");
                Assert.That(retriedExceptionAndStackTraceFailureGroupIds.Any(), Is.True, "The retried message was not classified");

                Assert.That(retriedMessage.FailureGroups.Single(x => x.Type == MessageTypeFailureClassifier.Id).Id, Is.EqualTo(originalMessage.FailureGroups.Single(x => x.Type == MessageTypeFailureClassifier.Id).Id), $"{MessageTypeFailureClassifier.Id} FailureGroup Ids changed");
            });

            foreach (var failureId in originalExceptionAndStackTraceFailureGroupIds)
            {
                Console.WriteLine($"failureId: {failureId}");
                Assert.That(retriedExceptionAndStackTraceFailureGroupIds, Does.Not.Contain(failureId), $"Failure Group {failureId} is still set on retried message");
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