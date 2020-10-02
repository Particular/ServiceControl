namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Operations.BodyStorage;

    [TestFixture]
    public class BodyStorageEnricherTests
    {
        [Test]
        public async Task Should_store_body_regardless_of_the_body_size()
        {
            var fakeStorage = new FakeBodyStorage();
            // previously the max body storage default was larger than 100 KB
            var enricher = new BodyStorageFeature.BodyStorageEnricher(fakeStorage);
            const int ExpectedBodySize = 150000;
            var body = Encoding.UTF8.GetBytes(new string('a', ExpectedBodySize));

            await enricher.StoreErrorMessageBody(body, new Dictionary<string, string>(), new Dictionary<string, object>());

            Assert.AreEqual(ExpectedBodySize, fakeStorage.StoredBodySize, "Body should never be dropped for error messages");
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