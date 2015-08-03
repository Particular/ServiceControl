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

        [Test]
        public void Should_not_get_an_empty_audit_message_body_when_configured_for_200K()
        {
            //Arrange
            AppConfigurationSettings.Clear();
            AppConfigurationSettings.Add("ServiceControl/MaxBodySizeToStore", "204800");
          

            var context = new Context();
            byte[] body = null;

            //Act
            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig)) // I want ServiceControl running
                .WithEndpoint<ServerEndpoint>(c => c.Given(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[(1024 * 100) + 1] //100K + 1 byte
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
        public void Should_get_an_empty_audit_message_body_when_configured_for_100K()
        {
            //Arrange
            AppConfigurationSettings.Clear();
            AppConfigurationSettings.Add("ServiceControl/MaxBodySizeToStore", "102400");


            var context = new Context();
            byte[] body = null;

            //Act
            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig)) // I want ServiceControl running
                .WithEndpoint<ServerEndpoint>(c => c.Given(b => b.SendLocal(
                    new BigFatMessage // An endpoint that is configured for audit
                    {
                        BigFatBody = new byte[(1024 * 100) + 1] //100K + 1 byte
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
            Assert.AreEqual(body.Length, 0);

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
