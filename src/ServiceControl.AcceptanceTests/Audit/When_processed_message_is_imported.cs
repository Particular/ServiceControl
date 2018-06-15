namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Contexts;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Contracts.Operations;

    public class When_processed_message_is_imported : AcceptanceTest
    {
        [Test]
        public async Task Should_be_accessible_via_the_rest_api()
        {
            const string Payload = "PAYLOAD";
            var context = new MyContext();
            MessagesView auditedMessage = null;
            byte[] body = null;

            await Define(context)
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage
                {
                    Payload = Payload
                })))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    var result = await TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId);
                    auditedMessage = result;
                    if (!result)
                    {
                        return false;
                    }

                    body = await DownloadData(auditedMessage.BodyUrl);

                    return true;

                })
                .Run(TimeSpan.FromSeconds(40));

            Console.WriteLine(JsonConvert.SerializeObject(auditedMessage));

            Assert.AreEqual(context.MessageId, auditedMessage.MessageId);
            Assert.AreEqual(MessageStatus.Successful, auditedMessage.Status);
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, auditedMessage.ReceivingEndpoint.Name,
                "Receiving endpoint name should be parsed correctly");

            Assert.AreNotEqual(Guid.Empty, auditedMessage.ReceivingEndpoint.HostId, "Host id should be set");
            Assert.False(string.IsNullOrEmpty(auditedMessage.ReceivingEndpoint.Host), "Host display name should be set");

            Assert.AreEqual(typeof(MyMessage).FullName, auditedMessage.MessageType,
                "AuditMessage type should be set to the FullName of the message type");
            Assert.False(auditedMessage.IsSystemMessage, "AuditMessage should not be marked as a system message");

            Assert.NotNull(auditedMessage.ConversationId);

            Assert.AreNotEqual(DateTime.MinValue, auditedMessage.TimeSent, "Time sent should be correctly set");
            Assert.AreNotEqual(DateTime.MinValue, auditedMessage.ProcessedAt, "Processed At should be correctly set");

            Assert.Less(TimeSpan.Zero, auditedMessage.ProcessingTime, "Processing time should be calculated");
            Assert.Less(TimeSpan.Zero, auditedMessage.CriticalTime, "Critical time should be calculated");
            Assert.AreEqual(MessageIntentEnum.Send, auditedMessage.MessageIntent, "Message intent should be set");

            var bodyAsString = Encoding.UTF8.GetString(body);

            Assert.True(bodyAsString.Contains(Payload), bodyAsString);

            Assert.AreEqual(body.Length, auditedMessage.BodySize);

            Assert.True(auditedMessage.Headers.Any(_=>_.Key == Headers.MessageId));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.MessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public string Payload { get; set; }
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}