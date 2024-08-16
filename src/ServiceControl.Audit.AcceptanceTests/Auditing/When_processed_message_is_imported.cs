namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using Audit.Auditing.MessagesView;
    using Audit.Monitoring;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Audit.Auditing;

    class When_processed_message_is_imported : AcceptanceTest
    {
        [Test]
        public async Task Should_be_accessible_via_the_rest_api()
        {
            const string Payload = "PAYLOAD";
            MessagesView auditedMessage = null;
            byte[] body = null;

            var context = await Define<MyContext>()
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

            Assert.That(auditedMessage.MessageId, Is.EqualTo(context.MessageId));
            Assert.That(auditedMessage.Status, Is.EqualTo(MessageStatus.Successful));
            Assert.That(auditedMessage.ReceivingEndpoint.Name, Is.EqualTo(context.EndpointNameOfReceivingEndpoint),
                "Receiving endpoint name should be parsed correctly");

            Assert.That(auditedMessage.ReceivingEndpoint.HostId, Is.Not.EqualTo(Guid.Empty), "Host id should be set");
            Assert.That(string.IsNullOrEmpty(auditedMessage.ReceivingEndpoint.Host), Is.False, "Host display name should be set");

            Assert.That(auditedMessage.MessageType, Is.EqualTo(typeof(MyMessage).FullName),
                "AuditMessage type should be set to the FullName of the message type");
            Assert.That(auditedMessage.IsSystemMessage, Is.False, "AuditMessage should not be marked as a system message");

            Assert.NotNull(auditedMessage.ConversationId);

            Assert.That(auditedMessage.TimeSent, Is.Not.EqualTo(DateTime.MinValue), "Time sent should be correctly set");
            Assert.That(auditedMessage.ProcessedAt, Is.Not.EqualTo(DateTime.MinValue), "Processed At should be correctly set");

            Assert.Less(TimeSpan.Zero, auditedMessage.ProcessingTime, "Processing time should be calculated");
            Assert.Less(TimeSpan.Zero, auditedMessage.CriticalTime, "Critical time should be calculated");
            Assert.That(auditedMessage.MessageIntent, Is.EqualTo(MessageIntent.Send), "Message intent should be set");

            var bodyAsString = Encoding.UTF8.GetString(body);

            Assert.That(bodyAsString.Contains(Payload), Is.True, bodyAsString);

            Assert.That(auditedMessage.BodySize, Is.EqualTo(body.Length));

            Assert.That(auditedMessage.Headers.Any(h => h.Key == Headers.MessageId), Is.True);
        }

        [Test]
        public async Task Should_be_counted()
        {
            const string Payload = "PAYLOAD";
            List<AuditCount> counts = null;

            var context = await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage
                {
                    Payload = Payload
                })))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    if (c.MessageId == null || c.EndpointNameOfReceivingEndpoint == null)
                    {
                        return false;
                    }

                    var result = await this.TryGet<List<AuditCount>>($"/api/endpoints/{c.EndpointNameOfReceivingEndpoint}/audit-count", c => c.Count > 0);
                    counts = result.Item;
                    if (!result)
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            Assert.That(counts.Count, Is.EqualTo(1));
            Assert.That(counts[0].UtcDate, Is.EqualTo(DateTime.UtcNow.Date));
            Assert.That(counts[0].Count, Is.EqualTo(1));
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

            public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings) : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
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

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}