namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using Operations;
    using ServiceControl.MessageFailures;

    class When_errors_with_same_uniqueid_are_imported : AcceptanceTest
    {
        const int NumberOfDuplicates = 10;

        [Test]
        public async Task The_import_should_deduplicate_on_TimeOfFailure()
        {
            var criticalErrorExecuted = false;

            SetSettings = settings => settings.MaximumConcurrencyLevel = NumberOfDuplicates;
            CustomizeHostBuilder = builder => builder.Services.AddSingleton<IEnrichImportedErrorMessages, CounterEnricher>();
            CustomConfiguration = config => config.DefineCriticalErrorAction((_, _) =>
                {
                    criticalErrorExecuted = true;
                    return Task.CompletedTask;
                });

            FailedMessage failure = null;
            var context = await Define<MyContext>()
                .WithEndpoint<SourceEndpoint>()
                .Done(async c =>
                {
                    if (c.UniqueId == null || c.IngestedCount < NumberOfDuplicates)
                    {
                        return false;
                    }

                    var result = await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueId}");
                    failure = result;
                    return criticalErrorExecuted || result;
                })
                .Run();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(criticalErrorExecuted, Is.False);
                Assert.That(failure, Is.Not.Null);
            }

            var attempts = failure.ProcessingAttempts;
            using (Assert.EnterMultipleScope())
            {
                Assert.That(attempts, Has.Count.EqualTo(1));
                Assert.That(attempts[^1].AttemptedAt, Is.EqualTo(context.FailureTime.UtcDateTime));
            }
        }

        class CounterEnricher(MyContext testContext) : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context)
            {
                if (context.Headers.TryGetValue("Counter", out var counter))
                {
                    testContext.OnMessage(counter);
                }
            }
        }

        public class SourceEndpoint : EndpointConfigurationBuilder
        {
            public SourceEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<SendMultipleFailedMessagesWithSameUniqueId>());

            class SendMultipleFailedMessagesWithSameUniqueId : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    context.UniqueId = DeterministicGuid.MakeId(messageId, "Error.SourceEndpoint").ToString();
                    context.FailureTime = new DateTimeOffset(2020, 09, 05, 13, 20, 00, 0, TimeSpan.Zero);

                    return new TransportOperations([.. GetMessages(context.UniqueId, context.FailureTime)]);
                }

                IEnumerable<TransportOperation> GetMessages(string uniqueId, DateTimeOffset failureTime)
                {
                    for (var i = 0; i < NumberOfDuplicates; i++)
                    {
                        var messageId = Guid.NewGuid().ToString();
                        var headers = new Dictionary<string, string>
                        {
                            [Headers.MessageId] = messageId,
                            ["ServiceControl.Retry.UniqueMessageId"] = uniqueId,
                            [Headers.ProcessingEndpoint] = "Error.SourceEndpoint",
                            ["NServiceBus.ExceptionInfo.ExceptionType"] = typeof(Exception).FullName,
                            ["NServiceBus.ExceptionInfo.Message"] = "Bad thing happened",
                            ["NServiceBus.ExceptionInfo.InnerExceptionType"] = "System.Exception",
                            ["NServiceBus.ExceptionInfo.Source"] = "NServiceBus.Core",
                            ["NServiceBus.ExceptionInfo.StackTrace"] = string.Empty,
                            ["NServiceBus.FailedQ"] = "Error.SourceEndpoint",
                            ["NServiceBus.TimeOfFailure"] = DateTimeOffsetHelper.ToWireFormattedString(failureTime),
                            ["Counter"] = i.ToString()
                        };

                        var outgoingMessage = new OutgoingMessage(messageId, headers, Array.Empty<byte>());

                        yield return new TransportOperation(outgoingMessage, new UnicastAddressTag("error"));
                    }
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string UniqueId { get; set; }

            public DateTimeOffset FailureTime { get; set; }

            public int IngestedCount => receivedMessages.Count;

            public void OnMessage(string counter) => receivedMessages.AddOrUpdate(counter, true, (id, old) => true);

            readonly ConcurrentDictionary<string, bool> receivedMessages = new();
        }
    }
}
