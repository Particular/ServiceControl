namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using CompositeViews.Messages;
    using Contracts.Operations;
    using Newtonsoft.Json;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    [RunOnAllTransports]
    class When_failed_message_is_imported : AcceptanceTest
    {
        [Test]
        public async Task Should_be_accessible_via_the_rest_api()
        {
            const string Payload = "PAYLOAD";
            MessagesView failedMessage = null;
            byte[] body = null;

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage
                {
                    Payload = Payload
                })))
                .WithEndpoint<Receiver>(b => b.DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (c.MessageId == null)
                    {
                        return false;
                    }

                    var result = await this.TryGetSingle<MessagesView>("/api/messages?include_system_messages=false&sort=id", m => m.MessageId == c.MessageId);
                    failedMessage = result;
                    if (!result)
                    {
                        return false;
                    }

                    body = await this.DownloadData(failedMessage.BodyUrl);

                    return true;
                })
                .Run();

            Console.WriteLine(JsonConvert.SerializeObject(failedMessage));

            Assert.AreEqual(context.MessageId, failedMessage.MessageId);
            Assert.AreEqual(MessageStatus.Failed, failedMessage.Status);
            Assert.AreEqual(context.EndpointNameOfReceivingEndpoint, failedMessage.ReceivingEndpoint.Name,
                "Receiving endpoint name should be parsed correctly");

            Assert.AreNotEqual(Guid.Empty, failedMessage.ReceivingEndpoint.HostId, "Host id should be set");
            Assert.False(string.IsNullOrEmpty(failedMessage.ReceivingEndpoint.Host), "Host display name should be set");

            Assert.AreEqual(typeof(MyMessage).FullName, failedMessage.MessageType,
                "AuditMessage type should be set to the FullName of the message type");
            Assert.False(failedMessage.IsSystemMessage, "ErrorMessage should not be marked as a system message");

            Assert.NotNull(failedMessage.ConversationId);

            Assert.AreNotEqual(DateTime.MinValue, failedMessage.TimeSent, "Time sent should be correctly set");
            Assert.AreNotEqual(DateTime.MinValue, failedMessage.ProcessedAt, "Processed At should be correctly set");

            Assert.AreEqual(TimeSpan.Zero, failedMessage.ProcessingTime, "Processing time should not be calculated");
            Assert.AreEqual(TimeSpan.Zero, failedMessage.CriticalTime, "Critical time should be not calculated");
            Assert.AreEqual(MessageIntentEnum.Send, failedMessage.MessageIntent, "Message intent should be set");

            var bodyAsString = Encoding.UTF8.GetString(body);

            Assert.True(bodyAsString.Contains(Payload), bodyAsString);

            Assert.AreEqual(body.Length, failedMessage.BodySize);

            Assert.True(failedMessage.Headers.Any(h => h.Key == Headers.MessageId));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
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
                EndpointSetup<DefaultServer>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(x => x.NumberOfRetries(0));
                    recoverability.Delayed(x => x.NumberOfRetries(0));
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext scenarioContext;
                readonly ReadOnlySettings settings;

                public MyMessageHandler(MyContext scenarioContext, ReadOnlySettings settings)
                {
                    this.scenarioContext = scenarioContext;
                    this.settings = settings;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    scenarioContext.MessageId = context.MessageId;
                    throw new Exception("Simulated exception");
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

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}