namespace ServiceControl.AcceptanceTests.Recoverability
{
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.AcceptanceTesting;
    using System.Collections.Generic;
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using TestSupport.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    class When_a_messages_fails_multiple_times : AcceptanceTest
    {
        const int numberOfFailedAttempts = 20;
        const int maximalNumberOfStoredFailedAttempts = 10;
        const string AttemptIdHeaderKey = "testing.failed_attempt_no";

        [Test]
        public async Task Should_clean_headers()
        {
            FailedMessage result = null;

            var context = await Define<TestContext>()
                .WithEndpoint<AnEndpoint>()
                .Done(async c =>
                {
                    if (string.IsNullOrWhiteSpace(c.UniqueMessageId))
                    {
                        return false;
                    }

                    result = await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueMessageId}");

                    return result != null && result.ProcessingAttempts.Count == maximalNumberOfStoredFailedAttempts && result.ProcessingAttempts.Any(pa =>
                    {
                        try
                        {
                            return pa.Headers[AttemptIdHeaderKey] == numberOfFailedAttempts.ToString();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    });
                })
                .Run();

            Assert.IsTrue(result.ProcessingAttempts.Count == maximalNumberOfStoredFailedAttempts, $"Only last {maximalNumberOfStoredFailedAttempts} processing attempts should be stored for any failing message");

            var failureTimes = result.ProcessingAttempts.Select(pa => pa.Headers["NServiceBus.TimeOfFailure"]).ToArray();

            CollectionAssert.AreEquivalent(failureTimes, context.LatestFailureTimes, "Processing attempts should be stored from latest to oldest");
        }

        class TestContext : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public List<string> LatestFailureTimes { get; set; } = new List<string>();
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class FailedMessagesSender : DispatchRawMessages<TestContext>
            {
                protected override TransportOperations CreateMessage(TestContext context)
                {
                    var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AnEndpoint));
                    var messageId = Guid.NewGuid().ToString();
                    var latestTimeOfFailure = DateTime.UtcNow;

                    context.UniqueMessageId = DeterministicGuid.MakeId(messageId, endpointName).ToString();

                    var transportOperations = Enumerable.Range(0, numberOfFailedAttempts)
                        .Select(i =>
                        {
                            var timeOfFailure = DateTimeExtensions.ToWireFormattedString(latestTimeOfFailure.Subtract(TimeSpan.FromMinutes(i + 1)));

                            var headers = new Dictionary<string, string>
                            {
                                [Headers.MessageId] = messageId,
                                [Headers.EnclosedMessageTypes] = typeof(MyMessage).FullName,
                                ["NServiceBus.FailedQ"] = endpointName,
                                ["$.diagnostics.hostid"] = Guid.NewGuid().ToString(),
                                ["NServiceBus.TimeOfFailure"] = timeOfFailure,

                                [AttemptIdHeaderKey] = (i + 1).ToString()
                            };

                            context.LatestFailureTimes.Add(timeOfFailure);

                            return new TransportOperation(new OutgoingMessage(messageId, headers, Array.Empty<byte>()), new UnicastAddressTag("error"));
                        })
                        .ToArray();

                    context.LatestFailureTimes = context.LatestFailureTimes.Take(maximalNumberOfStoredFailedAttempts).ToList();

                    return new TransportOperations(transportOperations);
                }
            }

            class MyMessage : ICommand
            {
            }
        }
    }
}