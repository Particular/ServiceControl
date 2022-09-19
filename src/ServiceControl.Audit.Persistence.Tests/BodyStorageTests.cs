namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    [TestFixture]
    class BodyStorageTests : PersistenceTestFixture
    {
        [Test]
        public async Task Basic_Roundtrip()
        {
            var bodyId = "MyBodyId";
            var contentType = "text/xml";
            var body = new byte[5];
            new Random().NextBytes(body);

            using (var stream = new MemoryStream(body))
            {
                await BodyStorage.Store(bodyId, contentType, body.Length, stream)
                    ;
            }

            var result = await BodyStorage.TryFetch(bodyId);

            Assert.That(result.HasResult, Is.True);
            Assert.That(result.ContentType, Is.EqualTo(contentType));
            Assert.That(result.BodySize, Is.EqualTo(body.Length));
            Assert.That(result.Etag, Is.Not.Null.Or.Empty);
            Assert.That(result.Stream, Is.Not.Null);
            var resultBody = new byte[body.Length];
            var readBytes = await result.Stream.ReadAsync(resultBody, 0, body.Length)
                ;
            Assert.That(readBytes, Is.EqualTo(body.Length));
            Assert.That(resultBody, Is.EqualTo(body));

            result.Stream.Dispose();
        }

        [Test]
        public async Task Handles_no_results_gracefully()
        {
            var nonExistentBodyId = Guid.NewGuid().ToString();
            var result = await BodyStorage.TryFetch(nonExistentBodyId)
                ;

            Assert.That(result.HasResult, Is.False);
        }
    }
}