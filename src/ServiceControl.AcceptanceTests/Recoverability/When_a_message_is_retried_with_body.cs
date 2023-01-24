namespace ServiceControl.AcceptanceTests.Recoverability
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Pipeline;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures.Api;
    using TestSupport.EndpointTemplates;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_is_retried_with_body : AcceptanceTest
    {
        [TestCase(false, false)]
        [TestCase(true, false)] // creates body above 85000 bytes to make sure it is ingested into the body storage
        [TestCase(false, true)]
        [TestCase(true, true)] // creates body above 85000 bytes to make sure it is ingested into the body storage
        public async Task Should_work_with_various_body_size(bool largeMessageBodies, bool enableFullTextSearch)
        {
            SetSettings = settings =>
            {
                settings.EnableFullTextSearchOnBodies = enableFullTextSearch;
            };

            string content = $"{{\"Content\":\"{(largeMessageBodies ? new string('a', 86 * 1024) : "Small")}\"}}";
            byte[] buffer = Encoding.UTF8.GetBytes(content);

            var context = await Define<TestContext>(c =>
                {
                    c.BodyToSend = buffer;
                })
                .WithEndpoint<MessageWithBody>()
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

            CollectionAssert.AreEqual(context.BodyToSend, context.BodyReceived);
        }

        class TestContext : ScenarioContext
        {
            public bool RetryIssued { get; set; }
            public bool Done { get; set; }
            public byte[] BodyReceived { get; set; }
            public byte[] BodyToSend { get; set; } = new byte[0];
        }

        class MessageWithBody : EndpointConfigurationBuilder
        {
            public MessageWithBody()
            {
                EndpointSetup<DefaultServer>(
                    (c, r) => c.Pipeline.Register(new CaptureIncomingMessage((TestContext)r.ScenarioContext), "Captures the incoming message"));
            }

            class FakeSender : DispatchRawMessages<TestContext>
            {
                protected override TransportOperations CreateMessage(TestContext context)
                {
                    var messageId = Guid.NewGuid().ToString();
                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        [Headers.EnclosedMessageTypes] = typeof(MyMessage).FullName
                    };

                    headers["NServiceBus.FailedQ"] = Conventions.EndpointNamingConvention(typeof(MessageWithBody));
                    headers["$.diagnostics.hostid"] = Guid.NewGuid().ToString();
                    headers["NServiceBus.TimeOfFailure"] = DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow);

                    var outgoingMessage = new OutgoingMessage(messageId, headers, context.BodyToSend);

                    return new TransportOperations(new TransportOperation(outgoingMessage, new UnicastAddressTag("error")));
                }
            }

            class CaptureIncomingMessage : Behavior<ITransportReceiveContext>
            {
                public CaptureIncomingMessage(TestContext context)
                {
                    testContext = context;
                }

                public override Task Invoke(ITransportReceiveContext context, Func<Task> next)
                {
                    testContext.BodyReceived = context.Message.Body;
                    testContext.Done = true;
                    return Task.CompletedTask;
                }

                TestContext testContext;
            }

            class MyMessage : ICommand
            {
                public string Content { get; set; }
            }
        }
    }
}
