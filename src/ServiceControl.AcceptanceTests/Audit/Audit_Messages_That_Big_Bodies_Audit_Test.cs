namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System.Net;
    using System.Threading.Tasks;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;

    class Audit_Messages_That_Big_Bodies_Audit_Test : AcceptanceTest
    {
        const int MAX_BODY_SIZE = 20536;

        [Test]
        public async Task Should_not_get_an_empty_audit_message_body_when_configured_MaxBodySizeToStore_is_greater_then_message_size()
        {
            //Arrange
            SetSettings = settings => settings.MaxBodySizeToStore = MAX_BODY_SIZE;

            var context = new Context();
            byte[] body = null;

            //Act
            await Define(context)
                .WithEndpoint<ServerEndpoint>(c => c.Given(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[MAX_BODY_SIZE - 10000]
                    }))
                )
                .Done(
                    async c =>
                        {
                            if (c.MessageId == null)
                            {
                                return false;
                            }

                            var result = await TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                            MessagesView auditMessage = result;
                            if (!result)
                            {
                                return false;
                            }

                            body = await DownloadData(auditMessage.BodyUrl);

                            return true;
                        })
                .Run();

            //Assert
            Assert.IsNotNull(body);
        }

        [Test]
        public async Task Should_get_an_empty_audit_message_body_when_configured_MaxBodySizeToStore_is_less_then_message_size()
        {
            //Arrange
            SetSettings = settings => settings.MaxBodySizeToStore = MAX_BODY_SIZE;

            var context = new Context();
            byte[] body = null;

            //Act
            await Define(context)
                .WithEndpoint<ServerEndpoint>(c => c.Given(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[MAX_BODY_SIZE + 1000]
                    }))
                )
                .Done(
                    async c =>
                    {
                        if (c.MessageId == null)
                        {
                            return false;
                        }
                        var result = await TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                        MessagesView auditMessage = result;
                        if (!result)
                        {
                            return false;
                        }

                        body = await DownloadData(auditMessage.BodyUrl, HttpStatusCode.NoContent);

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
