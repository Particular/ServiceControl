namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Raven.Client.Documents.Operations.Indexes;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;

class RavenMigrationSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_endpoint_settings_in_document_id_order_in_batches_of_the_requested_size()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        var batches = await CollectBatches(source, EndpointSettingsCategory, batchSize: 2);

        Assert.Multiple(() =>
        {
            Assert.That(batches.Select(batch => batch.Rows.Count), Is.EqualTo(new[] { 2, 2, 1 }), "A batch never exceeds the requested size, and the trailing partial batch is still yielded.");
            Assert.That(batches.SelectMany(batch => batch.Rows).Select(row => row.SourceId), Is.Ordered);
        });
    }

    [Test]
    public async Task Every_row_is_identified_by_the_document_id_the_persister_wrote_it_under()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        var rows = (await CollectBatches(source, EndpointSettingsCategory)).SelectMany(batch => batch.Rows).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(5));
            Assert.That(rows.Select(row => row.SourceId), Has.All.StartWith("EndpointSettings/"), "The cursor the engine checkpoints is a source document id, so a row identified by anything else cannot be resumed after.");
        });
    }

    [Test]
    public async Task Resuming_after_a_cursor_yields_only_what_follows_it()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        var firstBatch = (await CollectBatches(source, EndpointSettingsCategory, batchSize: 2))[0];
        var resumed = (await CollectBatches(source, EndpointSettingsCategory, resumeAfter: firstBatch.Cursor, batchSize: 2)).SelectMany(batch => batch.Rows).ToList();

        Assert.That(resumed, Has.Count.EqualTo(3));
        Assert.That(resumed.Select(row => row.SourceId), Has.No.Member(firstBatch.Rows[0].SourceId));
        Assert.That(resumed.Select(row => row.SourceId), Is.Ordered);
    }

    [Test]
    public async Task Resuming_after_a_cursor_this_source_never_issued_refuses_instead_of_starting_past_it()
    {
        // The cursor is saved in the target database, so a restored backup or a corrected database name leaves
        // one naming a document this source does not have.
        await SeedEndpointSettings(3);
        await using var source = await OpenMigrationSource();

        var refusal = Assert.ThrowsAsync<InvalidOperationException>(() => CollectBatches(source, EndpointSettingsCategory, resumeAfter: "EndpointSettings/zzz-from-another-database"));

        Assert.Multiple(() =>
        {
            Assert.That(refusal.Message, Does.Contain(EndpointSettingsCategory.Id));
            Assert.That(refusal.Message, Does.Contain("EndpointSettings/zzz-from-another-database"));
            Assert.That(refusal.Message, Does.Contain(DatabaseName), "the operator has to be told which database the cursor was looked for in");
        });
    }

    [Test]
    public async Task Counts_every_row_the_category_would_read()
    {
        await SeedEndpointSettings(5);
        await using var source = await OpenMigrationSource();

        Assert.That(await source.Count(EndpointSettingsCategory), Is.EqualTo(5));
    }

    [Test]
    public async Task Counts_a_category_with_nothing_in_it_as_zero()
    {
        await using var source = await OpenMigrationSource();

        Assert.That(await source.Count(EndpointSettingsCategory), Is.Zero, "A category with nothing to copy has to be told apart from one this source declines to count.");
    }

    [Test]
    public async Task Reading_every_category_creates_no_index()
    {
        await SeedEndpointSettings(2);
        await MonitoringDataStore.CreateIfNotExists(new EndpointDetails { Name = "Sales.Orders", HostId = Guid.NewGuid(), Host = "HOST01" });

        var before = await IndexNames();

        await using (var source = await OpenMigrationSource())
        {
            await CollectBatches(source, KnownEndpointsCategory);
            await CollectBatches(source, EndpointSettingsCategory);
        }

        var after = await IndexNames();

        Assert.Multiple(() =>
        {
            Assert.That(after, Is.EquivalentTo(before), "A reader that queries without naming a static index has RavenDB build one and index the whole collection on the customer's live database.");
            Assert.That(after, Has.None.StartWith("Auto/"));
        });
    }

    string DatabaseName => ((RavenPersisterSettings)PersistenceSettings).DatabaseName;

    async Task<string[]> IndexNames() =>
        await DocumentStore.Maintenance.SendAsync(new GetIndexNamesOperation(0, IndexNamePageSize), TestContext.CurrentContext.CancellationToken);

    const int IndexNamePageSize = 1024;

    static readonly MigrationCategory EndpointSettingsCategory = MigrationCategoryRegistry.Find("EndpointSettings");
    static readonly MigrationCategory KnownEndpointsCategory = MigrationCategoryRegistry.Find("KnownEndpoints");

    async Task SeedEndpointSettings(int count)
    {
        for (var index = 0; index < count; index++)
        {
            await EndpointSettingsStore.UpdateEndpointSettings(new EndpointSettings { Name = $"Endpoint{index}", TrackInstances = true });
        }
    }
}
