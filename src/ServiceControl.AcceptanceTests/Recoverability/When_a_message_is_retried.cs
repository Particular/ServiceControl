namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_is_retried : AcceptanceTest
    {
        [Test]
        public async Task Should_clean_headers()
        {
            var context = await Define<TestContext>()
                .WithEndpoint<VerifyHeader>()
                .Done(async x =>
                {
                    if (!x.RetryIssued && await this.TryGetMany<FailedMessageView>("/api/errors"))
                    {
                        x.RetryIssued = true;
                        await this.Post<object>("/api/errors/retry/all");
                    }

                    return x.Done;
                })
                .Run();

            CollectionAssert.DoesNotContain(HeadersThatShouldBeRemoved, context.Headers.Keys);
        }

        static readonly List<string> HeadersThatShouldBeRemoved = new List<string>
        {
            "NServiceBus.Retries",
            "NServiceBus.Retries.Timestamp",
            "NServiceBus.FailedQ",
            "ServiceContro.EditOf",
            "NServiceBus.TimeOfFailure",
            "NServiceBus.ExceptionInfo.ExceptionType",
            "NServiceBus.ExceptionInfo.AuditMessage",
            "NServiceBus.ExceptionInfo.Source",
            "NServiceBus.ExceptionInfo.StackTrace",
            "NServiceBus.ExceptionInfo.HelpLink",
            "NServiceBus.ExceptionInfo.Message",
            "NServiceBus.ExceptionInfo.InnerExceptionType",
            "NServiceBus.ExceptionInfo.Data.Custom",
            "NServiceBus.ProcessingMachine",
            "NServiceBus.ProcessingEndpoint",
            "$.diagnostics.hostid",
            "$.diagnostics.hostdisplayname"
        };

        class TestContext : ScenarioContext
        {
            public bool RetryIssued { get; set; }
            public bool Done { get; set; }
            public Dictionary<string, string> Headers { get; set; }
        }

        class VerifyHeader : EndpointConfigurationBuilder
        {
            public VerifyHeader()
            {
                EndpointSetup<DefaultServer>(
                    (c, r) => c.RegisterMessageMutator(new CaptureHeaders((TestContext)r.ScenarioContext))
                );
            }

            class FakeSender : DispatchRawMessages<TestContext>
            {
                protected override TransportOperations CreateMessage(TestContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId
                    };

                    foreach (var headerKey in HeadersThatShouldBeRemoved)
                    {
                        headers[headerKey] = "test";
                    }

                    headers["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(VerifyHeader));
                    headers["$.diagnostics.hostid"] = Guid.NewGuid().ToString();
                    headers["NServiceBus.TimeOfFailure"] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);

                    var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }

            class CaptureHeaders : IMutateIncomingTransportMessages
            {
                public CaptureHeaders(TestContext context)
                {
                    testContext = context;
                }

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    testContext.Headers = context.Headers;
                    testContext.Done = true;
                    return Task.FromResult(0);
                }

                TestContext testContext;
            }
        }
    }
}