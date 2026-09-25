namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using ServiceControl.Audit.Infrastructure;

    [TestFixture]
    class BodyVersionTests : PersistenceTestFixture
    {
        [Test]
        public async Task In_memory_list_queries_remain_unversioned()
        {
            var result = await DataStore.GetMessages(false, new PagingInfo(), new SortInfo("message_id", "asc"));

            Assert.That(result.QueryStats.Version.HasValue, Is.False);
        }

        [Test]
        public async Task Replacing_a_body_changes_its_validator_without_changing_reads()
        {
            var bodyId = Guid.NewGuid().ToString();
            await BodyStorage.Store(bodyId, "text/plain", 1, new MemoryStream([1]));
            var first = await BodyStorage.TryFetch(bodyId);
            var repeated = await BodyStorage.TryFetch(bodyId);

            await BodyStorage.Store(bodyId, "text/plain", 1, new MemoryStream([2]));
            var replaced = await BodyStorage.TryFetch(bodyId);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(first.Version.HasValue, Is.True);
                Assert.That(repeated.Version, Is.EqualTo(first.Version));
                Assert.That(replaced.Version, Is.Not.EqualTo(first.Version));
            }

            first.StreamContent.Dispose();
            repeated.StreamContent.Dispose();
            replaced.StreamContent.Dispose();
        }
    }
}
