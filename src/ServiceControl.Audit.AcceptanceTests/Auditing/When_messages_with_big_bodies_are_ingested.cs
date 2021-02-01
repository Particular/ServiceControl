namespace ServiceControl.Audit.AcceptanceTests.Auditing
{
    using System.Net;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using Audit.Auditing.MessagesView;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using TestSupport.EndpointTemplates;

    class When_messages_with_big_bodies_are_ingested : AcceptanceTest
    {
        [Test]
        public async Task Should_not_get_an_empty_audit_message_body_when_configured_MaxBodySizeToStore_is_greater_then_message_size()
        {
            //Arrange
            SetSettings = settings => settings.MaxBodySizeToStore = MAX_BODY_SIZE;

            byte[] body = null;

            //Act
            await Define<Context>()
                .WithEndpoint<FatMessageEndpoint>(c => c.When(b => b.SendLocal(
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
                .WithEndpoint<FatMessageEndpoint>(c => c.When(b => b.SendLocal(
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

        [Test]
        public async Task Should_not_get_an_empty_audit_message_body_when_body_is_above_loh_but_below_max_body_size()
        {
            //Arrange
            SetSettings = settings => settings.MaxBodySizeToStore = 2 * MAX_BODY_SIZE_BIGGER_THAN_LOH;

            byte[] body = null;

            //Act
            await Define<Context>()
                .WithEndpoint<FatMessageEndpoint>(c => c.When(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[MAX_BODY_SIZE_BIGGER_THAN_LOH]
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

        const int MAX_BODY_SIZE = 20536;
        const int MAX_BODY_SIZE_BIGGER_THAN_LOH = 87000;

        class FatMessageEndpoint : EndpointConfigurationBuilder
        {
            public FatMessageEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>();
            }

            public class BigFatMessageHandler : IHandleMessages<BigFatMessage>
            {
                public BigFatMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(BigFatMessage message, IMessageHandlerContext context)
                {
                    testContext.MessageId = context.MessageHeaders["NServiceBus.MessageId"];
                    return Task.FromResult(0);
                }

                readonly Context testContext;
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