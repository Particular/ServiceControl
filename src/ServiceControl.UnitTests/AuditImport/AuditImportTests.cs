namespace ServiceControl.UnitTests.AuditImport
{
    using System.Messaging;
    using NServiceBus;
    using NServiceBus.Transports.Msmq;
    using NUnit.Framework;
    
    [TestFixture]
    public class AuditImportTests
    {
        [Test, Explicit]
        public void SendAMessageWithInvalidHeadersToTheAuditQueue()
        {
            // Generate a bad msg to test MSMQ Audit Queue Importer
            // This message should fail to parse to a transport message because of the bad header and so should end up in the Particular.ServiceControl.Errors queue
            var q = new MessageQueue(MsmqUtilities.GetFullPath(Address.Parse("audit")), false, true, QueueAccessMode.Send);
            using (var tx = new MessageQueueTransaction())
            {
                tx.Begin();
                var message = new Message("Message with invalid headers"){Extension = new byte[]{1}};
                q.Send(message, tx);
                tx.Commit();
            }
        }
    }
}
