namespace ServiceControl.Audit.Persistence.Tests.MongoDB
{
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.MongoDB.BodyStorage;

    /// <summary>
    /// Unit tests for NullBodyStorage (no MongoDB required).
    /// </summary>
    [TestFixture]
    class NullBodyStorageTests
    {
        [Test]
        public async Task Store_should_complete_without_error()
        {
            var storage = new NullBodyStorage();
            var bodyContent = "test body content";

            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(bodyContent));

            // Should not throw
            await storage.Store("test-id", "text/plain", (int)bodyStream.Length, bodyStream, CancellationToken.None).ConfigureAwait(false);
        }

        [Test]
        public async Task TryFetch_should_return_no_result()
        {
            var storage = new NullBodyStorage();

            var result = await storage.TryFetch("any-body-id", CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.HasResult, Is.False, "NullBodyStorage should always return HasResult=false");
        }

        [Test]
        public async Task TryFetch_should_return_no_result_even_after_store()
        {
            var storage = new NullBodyStorage();
            var bodyId = "stored-body-id";
            var bodyContent = "some content";

            using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(bodyContent));
            await storage.Store(bodyId, "text/plain", (int)bodyStream.Length, bodyStream, CancellationToken.None).ConfigureAwait(false);

            var result = await storage.TryFetch(bodyId, CancellationToken.None).ConfigureAwait(false);

            Assert.That(result.HasResult, Is.False, "NullBodyStorage should not store anything");
        }
    }
}
