namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using RavenLicensingDataStore = Persistence.RavenDB.Throughput.LicensingDataStore;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

class LicensingEndpointsSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_endpoints_and_skips_the_four_singleton_documents()
    {
        using (var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName))
        {
            await session.StoreAsync(new EndpointDocument(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker))
            {
                SanitizedName = "sales.orders"
            }, "Sales.Orders/Broker");

            await session.StoreAsync(new { }, RavenLicensingDataStore.AuditServiceMetadataDocumentId);
            await session.StoreAsync(new { MaskedStrings = new[] { "secret" } }, RavenLicensingDataStore.ReportMasksDocumentId);
            await session.StoreAsync(new { }, RavenLicensingDataStore.BrokerMetadataDocumentId);
            await session.StoreAsync(new { LicenseId = "abc" }, RavenLicensingDataStore.LicencedEndpointDetailsDocumentId);

            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingEndpoints");

        var batches = await CollectBatches(source, category);
        var rows = batches.SelectMany(batch => batch.Rows).ToList();

        Assert.That(rows, Has.Count.EqualTo(1));
        var endpoint = (Endpoint)rows[0].Document;
        Assert.That(endpoint.Id.Name, Is.EqualTo("Sales.Orders"));
    }

    [Test]
    public async Task Reads_endpoints_in_id_order_and_resumes_after_the_cursor_whatever_order_they_were_stored_in()
    {
        foreach (var name in new[] { "Z", "A", "M" })
        {
            using var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName);
            await session.StoreAsync(new EndpointDocument(new EndpointIdentifier(name, ThroughputSource.Broker)) { SanitizedName = name.ToLowerInvariant() }, $"{name}/Broker");
            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.LicensingEndpoints);

        var batches = await CollectBatches(source, category, batchSize: 1);
        var resumed = await CollectBatches(source, category, resumeAfter: batches[0].Cursor, batchSize: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { "A/Broker", "M/Broker", "Z/Broker" }), "each batch cursors on its last endpoint, in id order rather than the order RavenDB stored them");
            Assert.That(resumed.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.EqualTo(new[] { "M/Broker", "Z/Broker" }), "a resume reads only the endpoints after the cursor");
        }
    }

    [Test]
    public async Task A_document_of_another_collection_is_neither_read_nor_counted()
    {
        using (var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName))
        {
            await session.StoreAsync(new EndpointDocument(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker))
            {
                SanitizedName = "sales.orders"
            }, "Sales.Orders/Broker");

            var stray = new { Setting = "left by an older version" };
            await session.StoreAsync(stray, "LegacySettings/1");
            session.Advanced.GetMetadataFor(stray)["@collection"] = "LegacySettings";

            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.LicensingEndpoints);

        var rows = (await CollectBatches(source, category)).SelectMany(batch => batch.Rows).ToList();
        var count = await source.Count(category);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rows.Select(row => row.SourceId), Has.None.StartsWith("LegacySettings/"), "the product reads endpoints only from the EndpointDocuments collection, so a document of any other kind is not an endpoint");
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(count, Is.EqualTo(rows.Count), "the count reads what the copy reads, so --migration-verify expects no row for the stray document");
        }
    }
}
