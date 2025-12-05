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
    using AcceptanceTesting.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    class When_a_messages_fails_multiple_times : AcceptanceTest
    {
        const int NumberOfFailedAttempts = 20;
        const int MaximalNumberOfStoredFailedAttempts = 10;
        const string AttemptIdHeaderKey = "testing.failed_attempt_no";

        [Test]
        public async Task Should_store_only_the_latest_processing_attempts()
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

                    var failureTimes = result?.ProcessingAttempts.Select(pa => pa.Headers["NServiceBus.TimeOfFailure"]).ToArray() ?? [];

                    return failureTimes.SequenceEqual([.. c.LatestFailureTimes]);
                })
                .Run();
        }

        class TestContext : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public List<string> LatestFailureTimes { get; set; } = [];
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint() => EndpointSetup<DefaultServerWithoutAudit>(c => c.EnableFeature<FailedMessagesSender>());

            class FailedMessagesSender : DispatchRawMessages<TestContext>
            {
                protected override TransportOperations CreateMessage(TestContext context)
                {
                    var endpointName = NServiceBus.AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(AnEndpoint));
                    var messageId = Guid.NewGuid().ToString();
                    var earliestTimeOfFailure = DateTime.UtcNow;

                    context.UniqueMessageId = DeterministicGuid.MakeId(messageId, endpointName).ToString();

                    var transportOperations = Enumerable.Range(0, NumberOfFailedAttempts)
                        .Select(i =>
                        {
                            var timeOfFailure = DateTimeOffsetHelper.ToWireFormattedString(earliestTimeOfFailure.Add(TimeSpan.FromMinutes(i)));

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

                    context.LatestFailureTimes = context.LatestFailureTimes
                        .Skip(context.LatestFailureTimes.Count - MaximalNumberOfStoredFailedAttempts)
                        .Take(MaximalNumberOfStoredFailedAttempts)
                        .ToList();

                    return new TransportOperations(transportOperations);
                }
            }

            class MyMessage : ICommand;
        }
    }
}