namespace ServiceControl.Persistence.Tests.RavenDB.Recoverability
{
    using System;
    using NUnit.Framework;
    using Persistence.RavenDB;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class FailedMessageRetryTests
    {
        [Test]
        public void Should_Extract_Correct_FailedMessageRetryId_From_FailedMessageId()
        {
            var messageId = Guid.NewGuid().ToString();
            var failedMessageId = FailedMessageIdGenerator.MakeDocumentId(messageId);

            var extractedFailedMessageRetryId = RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(FailedMessageIdGenerator.GetMessageIdFromDocumentId(failedMessageId));

            Assert.That(extractedFailedMessageRetryId, Is.EqualTo(RetryDocumentDataStore.MakeFailedMessageRetriesDocumentId(messageId)));
        }
    }
}