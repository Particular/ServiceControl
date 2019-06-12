namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.MessageFailures.Api;

    class When_an_event_with_multiple_subscribers_fails : AcceptanceTest
    {
        [Test]
        public async Task There_should_be_a_FailedMessage_for_each_subscriber()
        {
            var failedMessages = new List<FailedMessageView>();

            await Define<FailingEventContext>()
                .WithEndpoint<SimulateTwoFailedMessagesFromOnePublish>()
                .Done(async ctx =>
                {
                    var result = await this.TryGetMany<FailedMessageView>("/api/errors");
                    failedMessages = result;
                    return result && failedMessages.Sum(x => x.NumberOfProcessingAttempts) >= 2;
                })
                .Run();

            var subscriber1FailedMessage = failedMessages.SingleOrDefault(msg => msg.QueueAddress.Contains("subscriber1"));
            var subscriber2FailedMessage = failedMessages.SingleOrDefault(msg => msg.QueueAddress.Contains("subscriber2"));

            Assert.IsNotNull(subscriber1FailedMessage, "Subscriber1 did not report failed message");
            Assert.IsNotNull(subscriber2FailedMessage, "Subscriber2 did not report failed message");
            Assert.AreNotSame(subscriber1FailedMessage, subscriber2FailedMessage, "There should be two distinct failed messages");
        }

        public class SimulateTwoFailedMessagesFromOnePublish : EndpointConfigurationBuilder
        {
            public SimulateTwoFailedMessagesFromOnePublish()
            {
                EndpointSetup<DefaultServer>();
            }

            class SendDuplicateMessages : DispatchRawMessages<FailingEventContext>
            {
                protected override TransportOperations CreateMessage(FailingEventContext context)
                {
                    var messageId = Guid.NewGuid().ToString();

                    var headers = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        [Headers.ProcessingEndpoint] = "subscriber1",
                        ["NServiceBus.FailedQ"] = "subscriber1"
                    };

                    var headers2 = new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        [Headers.ProcessingEndpoint] = "subscriber2",
                        ["NServiceBus.FailedQ"] = "subscriber2"
                    };

                    var outgoingMessage = new OutgoingMessage(messageId, headers, new byte[0]);
                    var outgoingMessage2 = new OutgoingMessage(messageId, headers2, new byte[0]);

                    return new TransportOperations(
                        new TransportOperation(outgoingMessage, new UnicastAddressTag("error")), 
                        new TransportOperation(outgoingMessage2, new UnicastAddressTag("error"))
                        );
                }
            }
        }

        public class FailingEventContext : ScenarioContext
        {
        }
    }
}