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
    using ServiceControlInstaller.Engine.Instances;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    [RunOnAllTransports]
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

            // ASQ adds this value to it's message wrapper and writes the incoming message header
            if (TransportIntegration.Name != TransportNames.AzureStorageQueue)
            {
                Assert.False(context.Headers.ContainsKey(Headers.MessageIntent), "Should not add the intent header");
            }

            //Rabbit defaults the header when deserializing the message based on the IBasicProperties.DeliveryMode
            if (TransportIntegration.Name == TransportNames.RabbitMQClassicConventionalRoutingTopology ||
                TransportIntegration.Name == TransportNames.RabbitMQQuorumConventionalRoutingTopology ||
                TransportIntegration.Name == TransportNames.RabbitMQDirectRoutingTopology)
            {
                Assert.AreEqual("False", context.Headers[Headers.NonDurableMessage], "Should not corrupt the non-durable header");
            }
            else
            {
                Assert.False(context.Headers.ContainsKey(Headers.NonDurableMessage), "Should not add the non-durable header");
            }
        }

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
                    (c, r) => c.RegisterMessageMutator(new VerifyHeaderIsUnchanged((TestContext)r.ScenarioContext))
                );
            }

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

                    var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }

            class VerifyHeaderIsUnchanged : IMutateIncomingTransportMessages
            {
                public VerifyHeaderIsUnchanged(TestContext context)
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