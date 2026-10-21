namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Transport;
    using NUnit.Framework;

    class When_a_message_processed_with_receive_properties : AcceptanceTest
    {
        [Test]
        public async Task Should_save_audit_message_with_receive_properties()
        {
            const string Payload = "PAYLOAD";
            MessagesView auditedMessage = null;
            byte[] body = null;

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage { Payload = Payload })))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    var result = await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId);
                    auditedMessage = result;
                    if (!result)
                    {
                        return false;
                    }

                    body = await this.DownloadData($"/api{auditedMessage.BodyUrl}");

                    return true;
                })
                .Run();

            var hasReceivedProperties = auditedMessage.Headers.Any(x => x.Key.Equals("ServiceControl.ReceiveProperties.LearningTransport.FileCreatedAt"));

            var receivedProperties = new KeyValuePair<string, string>();
            if (hasReceivedProperties)
            {
                receivedProperties = auditedMessage.Headers.First(x => x.Key.Equals("ServiceControl.ReceiveProperties.LearningTransport.FileCreatedAt"));
            }

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hasReceivedProperties, Is.True, "The audit message should have receive properties");
                Assert.That(receivedProperties.Value, Is.EqualTo(context.ReceivedMessageFileCreatedAt), "The audit message should have receive properties");
            }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>();

            [Handler]
            public class MyMessageHandler(MyContext testContext) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.ReceivedMessageFileCreatedAt = context.Extensions.Get<IncomingMessage>().ReceiveProperties["LearningTransport.FileCreatedAt"];
                    testContext.MessageId = context.MessageId;
                    return Task.Delay(500, context.CancellationToken);
                }
            }
        }

        public class MyMessage : ICommand
        {
            public string Payload { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string ReceivedMessageFileCreatedAt { get; set; }
        }
    }
}