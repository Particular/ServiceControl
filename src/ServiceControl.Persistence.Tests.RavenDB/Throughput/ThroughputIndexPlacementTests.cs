namespace ServiceControl.Persistence.Tests.RavenDB.Throughput;

using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations.Indexes;
using ServiceControl.Persistence.RavenDB;
using ServiceControl.Persistence.RavenDB.Throughput.Indexes;

[TestFixture]
class ThroughputIndexPlacementTests : RavenPersistenceTestBase
{
    [TestCase(IndexLockMode.Unlock)]
    [TestCase(IndexLockMode.LockedIgnore)]
    [TestCase(IndexLockMode.LockedError)]
    public async Task Setup_should_delete_the_throughput_index_from_the_error_database(IndexLockMode lockMode)
    {
        var settings = (RavenPersisterSettings)PersistenceSettings;
        var indexName = new EndpointIndex().IndexName;

        // Earlier versions deployed the throughput indexes to the error database. An operator might have locked it
        // after migrating it to Lucene.
        await DocumentStore.Maintenance.SendAsync(new PutIndexesOperation(new IndexDefinition
        {
            Name = indexName,
            LockMode = lockMode,
            Maps = ["from message in docs.EndpointDocuments select new { message.EndpointId.ThroughputSource, message.EndpointId.Name }"]
        }));

        await new DatabaseSetup(settings, DocumentStore).Execute();

        var errorDatabaseIndex = await DocumentStore.Maintenance.SendAsync(new GetIndexOperation(indexName));
        var throughputDatabaseIndex = await DocumentStore.Maintenance.ForDatabase(settings.ThroughputDatabaseName).SendAsync(new GetIndexOperation(indexName));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errorDatabaseIndex, Is.Null);
            Assert.That(throughputDatabaseIndex, Is.Not.Null);
        }
    }
}
