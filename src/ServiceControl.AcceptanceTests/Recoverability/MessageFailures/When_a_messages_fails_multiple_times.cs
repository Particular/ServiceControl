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
        const string AttemptNumberHeaderKey = "testing.failed_attempt_no";

        [Test]
        public async Task Should_report_the_most_recent_attempt_last()
        {
            FailedMessage result = null;

            await Define<TestContext>()
                .WithEndpoint<AnEndpoint>()
                .Done(async c =>
                {
                    if (string.IsNullOrWhiteSpace(c.UniqueMessageId))
                    {
                        return false;
                    }

                    result = await this.TryGet<FailedMessage>(
                        $"/api/errors/{c.UniqueMessageId}",
                        m => LatestAttemptNumber(m) == NumberOfFailedAttempts.ToString());

                    return result != null;
                })
                .Run();

            Assert.That(LatestAttemptNumber(result), Is.EqualTo(NumberOfFailedAttempts.ToString()));
        }

        static string LatestAttemptNumber(FailedMessage message) =>
            message.ProcessingAttempts[^1].Headers.GetValueOrDefault(AttemptNumberHeaderKey);

        class TestContext : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
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
                    var earliestTimeOfFailure = DateTimeOffset.UtcNow;

                    context.UniqueMessageId = DeterministicGuid.MakeId(messageId, endpointName).ToString();

                    var transportOperations = Enumerable.Range(0, NumberOfFailedAttempts)
                        .Select(i =>
                        {
                            var headers = new Dictionary<string, string>
                            {
                                [Headers.MessageId] = messageId,
                                [Headers.EnclosedMessageTypes] = typeof(MyMessage).FullName,
                                ["NServiceBus.FailedQ"] = endpointName,
                                ["$.diagnostics.hostid"] = Guid.NewGuid().ToString(),
                                ["NServiceBus.TimeOfFailure"] = DateTimeOffsetHelper.ToWireFormattedString(earliestTimeOfFailure.Add(TimeSpan.FromMinutes(i))),
                                [AttemptNumberHeaderKey] = (i + 1).ToString()
                            };

                            return new TransportOperation(new OutgoingMessage(messageId, headers, Array.Empty<byte>()), new UnicastAddressTag("error"));
                        })
                        .ToArray();

                    return new TransportOperations(transportOperations);
                }
            }

            class MyMessage : ICommand;
        }
    }
}
