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
        const int MAX_BODY_SIZE = 30536;

        [Test]
        public void Should_not_get_an_empty_audit_message_body_when_configured_MaxBodySizeToStore_is_greater_then_message_size()
        {
            //Arrange
            AppConfigurationSettings.Add("ServiceControl/MaxBodySizeToStore", MAX_BODY_SIZE.ToString());
          

            var context = new Context();
            byte[] body = null;

            //Act
            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig)) // I want ServiceControl running
                .WithEndpoint<ServerEndpoint>(c => c.Given(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[MAX_BODY_SIZE - 1000]
                    }))
                )
                .Done(
                    c =>
                        {
                            MessagesView auditMessage;

                            if (c.MessageId == null) return false;
                            if (!TryGetSingle("/api/messages", out auditMessage, r => r.MessageId == c.MessageId)) return false;

                            try
                            {
                                body = DownloadData(auditMessage.BodyUrl);
                            }
                            catch (WebException wex)
                            {
                                if (((HttpWebResponse) wex.Response).StatusCode != HttpStatusCode.NotFound)
                                {
                                    throw;
                                }
                            }

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
            AppConfigurationSettings.Add("ServiceControl/MaxBodySizeToStore", MAX_BODY_SIZE.ToString());


            var context = new Context();
            byte[] body = null;

            //Act
            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig)) // I want ServiceControl running
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
                        if (!TryGetSingle("/api/messages", out auditMessage, r => r.MessageId == c.MessageId)) return false;

                        try
                        {
                            body = DownloadData(auditMessage.BodyUrl);
                        }
                        catch (WebException wex)
                        {
                            if (((HttpWebResponse)wex.Response).StatusCode != HttpStatusCode.NotFound)
                            {
                                throw;
                            }
                        }

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
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class BigFatMessageHandler : IHandleMessages<BigFatMessage>
            {
                readonly Context _context;

                public BigFatMessageHandler(Context context)
                {
                    _context = context;
                }

                public void Handle(BigFatMessage message)
                {
                    _context.MessageId = message.GetHeader("NServiceBus.MessageId");
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
