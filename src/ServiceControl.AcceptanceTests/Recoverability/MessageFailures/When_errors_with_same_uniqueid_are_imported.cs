﻿namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
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
        [Test]
        public async Task The_import_should_deduplicate_on_TimeOfFailure()
        {
            var criticalErrorExecuted = false;

            SetSettings = settings => { settings.MaximumConcurrencyLevel = 10; };
            CustomConfiguration = config =>
            {
                config.DefineCriticalErrorAction((_, __) =>
                {
                    criticalErrorExecuted = true;
                    return Task.CompletedTask;
                });
                config.RegisterComponents(services => services.AddSingleton<CounterEnricher>());
            };

            FailedMessage failure = null;
            var context = await Define<MyContext>()
                .WithEndpoint<SourceEndpoint>()
                .Done(async c =>
                {
                    if (c.UniqueId == null)
                    {
                        return false;
                    }

                    var result = await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueId}", m =>
                    {
                        Console.WriteLine("Processing attempts: " + m.ProcessingAttempts.Count);
                        return m.ProcessingAttempts.Count == 2;
                    });
                    failure = result;
                    return criticalErrorExecuted || result;
                })
                .Run();

            Assert.Multiple(() =>
            {
                Assert.That(criticalErrorExecuted, Is.False);
                Assert.That(failure, Is.Not.Null);
            });

            var attempts = failure.ProcessingAttempts;
            Assert.Multiple(() =>
            {
                Assert.That(attempts, Has.Count.EqualTo(2));
                Assert.That(attempts.Select(a => a.AttemptedAt), Is.EquivalentTo(context.FailureTimes));
            });
        }

        class CounterEnricher(MyContext testContext) : IEnrichImportedErrorMessages
        {
            public void Enrich(ErrorEnricherContext context)
            {
                if (context.Headers.TryGetValue("Counter", out var counter))
                {
                    testContext.OnMessage(counter);
                }
                else
                {
                    Console.WriteLine("No Counter header found");
                }
            }
        }

        public class SourceEndpoint : EndpointConfigurationBuilder
        {
            public SourceEndpoint() => EndpointSetup<DefaultServerWithoutAudit>();

            class SendMultipleFailedMessagesWithSameUniqueId : DispatchRawMessages<MyContext>
            {
                protected override TransportOperations CreateMessage(MyContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    context.UniqueId = DeterministicGuid.MakeId(messageId, "Error.SourceEndpoint").ToString();
                    context.FailureTimes = new[]
                    {
                        new DateTime(2020, 09, 05, 13, 20, 00, 0, DateTimeKind.Utc),
                        new DateTime(2020, 09, 05, 12, 20, 00, 0, DateTimeKind.Utc),
                    };

                    return new TransportOperations(GetMessages(context.UniqueId, context.FailureTimes).ToArray());
                }

                IEnumerable<TransportOperation> GetMessages(string uniqueId, DateTime[] failureTimes)
                {
                    for (var failureNo = 0; failureNo < failureTimes.Length; failureNo++)
                    {
                        for (var i = 0; i < 5; i++)
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
                                ["NServiceBus.TimeOfFailure"] = DateTimeOffsetHelper.ToWireFormattedString(failureTimes[failureNo]),
                                ["Counter"] = i.ToString()
                            };

                            var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);

                            yield return new TransportOperation(outgoingMessage, new UnicastAddressTag("error"));
                        }
                    }
                }
            }
        }

        class MyContext : ScenarioContext
        {
            public string UniqueId { get; set; }

            public DateTime[] FailureTimes { get; set; }

            public void OnMessage(string counter) => receivedMessages.AddOrUpdate(counter, true, (id, old) => true);

            readonly ConcurrentDictionary<string, bool> receivedMessages = new();
        }
    }
}