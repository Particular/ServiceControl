using ServiceControl.Audit.Auditing;

namespace ServiceControl.UnitTests.BodyStorage
{
    using System.Collections.Generic;
    using System.Text;
    using Audit.Auditing.BodyStorage;
    using Audit.Infrastructure.Settings;
    using NServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class BodyStorageEnricherTests
    {
        [Test]
        public void Should_remove_body_when_above_threshold()
        {
            var maxBodySizeToStore = 20000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new ProcessedMessageData();

            enricher.StoreAuditMessageBody("ID", body, new Dictionary<string, string>(), metadata);

            Assert.IsNull(metadata.Body);
        }

        [Test]
        public void Should_remove_body_when_above_threshold_and_binary()
        {
            var maxBodySizeToStore = 20000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var metadata = new ProcessedMessageData();
            var headers = new Dictionary<string, string> { { Headers.ContentType, "application/binary"}};

            enricher.StoreAuditMessageBody("ID", body, headers, metadata);

            Assert.IsNull(metadata.Body);
        }

        [Test]
        public void Should_store_body_in_metadata_when_below_large_object_heap_and_not_binary()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new ProcessedMessageData();

            enricher.StoreAuditMessageBody("ID", body, new Dictionary<string, string>(), metadata);

            Assert.AreEqual(body, metadata.Body);
        }

        [Test]
        public void Should_store_body_in_storage_when_above_large_object_heap_but_below_threshold_and_not_binary()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new ProcessedMessageData();

            enricher.StoreAuditMessageBody("ID", body, new Dictionary<string, string>(), metadata);

            Assert.IsNull(metadata.Body);
        }

        [Test]
        public void Should_store_body_in_storage_when_below_threshold_and_binary()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new ProcessedMessageData();
            var headers = new Dictionary<string, string> { { Headers.ContentType, "application/binary"}};

            enricher.StoreAuditMessageBody("ID", body, headers, metadata);

            Assert.IsNull(metadata.Body);
        }

        [Test]
        public void Should_store_body_in_storage_when_below_threshold()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageFeature.BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var metadata = new ProcessedMessageData();

            enricher.StoreAuditMessageBody("ID", body, new Dictionary<string, string>(), metadata);

            Assert.IsNull(metadata.Body);
        }
    }
}