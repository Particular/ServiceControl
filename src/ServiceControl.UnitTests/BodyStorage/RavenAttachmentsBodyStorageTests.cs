﻿namespace ServiceControl.UnitTests.BodyStorage
{
    using System.IO;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Operations.BodyStorage.RavenAttachments;

    [TestFixture]
    public class RavenAttachmentsBodyStorageTests
    {
        [Test]
        public async Task Attachments_with_ids_that_contain_backslash_should_be_readable()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var bodyStore = new RavenAttachmentsBodyStorage {DocumentStore = store};

                var messageId = "messagebodies/3f0240a7-9b2e-4e2a-ab39-6114932adad1\\2055783";
                var contentType = "NotImportant";
                var body = new byte[] {1, 2, 3};

                await bodyStore.Store(messageId, contentType, body.Length, new MemoryStream(body));

                var retrieved = await bodyStore.TryFetch(messageId);
                Assert.True(retrieved.HasResult);
            }
        }
    }
}