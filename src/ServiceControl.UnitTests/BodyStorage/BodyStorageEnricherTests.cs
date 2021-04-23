namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using ServiceControl.Operations.BodyStorage;

    [TestFixture]
    public class BodyStorageEnricherTests
    {
        const int MinBodySizeAboveLOHThreshold = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
        const int MaxBodySizeBelowLOHThreshold = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold - 1;

        [Test]
        public async Task Should_store_body_in_storage_when_binary_and_below_LOH_threshold()
        {
            var fakeStorage = new FakeBodyStorage();

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage);
            var expectedBodySize = MinBodySizeAboveLOHThreshold;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();
            var headers = new Dictionary<string, string> { { Headers.ContentType, "application/binary" } };

            await enricher.StoreErrorMessageBody(body, headers, metadata);

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
        }

        [Test]
        public async Task Should_store_body_in_metadata_when_not_binary_and_below_LOH_threshold()
        {
            var fakeStorage = new FakeBodyStorage();

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage);
            var expectedBodySize = MaxBodySizeBelowLOHThreshold;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            await enricher.StoreErrorMessageBody(body, new Dictionary<string, string>(), metadata);

            Assert.AreEqual(body, metadata["Body"], "Body should be stored if below threshold");
            Assert.AreEqual(0, fakeStorage.StoredBodySize);
        }

        [Test]
        public async Task Should_store_body_in_storage_when_not_binary_and_above_LOH_threshold()
        {
            var fakeStorage = new FakeBodyStorage();

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage);
            var expectedBodySize = MinBodySizeAboveLOHThreshold;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new Dictionary<string, object>();

            await enricher.StoreErrorMessageBody(body, new Dictionary<string, string>(), metadata);

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
            Assert.IsFalse(metadata.ContainsKey("Body"));
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