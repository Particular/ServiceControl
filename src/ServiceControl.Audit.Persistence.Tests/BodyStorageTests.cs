namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure;
    using NUnit.Framework;

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

            using (var stream = Memory.Manager.GetStream(bodyId, body, 0, body.Length))
            {
                await BodyStorage.Store(bodyId, contentType, body.Length, stream)
                    .ConfigureAwait(false);
            }

            var result = await BodyStorage.TryFetch(bodyId).ConfigureAwait(false);

            Assert.That(result.HasResult, Is.True);
            Assert.That(result.ContentType, Is.EqualTo(contentType));
            Assert.That(result.BodySize, Is.EqualTo(body.Length));
            Assert.That(result.Etag, Is.Not.Null.Or.Empty);
            Assert.That(result.Stream, Is.Not.Null);
            var resultBody = new byte[body.Length];
            var readBytes = await result.Stream.ReadAsync(resultBody, 0, body.Length)
                .ConfigureAwait(false);
            Assert.That(readBytes, Is.EqualTo(body.Length));
            Assert.That(resultBody, Is.EqualTo(body));

            result.Stream.Dispose();
        }

        [Test]
        public async Task Handles_no_results_gracefully()
        {
            var nonExistentBodyId = Guid.NewGuid().ToString();
            var result = await BodyStorage.TryFetch(nonExistentBodyId)
                .ConfigureAwait(false);

            Assert.That(result.HasResult, Is.False);
        }
    }
}