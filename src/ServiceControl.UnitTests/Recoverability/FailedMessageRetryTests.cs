namespace ServiceControl.UnitTests.Operations
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Recoverability;

    [TestFixture]
    public class FailedMessageRetryTests
    {
        [Test]
        public void Should_Extract_Correct_FailedMessageRetryId_From_FailedMessageId()
        {
            var messageId = Guid.NewGuid().ToString();
            var failedMessageId = FailedMessage.MakeDocumentId(messageId);

            var extractedFailedMessageRetryId = FailedMessageRetry.MakeDocumentIdFromFailedMessageId(failedMessageId);

            Assert.AreEqual(FailedMessageRetry.MakeDocumentId(messageId), extractedFailedMessageRetryId);
        }
    }
}