namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.MessageMutator;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;


    class When_a_native_integration_message_is_retried : AcceptanceTest
    {
        [Test]
        public async Task Should_not_corrupt_headers()
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

            Assert.Multiple(() =>
            {
                Assert.That(context.Headers.ContainsKey(Headers.MessageIntent), Is.False, "Should not add the intent header");
                Assert.That(context.Headers.ContainsKey("NServiceBus.NonDurableMessage"), Is.False, "Should not add the non-durable header");
            });
        }

        class TestContext : ScenarioContext
        {
            public bool RetryIssued { get; set; }
            public bool Done { get; set; }
            public Dictionary<string, string> Headers { get; set; }
        }

        class VerifyHeader : EndpointConfigurationBuilder
        {
            public VerifyHeader() =>
                EndpointSetup<DefaultServerWithoutAudit>(
                    (c, r) =>
                    {
                        c.EnableFeature<FakeSender>();
                        c.RegisterMessageMutator(new VerifyHeaderIsUnchanged((TestContext)r.ScenarioContext));
                    });

            class FakeSender : DispatchRawMessages<TestContext>
            {
                protected override TransportOperations CreateMessage(TestContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(VerifyHeader))
                    };

                    var outgoingMessage = new OutgoingMessage(messageId, headers, Array.Empty<byte>());

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }

            class VerifyHeaderIsUnchanged(TestContext testContext) : IMutateIncomingTransportMessages
            {
                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    testContext.Headers = context.Headers;

                    testContext.Done = true;
                    return Task.CompletedTask;
                }
            }
        }
    }
}