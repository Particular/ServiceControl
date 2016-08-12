namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System.Net;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    class Audit_Messages_That_Big_Bodies_Audit_Test : AcceptanceTest
    {
        const int MAX_BODY_SIZE = 20536;

        [Test]
        public void Should_not_get_an_empty_audit_message_body_when_configured_MaxBodySizeToStore_is_greater_then_message_size()
        {
            //Arrange
            SetSettings = settings => settings.MaxBodySizeToStore = MAX_BODY_SIZE;

            var context = new Context();
            byte[] body = null;

            //Act
            Define(context)
                .WithEndpoint<ServerEndpoint>(c => c.Given(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[MAX_BODY_SIZE - 10000]
                    }))
                )
                .Done(
                    c =>
                        {
                            MessagesView auditMessage;

                            if (c.MessageId == null)
                            {
                                return false;
                            }

                            if (!TryGetSingle("/api/messages", out auditMessage, r => r.MessageId == c.MessageId))
                            {
                                return false;
                            }

                            body = DownloadData(auditMessage.BodyUrl);

                            return true;
                        })
                .Run();

            //Assert
            Assert.IsNotNull(body);
        }

        [Test]
        public void Should_get_an_empty_audit_message_body_when_configured_MaxBodySizeToStore_is_less_then_message_size()
        {
            //Arrange
            SetSettings = settings => settings.MaxBodySizeToStore = MAX_BODY_SIZE;

            var context = new Context();
            byte[] body = null;

            //Act
            Define(context)
                .WithEndpoint<ServerEndpoint>(c => c.Given(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[MAX_BODY_SIZE + 1000]
                    }))
                )
                .Done(
                    c =>
                    {
                        MessagesView auditMessage;

                        if (c.MessageId == null) return false;
                        if (!TryGetSingle("/api/messages", out auditMessage, r => r.MessageId == c.MessageId))
                        {
                            return false;
                        }

                        body = DownloadData(auditMessage.BodyUrl, HttpStatusCode.NoContent);

                        return true;
                    })
                .Run();

            //Assert
            Assert.AreEqual(0, body.Length);
        }

        class ServerEndpoint : EndpointConfigurationBuilder
        {
            public ServerEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class BigFatMessageHandler : IHandleMessages<BigFatMessage>
            {
                readonly Context _context;
                readonly IBus bus;

                public BigFatMessageHandler(Context context, IBus bus)
                {
                    _context = context;
                    this.bus = bus;
                }

                public void Handle(BigFatMessage message)
                {
                    _context.MessageId = bus.GetMessageHeader(message, "NServiceBus.MessageId");
                }
            }
        }

        class BigFatMessage : IMessage
        {
            public string MessageId { get; set; }
            public byte[] BigFatBody { get; set; }
        }

        class Context : ScenarioContext
        {
            public string MessageId { get; set; }
            public bool HasBodyBeenTamperedWith { get; set; }
        }

    }
}
