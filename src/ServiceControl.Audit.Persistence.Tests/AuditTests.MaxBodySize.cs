namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    partial class AuditTests
    {
        [Test]
        public async Task Does_respect_max_message_body()
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);

            var body = new byte[MAX_BODY_SIZE + 1000];
            Random.Shared.NextBytes(body);
            var processedMessage = MakeMessage();

            await unitOfWork.RecordProcessedMessage(processedMessage, body);

            await unitOfWork.Complete();

            var bodyId = GetBodyId(processedMessage);

            var retrievedMessage = await MessagesViewStore.GetMessageBody(bodyId, TestContext.CurrentContext.CancellationToken);

            Assert.That(retrievedMessage, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(retrievedMessage.Found, Is.True);
                Assert.That(retrievedMessage.HasContent, Is.False);
            }
        }
    }
}
