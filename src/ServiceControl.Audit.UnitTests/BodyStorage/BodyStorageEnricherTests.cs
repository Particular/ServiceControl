namespace ServiceControl.UnitTests.BodyStorage
{
    using System.Collections.Generic;
    using System.Text;
    using Audit.Auditing;
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

            var enricher = new BodyStorageEnricher(settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var searchTerms = new Dictionary<string, string>();

            enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), new ProcessedMessage(), searchTerms);

            Assert.IsFalse(searchTerms.ContainsKey("Body"));
        }

        [Test]
        public void Should_remove_body_when_above_threshold_and_binary()
        {
            var maxBodySizeToStore = 20000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageEnricher(settings);
            var body = Encoding.UTF8.GetBytes(new string('a', maxBodySizeToStore + 1));
            var searchTerms = new Dictionary<string, string>();
            var headers = new Dictionary<string, string> { { Headers.ContentType, "application/binary"}};

            enricher.StoreAuditMessageBody(body, headers, new ProcessedMessage(), searchTerms);

            Assert.IsFalse(searchTerms.ContainsKey("Body"));
        }

        [Test]
        public void Should_store_body_in_metadata_when_below_large_object_heap_and_not_binary()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var searchTerms = new Dictionary<string, string>();

            enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), new ProcessedMessage(), searchTerms);

            Assert.AreEqual(body, searchTerms["Body"], "Body should be stored if below threshold");
        }

        [Test]
        public void Should_store_body_in_storage_when_above_large_object_heap_but_below_threshold_and_not_binary()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var searchTerms = new Dictionary<string, string>();

            enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), new ProcessedMessage(), searchTerms);

            Assert.IsFalse(searchTerms.ContainsKey("Body"));
        }

        [Test]
        public void Should_store_body_in_storage_when_below_threshold_and_binary()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var searchTerms = new Dictionary<string, string>();
            var headers = new Dictionary<string, string> { { Headers.ContentType, "application/binary"}};

            enricher.StoreAuditMessageBody(body, headers, new ProcessedMessage(), searchTerms);

            Assert.IsFalse(searchTerms.ContainsKey("Body"));
        }

        [Test]
        public void Should_store_body_in_storage_when_below_threshold()
        {
            var maxBodySizeToStore = 100000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageEnricher(settings);
            var expectedBodySize = BodyStorageEnricher.LargeObjectHeapThreshold + 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));
            var searchTerms = new Dictionary<string, string>();

            enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), new ProcessedMessage(), searchTerms);

            Assert.IsFalse(searchTerms.ContainsKey("Body"));
        }
    }
}