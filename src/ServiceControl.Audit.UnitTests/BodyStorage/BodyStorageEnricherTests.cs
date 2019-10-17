namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Audit.Auditing.BodyStorage;
    using Audit.Infrastructure.Settings;
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

            await enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), new Dictionary<string, object>());

            Assert.AreEqual(0, fakeStorage.StoredBodySize, "Body should be removed if above threshold");
        }

        [Test]
        public async Task Should_store_body_when_below_threshold()
        {
            var fakeStorage = new FakeBodyStorage();
            var maxBodySizeToStore = 20000;
            var settings = new Settings
            {
                MaxBodySizeToStore = maxBodySizeToStore
            };

            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage, settings);
            var expectedBodySize = maxBodySizeToStore - 1;
            var body = Encoding.UTF8.GetBytes(new string('a', expectedBodySize));

            await enricher.StoreAuditMessageBody(body, new Dictionary<string, string>(), new Dictionary<string, object>());

            Assert.AreEqual(expectedBodySize, fakeStorage.StoredBodySize, "Body should be stored if below threshold");
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