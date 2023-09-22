namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using MessageFailures;
    using NUnit.Framework;
    using Raven.Client.Documents;

    [TestFixture]
    sealed class RavenAttachmentsBodyStorageTests : PersistenceTestBase
    {
        [Test]
        public async Task Attachments_with_ids_that_contain_backslash_should_be_readable()
        {
            var messageId = "3f0240a7-9b2e-4e2a-ab39-6114932adad1\\2055783";
            var contentType = "NotImportant";
            var body = BitConverter.GetBytes(0xDEADBEEF);

            using (var session = GetRequiredService<IDocumentStore>().OpenAsyncSession())
            {
                await session.StoreAsync(new FailedMessage { Id = messageId });
                await session.SaveChangesAsync();
            }

            await BodyStorage.Store(messageId, contentType, body.Length, new MemoryStream(body));

            var retrieved = await BodyStorage.TryFetch(messageId);
            Assert.IsNotNull(retrieved);
            Assert.True(retrieved.HasResult);
            Assert.AreEqual(contentType, retrieved.ContentType);

            var buffer = new byte[retrieved.BodySize];
            retrieved.Stream.Read(buffer, 0, retrieved.BodySize);

            Assert.AreEqual(body, buffer);
        }
    }
}