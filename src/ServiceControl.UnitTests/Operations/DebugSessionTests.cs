namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Operations;


    [TestFixture]
    public class ImportErrorHandlerTests
    {
        [Test]
        public void Integration()
        {
            var logPath = Path.Combine(Path.GetTempPath(), "ImportErrorHandlerTests");
            Settings.LogPath = logPath;
            if (Directory.Exists(logPath))
            {
                Directory.Delete(logPath, true);
            }
            using (var ravenStore = InMemoryStoreBuilder.GetInMemoryStore())
            {
                ravenStore.Initialize();

                using (var ravenSession = ravenStore.OpenSession())
                {
                    var importErrorHandler = new ImportErrorHandler
                        {
                            Session = ravenSession
                        };
                    var messageId = Guid.NewGuid().ToString();
                    var transportMessage = new TransportMessage(messageId, new Dictionary<string, string>());
                    importErrorHandler.HandleAudit(transportMessage, BuildException());

                    Assert.AreEqual(ravenSession.Load<FailedAuditImport>(messageId).Message.Id, messageId);
                    Assert.IsTrue(File.Exists(Path.Combine(logPath, importErrorHandler.AuditLogDirectory, messageId+ ".txt")));
                }
            }
        }

        public Exception BuildException()
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