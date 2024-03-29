namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Audit.Auditing;
    using Audit.Auditing.BodyStorage;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence;

    [TestFixture]
    public class BodyStorageEnricherTests
    {
        [Test]
        public async Task Should_remove_body_when_above_threshold()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 20000;

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), true, maxBodySizeToStore));
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { Headers.ProcessingEndpoint, "someendpoint" },
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

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), true, maxBodySizeToStore));
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new Dictionary<string, object>();
            var headers = new Dictionary<string, string>
            {
                { Headers.ContentType, "application/binary" },
                { Headers.MessageId, "someid" },
                { Headers.ProcessingEndpoint, "someendpoint" },
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

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), true, maxBodySizeToStore));
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = "someid",
                ["ServiceControl.Retry.UniqueMessageId"] = "someid",
                [Headers.ProcessingEndpoint] = "someendpoint",
                [Headers.ContentType] = "text/xml"
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

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), false, maxBodySizeToStore));
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = "someid",
                ["ServiceControl.Retry.UniqueMessageId"] = "someid",
                [Headers.ProcessingEndpoint] = "someendpoint",
                [Headers.ContentType] = "text/xml"
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

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), true, maxBodySizeToStore));
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = "someid",
                ["ServiceControl.Retry.UniqueMessageId"] = "someid",
                [Headers.ProcessingEndpoint] = "someendpoint",
                [Headers.ContentType] = "text/xml"
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

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), true, maxBodySizeToStore));
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();
            var headers = new Dictionary<string, string>
            {
                { Headers.ContentType, "application/binary" },
                { Headers.MessageId, "someid" },
                { Headers.ProcessingEndpoint, "someendpoint" },
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

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), true, maxBodySizeToStore));
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { Headers.ProcessingEndpoint, "someendpoint" },
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
        public async Task Should_store_body_in_storage_when_encoding_fails()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;

            var enricher = new BodyStorageEnricher(fakeStorage, new PersistenceSettings(TimeSpan.FromHours(1), true, maxBodySizeToStore));
            var body = new byte[] { 0x00, 0xDE };
            var metadata = new Dictionary<string, object>();

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, "someid" },
                { Headers.ProcessingEndpoint, "someendpoint" },
                { "ServiceControl.Retry.UniqueMessageId", "someid" }
            };

            var message = new ProcessedMessage(headers, metadata);

            await enricher.StoreAuditMessageBody(body, message);

            Assert.IsTrue(fakeStorage.StoredBodySize > 0);
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
