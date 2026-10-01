namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;

    partial class AuditTests
    {
        [Test]
        public async Task Can_roundtrip_message_body()
        {
            string expectedContentType = "text/plain";
            await using var unitOfWork = await StartAuditUnitOfWork(1);

            var body = new byte[100];
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
                Assert.That(retrievedMessage.HasContent, Is.True);
                Assert.That(retrievedMessage.ContentLength, Is.EqualTo(body.Length));
                Assert.That(retrievedMessage.Version.HasValue, Is.True);
                Assert.That(retrievedMessage.StreamContent, Is.Not.Null);
                Assert.That(retrievedMessage.ContentType, Is.EqualTo(expectedContentType));
            }

            var resultBody = new byte[body.Length];
            var readBytes = await retrievedMessage.StreamContent.ReadAsync(resultBody, 0, body.Length);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(readBytes, Is.EqualTo(body.Length));
                Assert.That(resultBody, Is.EqualTo(body));
            }
        }
    }
}
