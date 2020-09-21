namespace ServiceControl.UnitTests.Operations
{
    using System;
    using NUnit.Framework;
    using MessageFailures;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class FailedMessageRetryTests
    {
        [Test]
        public void Should_Extract_Correct_FailedMessageRetryId_From_FailedMessageId()
        {
            var messageId = Guid.NewGuid().ToString();
            var failedMessageId = FailedMessage.MakeDocumentId(messageId);

            var extractedFailedMessageRetryId = FailedMessageRetry.MakeDocumentId(FailedMessage.GetMessageIdFromDocumentId(failedMessageId));

            Assert.AreEqual(FailedMessageRetry.MakeDocumentId(messageId), extractedFailedMessageRetryId);
        }
    }
}