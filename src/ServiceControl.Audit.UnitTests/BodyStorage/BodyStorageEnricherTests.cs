namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
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
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new Dictionary<string, object>();
            
            await enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), metadata);

            Assert.AreEqual(0, fakeStorage.StoredBodySize, "Body should be removed if above threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
        }
        
        [Test]
        public async Task Should_remove_body_when_above_threshold_and_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 20000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new Dictionary<string, object>();
            var headers = new Dictionary<string, string> { { Headers.ContentType, "application/binary"}};
            
            await enricher.StoreAuditMessageBody(body, headers, metadata);

            Assert.AreEqual(0, fakeStorage.StoredBodySize, "Body should be removed if above threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
        }

        [Test]
        public async Task Should_store_body_in_metadata_when_below_large_object_heap_and_not_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();
            
            await enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), metadata);

            Assert.AreEqual(body, metadata["Body"], "Body should be stored if below threshold");
            Assert.AreEqual(0, fakeStorage.StoredBodySize);
        }
        
        [Test]
        public async Task Should_store_body_in_storage_when_above_large_object_heap_but_below_threshold_and_not_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();
            
            await enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), metadata);

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
        }
        
        [Test]
        public async Task Should_store_body_in_storage_when_below_threshold_and_binary()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();
            var headers = new Dictionary<string, string> { { Headers.ContentType, "application/binary"}};
            
            await enricher.StoreAuditMessageBody(body, headers, metadata);

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
        }
        
        class FakeBodyStorage : IBodyStorage
        {
            public int StoredBodySize { get; set; }

            public Task<string> Store(string bodyId, string contentType, int bodySize, Stream bodyStream)
            {
                StoredBodySize = bodySize;
                return Task.FromResult(default(string));
            }

            public Task<StreamResult> TryFetch(string bodyId)
            {
                throw new NotImplementedException();
            }
        }
    }
}