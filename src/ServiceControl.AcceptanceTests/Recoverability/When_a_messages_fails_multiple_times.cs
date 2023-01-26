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
        const string AttemptIdHeaderKey = "testing.failed_attempt_no";

        [Test]
        public async Task Should_clean_headers()
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

                    result = await this.TryGet<FailedMessage>($"/api/errors/{c.UniqueMessageId}");

                    return result != null && result.ProcessingAttempts.Count == 20 && result.ProcessingAttempts.Any(pa =>
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

        }

        class TestContext : ScenarioContext
        {
            public string UniqueMessageId { get; set; }
            public DateTime LatestTimeOfFailure { get; set; }
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
                    context.LatestTimeOfFailure = latestTimeOfFailure;

                    var transportOperations = Enumerable.Range(0, numberOfFailedAttempts)
                        .Select(i =>
                        {
                            var headers = new Dictionary<string, string>
                            {
                                [Headers.MessageId] = messageId,
                                [Headers.EnclosedMessageTypes] = typeof(MyMessage).FullName,
                                ["NServiceBus.FailedQ"] = endpointName,
                                ["$.diagnostics.hostid"] = Guid.NewGuid().ToString(),
                                ["NServiceBus.TimeOfFailure"] = DateTimeExtensions.ToWireFormattedString(latestTimeOfFailure.Subtract(TimeSpan.FromMinutes(i + 1))),

                                [AttemptIdHeaderKey] = (i + 1).ToString()
                            };

                            return new TransportOperation(new OutgoingMessage(messageId, headers, Array.Empty<byte>()), new UnicastAddressTag("error"));
                        })
                        .ToArray();

                    return new TransportOperations(transportOperations);
                }
            }

            class MyMessage : ICommand
            {
            }
        }
    }
}