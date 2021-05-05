namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Audit.Auditing;
    using Audit.Auditing.BodyStorage;
    using Audit.Infrastructure.Settings;
    using NServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class BodyStorageEnricherTests
    {
        [Test]
        public async Task Should_remove_body_when_above_threshold()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 20000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore,
                EnableFullTextSearchOnBodies = true
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.AreEqual(0, fakeStorage.StoredBodySize, "Body should be removed if above threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
            Assert.IsNull(message.Body, "Body property was set but shouldn't have been");
            Assert.AreEqual("/messages/someid/body", metadata["BodyUrl"]);
        }

        [Test]
        public async Task Should_remove_body_when_above_threshold_and_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 20000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore,
                EnableFullTextSearchOnBodies = true
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new Dictionary<string, object>();
            var headers = new Dictionary<string, string>
            {
                { Headers.ContentType, "application/binary" },
                { Headers.MessageId, "someid" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.AreEqual(0, fakeStorage.StoredBodySize, "Body should be removed if above threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
            Assert.IsNull(message.Body, "Body property was set but shouldn't have been");
            Assert.AreEqual("/messages/someid/body", metadata["BodyUrl"]);
        }

        [Test]
        public async Task Should_store_body_in_metadata_when_below_large_object_heap_and_not_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore,
                EnableFullTextSearchOnBodies = true
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.AreEqual(body, metadata["Body"], "Body should be stored if below threshold");
            Assert.IsNull(message.Body, "Body property was set but shouldn't have been");
            Assert.AreEqual(0, fakeStorage.StoredBodySize);
            Assert.AreEqual("/messages/someid/body", metadata["BodyUrl"]);
        }

        [Test]
        public async Task Should_store_body_in_body_property_when_full_text_disabled_and_below_large_object_heap_and_not_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore,
                EnableFullTextSearchOnBodies = false
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.AreEqual(body, message.Body, "Body should be stored if below threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"), "Body should not be in metadata");
            Assert.AreEqual(0, fakeStorage.StoredBodySize);
            Assert.AreEqual("/messages/someid/body", metadata["BodyUrl"]);
        }

        [Test]
        public async Task Should_store_body_in_storage_when_above_large_object_heap_but_below_threshold_and_not_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore,
                EnableFullTextSearchOnBodies = true
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
            Assert.IsNull(message.Body, "Body property was set but shouldn't have been");
            Assert.IsFalse(metadata.ContainsKey("Body"));
            Assert.AreEqual("/messages/someid/body", metadata["BodyUrl"]);
        }

        [Test]
        public async Task Should_store_body_in_storage_when_below_threshold_and_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore,
                EnableFullTextSearchOnBodies = true
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();
            var headers = new Dictionary<string, string>
            {
                { Headers.ContentType, "application/binary" },
                { Headers.MessageId, "someid" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
            Assert.IsNull(message.Body, "Body property was set but shouldn't have been");
            Assert.IsFalse(metadata.ContainsKey("Body"));
            Assert.AreEqual("/messages/someid/body", metadata["BodyUrl"]);
        }

        [Test]
        public async Task Should_store_body_in_storage_when_below_threshold()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore,
                EnableFullTextSearchOnBodies = true
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
            Assert.IsNull(message.Body, "Body property was set but shouldn't have been");
            Assert.IsFalse(metadata.ContainsKey("Body"));
            Assert.AreEqual("/messages/someid/body", metadata["BodyUrl"]);
        }

        class FakeBodyStorage : IBodyStorage
        {
            public int StoredBodySize { get; set; }

            public Task Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
            {
                StoredBodySize = bodySize;
                return Task.CompletedTask;
            }

            public Task<StreamResult> TryFetch(string bodyId)
            {
                throw new NotImplementedException();
            }
        }
    }
}