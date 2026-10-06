namespace ServiceControl.Persistence.Tests.RavenDB.Recoverability
{
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Raven.Client.Documents.Operations.Indexes;

    // The staging query must use a static index. A dynamic query makes RavenDB create an
    // Auto/RetryBatches/ByStatus index. An auto index has no per-index search engine setting, so it
    // keeps the "Error Database Search Engine" custom check failing after all static indexes use Lucene.
    [TestFixture]
    class RetryStagingStoreIndexTests : RavenPersistenceTestBase
    {
        [Test]
        public async Task Getting_the_staging_batch_creates_no_auto_index()
        {
            await RetryStagingStore.GetStagingBatch();

            var indexNames = await DocumentStore.Maintenance.SendAsync(new GetIndexNamesOperation(0, int.MaxValue));

            Assert.That(indexNames.Where(name => name.StartsWith("Auto/")), Is.Empty);
        }
    }
}
