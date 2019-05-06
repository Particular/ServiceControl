namespace ServiceBus.Management.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EndpointTemplates;
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
            var context = await Define<Context>()
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

            Assert.False(context.Headers.ContainsKey(Headers.MessageIntent), "Should not add the intent header");
            Assert.False(context.Headers.ContainsKey(Headers.NonDurableMessage), "Should not add the non-durable header");
        }

        class OriginalMessage : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool RetryIssued { get; set; }
            public bool Done { get; set; }
            public Dictionary<string, string> Headers { get; set; }
        }

        class VerifyHeader : EndpointConfigurationBuilder
        {
            public VerifyHeader()
            {
                EndpointSetup<DefaultServerWithoutAudit>(
                    (c, r) => c.RegisterMessageMutator(new VerifyHeaderIsUnchanged((Context)r.ScenarioContext))
                );
            }

            class FakeSender : DispatchRawMessages<Context>
            {
                protected override TransportOperations CreateMessage(Context context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        ["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(VerifyHeader)),
                    };

                    var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }

            class VerifyHeaderIsUnchanged : IMutateIncomingTransportMessages
            {
                public VerifyHeaderIsUnchanged(Context context)
                {
                    testContext = context;
                }

                public Task MutateIncoming(MutateIncomingTransportMessageContext context)
                {
                    testContext.Headers = context.Headers;

                    testContext.Done = true;
                    return Task.FromResult(0);
                }

                Context testContext;
            }
        }
    }
}