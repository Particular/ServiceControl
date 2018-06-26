namespace ServiceBus.Management.AcceptanceTests.MessageFailures
{
    using System.Net;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.CompositeViews.Messages;

    class Audit_Messages_That_Big_Bodies_Audit_Test : AcceptanceTest
    {
        const int MAX_BODY_SIZE = 20536;

        [Test]
        public async Task Should_not_get_an_empty_audit_message_body_when_configured_MaxBodySizeToStore_is_greater_then_message_size()
        {
            //Arrange
            SetSettings = settings => settings.MaxBodySizeToStore = MAX_BODY_SIZE;

            byte[] body = null;

            //Act
            await Define<Context>()
                .WithEndpoint<ServerEndpoint>(c => c.When(b => b.SendLocal(
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

                            var result = await this.TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                            MessagesView auditMessage = result;
                            if (!result)
                            {
                                return false;
                            }

                            body = await this.DownloadData(auditMessage.BodyUrl);

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

            byte[] body = null;

            //Act
            await Define<Context>()
                .WithEndpoint<ServerEndpoint>(c => c.When(b => b.SendLocal(
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
                        var result = await this.TryGetSingle<MessagesView>("/api/messages", r => r.MessageId == c.MessageId);
                        MessagesView auditMessage = result;
                        if (!result)
                        {
                            return false;
                        }

                        body = await this.DownloadData(auditMessage.BodyUrl, HttpStatusCode.NoContent);

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
                readonly Context testContext;

                public BigFatMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(BigFatMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageHeaders["NServiceBus.MessageId"];
                    return Task.FromResult(0);
                }
            }
        }

        class BigFatMessage : IMessage
        {
            public byte[] BigFatBody { get; set; }
        }

        class Context : ScenarioContext
        {
            public string MessageId { get; set; }
        }

    }
}
