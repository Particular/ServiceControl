namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Audit.Auditing.BodyStorage;
    using Audit.Auditing.BodyStorage.RavenAttachments;
    using Audit.Infrastructure.Settings;
    using NServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class BodyStorageEnricherTests
    {
        [Test]
        public async Task Should_remove_body_when_above_threshold()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var attachmentsBodyStorage = new RavenAttachmentsBodyStorage();
                var maxBodySizeToStore = 20000;
                var settings = new Settings
                {
                    MaxBodySizeToStore = maxBodySizeToStore
                };

                var enricher = new BodyStorageFeature.BodyStorageEnricher(attachmentsBodyStorage, settings);
                var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
                var metadata = new Dictionary<string, object>();
                var headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.NewGuid().ToString()},
                };

                using (var bulkInsert = store.BulkInsert())
                {
                    await enricher.StoreAuditMessageBody(bulkInsert, body, headers, metadata);

                    await bulkInsert.DisposeAsync();
                }

                var streamResult = await attachmentsBodyStorage.TryFetch(store, headers[Headers.MessageId]);

                Assert.IsFalse(streamResult.HasResult, "Body should be removed if above threshold");
                Assert.IsFalse(metadata.ContainsKey("Body"));
            }
        }

        [Test]
        public async Task Should_remove_body_when_above_threshold_and_binary()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var attachmentsBodyStorage = new RavenAttachmentsBodyStorage();
                var maxBodySizeToStore = 20000;
                var settings = new Settings
                {
                    MaxBodySizeToStore = maxBodySizeToStore
                };

                var enricher = new BodyStorageFeature.BodyStorageEnricher(attachmentsBodyStorage, settings);
                var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
                var metadata = new Dictionary<string, object>();
                var headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.NewGuid().ToString()},
                    {Headers.ContentType, "application/binary"}
                };

                using (var bulkInsert = store.BulkInsert())
                {
                    await enricher.StoreAuditMessageBody(bulkInsert, body, headers, metadata);

                    await bulkInsert.DisposeAsync();
                }

                var streamResult = await attachmentsBodyStorage.TryFetch(store, headers[Headers.MessageId]);

                Assert.IsFalse(streamResult.HasResult, "Body should be removed if above threshold");
                Assert.IsFalse(metadata.ContainsKey("Body"));
            }
        }

        [Test]
        public async Task Should_store_body_in_metadata_when_below_large_object_heap_and_not_binary()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var attachmentsBodyStorage = new RavenAttachmentsBodyStorage();
                var maxBodySizeToStore = 100000;
                var settings = new Settings
                {
                    MaxBodySizeToStore = maxBodySizeToStore
                };

                var enricher = new BodyStorageFeature.BodyStorageEnricher(attachmentsBodyStorage, settings);
                var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold - 1;
                var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
                var metadata = new Dictionary<string, object>();
                var headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.NewGuid().ToString()},
                };

                using (var bulkInsert = store.BulkInsert())
                {
                    await enricher.StoreAuditMessageBody(bulkInsert, body, headers, metadata);

                    await bulkInsert.DisposeAsync();
                }

                var streamResult = await attachmentsBodyStorage.TryFetch(store, headers[Headers.MessageId]);

                Assert.AreEqual(body, metadata["Body"], "Body should be stored if below threshold");
                Assert.IsFalse(streamResult.HasResult);
            }
        }

        [Test]
        public async Task Should_store_body_in_storage_when_above_large_object_heap_but_below_threshold_and_not_binary()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var attachmentsBodyStorage = new RavenAttachmentsBodyStorage();
                var maxBodySizeToStore = 100000;
                var settings = new Settings
                {
                    MaxBodySizeToStore = maxBodySizeToStore
                };

                var enricher = new BodyStorageFeature.BodyStorageEnricher(attachmentsBodyStorage, settings);
                var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
                var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
                var metadata = new Dictionary<string, object>();
                var headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.NewGuid().ToString()},
                };

                using (var bulkInsert = store.BulkInsert())
                {
                    await enricher.StoreAuditMessageBody(bulkInsert, body, headers, metadata);

                    await bulkInsert.DisposeAsync();
                }

                var streamResult = await attachmentsBodyStorage.TryFetch(store, headers[Headers.MessageId]);

                Assert.AreEqual(expectedBodySize, streamResult.BodySize, "Body should be stored if below threshold");
                Assert.IsFalse(metadata.ContainsKey("Body"));
            }
        }

        [Test]
        public async Task Should_store_body_in_storage_when_below_threshold_and_binary()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var attachmentsBodyStorage = new RavenAttachmentsBodyStorage();
                var maxBodySizeToStore = 100000;
                var settings = new Settings
                {
                    MaxBodySizeToStore = maxBodySizeToStore
                };

                var enricher = new BodyStorageFeature.BodyStorageEnricher(attachmentsBodyStorage, settings);
                var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
                var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
                var metadata = new Dictionary<string, object>();
                var headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.NewGuid().ToString()},
                    {Headers.ContentType, "application/binary"}
                };

                using (var bulkInsert = store.BulkInsert())
                {
                    await enricher.StoreAuditMessageBody(bulkInsert, body, headers, metadata);

                    await bulkInsert.DisposeAsync();
                }

                var streamResult = await attachmentsBodyStorage.TryFetch(store, headers[Headers.MessageId]);

                Assert.AreEqual(expectedBodySize, streamResult.BodySize, "Body should be stored if below threshold");
                Assert.IsFalse(metadata.ContainsKey("Body"));
            }
        }

        [Test]
        public async Task Should_store_body_in_storage_when_below_threshold()
        {
            using (var store = InMemoryStoreBuilder.GetInMemoryStore())
            {
                var attachmentsBodyStorage = new RavenAttachmentsBodyStorage();
                var maxBodySizeToStore = 100000;
                var settings = new Settings
                {
                    MaxBodySizeToStore = maxBodySizeToStore
                };

                var enricher = new BodyStorageFeature.BodyStorageEnricher(attachmentsBodyStorage, settings);
                var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
                var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
                var metadata = new Dictionary<string, object>();
                var headers = new Dictionary<string, string>
                {
                    {Headers.MessageId, Guid.NewGuid().ToString()},
                };

                using (var bulkInsert = store.BulkInsert())
                {
                    await enricher.StoreAuditMessageBody(bulkInsert, body, headers, metadata);

                    await bulkInsert.DisposeAsync();
                }

                var streamResult = await attachmentsBodyStorage.TryFetch(store, headers[Headers.MessageId]);

                Assert.AreEqual(expectedBodySize, streamResult.BodySize, "Body should be stored if below threshold");
                Assert.IsFalse(metadata.ContainsKey("Body"));
            }
        }
    }
}