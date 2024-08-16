namespace ServiceControl.AcceptanceTests.Recoverability.MessageFailures
{
    using System;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Persistence;


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

                    body = await this.DownloadData($"/api{failedMessage.BodyUrl}");

                    return true;
                })
                .Run();

            Assert.That(failedMessage.MessageId, Is.EqualTo(context.MessageId));
            Assert.That(failedMessage.Status, Is.EqualTo(MessageStatus.Failed));
            Assert.That(failedMessage.ReceivingEndpoint.Name, Is.EqualTo(context.EndpointNameOfReceivingEndpoint),
                "Receiving endpoint name should be parsed correctly");

            Assert.That(failedMessage.ReceivingEndpoint.HostId, Is.Not.EqualTo(Guid.Empty), "Host id should be set");
            Assert.That(string.IsNullOrEmpty(failedMessage.ReceivingEndpoint.Host), Is.False, "Host display name should be set");

            Assert.That(failedMessage.MessageType, Is.EqualTo(typeof(MyMessage).FullName),
                "AuditMessage type should be set to the FullName of the message type");
            Assert.That(failedMessage.IsSystemMessage, Is.False, "ErrorMessage should not be marked as a system message");

            Assert.NotNull(failedMessage.ConversationId);

            Assert.That(failedMessage.TimeSent, Is.Not.EqualTo(DateTime.MinValue), "Time sent should be correctly set");
            Assert.That(failedMessage.ProcessedAt, Is.Not.EqualTo(DateTime.MinValue), "Processed At should be correctly set");

            Assert.That(failedMessage.ProcessingTime, Is.EqualTo(TimeSpan.Zero), "Processing time should not be calculated");
            Assert.That(failedMessage.CriticalTime, Is.EqualTo(TimeSpan.Zero), "Critical time should be not calculated");
            Assert.That(failedMessage.MessageIntent, Is.EqualTo(MessageIntent.Send), "Message intent should be set");

            var bodyAsString = Encoding.UTF8.GetString(body);

            Assert.That(bodyAsString.Contains(Payload), Is.True, bodyAsString);

            Assert.That(failedMessage.BodySize, Is.EqualTo(body.Length));

            Assert.That(failedMessage.Headers.Any(h => h.Key == Headers.MessageId), Is.True);
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
            public Receiver() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var recoverability = c.Recoverability();
                    recoverability.Immediate(x => x.NumberOfRetries(0));
                    recoverability.Delayed(x => x.NumberOfRetries(0));
                });

            public class MyMessageHandler(MyContext scenarioContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
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