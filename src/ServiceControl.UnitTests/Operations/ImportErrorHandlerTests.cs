namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Mail;
    using netDumbster.smtp;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;

    [TestFixture]
    public class ImportErrorHandlerTests
    {
        [Test,Ignore]
        public void Integration()
        {
            var logPath = Path.Combine(Path.GetTempPath(), "ImportErrorHandlerTests");
            Settings.LogPath = logPath;
            Settings.Email = "a@b.com";
            if (Directory.Exists(logPath))
            {
                Directory.Delete(logPath, true);
            }
            SimpleSmtpServer server = null;
            try
            {
                server = SimpleSmtpServer.Start();
                using (var client = new SmtpClient("localhost", server.Port))
                using (var ravenStore = InMemoryStoreBuilder.GetInMemoryStore())
                using (var ravenSession = ravenStore.OpenSession())
                {
                    var importErrorHandler = new ImportErrorHandler
                        {
                            Session = ravenSession,
                            GetSmtpClient = () => client
                        };
                    var messageId = Guid.NewGuid().ToString();
                    var message = new TransportMessage(messageId, new Dictionary<string, string>());
                    importErrorHandler.HandleAudit(message, BuildException());

                    Assert.AreEqual(ravenSession.Load<FailedAuditImport>(messageId).Message.Id, messageId);
                    Assert.IsTrue(File.Exists(Path.Combine(logPath, importErrorHandler.AuditLogDirectory, messageId + ".txt")));
                    Assert.IsTrue(server.ReceivedEmail.First().Body().StartsWith("A message import has failed. A log file has been written to "));
                }
            }
            catch (Exception)
            {
                if (server != null)
                {
                    server.Stop();
                }
                throw;
            }
        }

        Exception BuildException()
        {
            try
            {
                throw new Exception("message");
            }
            catch (Exception exception)
            {
                return exception;
            }
        }
    }
}