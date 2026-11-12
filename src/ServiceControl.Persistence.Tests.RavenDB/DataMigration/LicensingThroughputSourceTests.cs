namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using Raven.Client.Documents.Conventions;
using ServiceControl.Persistence.DataMigration;
using RavenLicensingDataStore = Persistence.RavenDB.Throughput.LicensingDataStore;
using ServiceControl.Persistence.RavenDB.Throughput.Models;

class LicensingThroughputSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_one_endpoints_whole_time_series_as_one_batch_and_cursors_on_its_document_id()
    {
        var documentId = "Sales.Orders/Broker";

        using (var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName))
        {
            await session.StoreAsync(new EndpointDocument(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker)), documentId);
            await session.SaveChangesAsync();

            session.IncrementalTimeSeriesFor(documentId, RavenLicensingDataStore.ThroughputTimeSeriesName)
                .Increment(new DateTime(2026, 1, 1), 100);
            session.IncrementalTimeSeriesFor(documentId, RavenLicensingDataStore.ThroughputTimeSeriesName)
                .Increment(new DateTime(2026, 1, 2), 250);
            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingThroughput");

        var batches = await CollectBatches(source, category);

        Assert.That(batches, Has.Count.EqualTo(1));
        Assert.That(batches[0].Cursor, Is.EqualTo(documentId));
        Assert.That(batches[0].Rows.Select(row => row.Document), Is.EqualTo(new[]
        {
            new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), new DateOnly(2026, 1, 1), 100),
            new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), new DateOnly(2026, 1, 2), 250)
        }), "each row carries the endpoint its count belongs to, because EndpointDailyThroughput does not, and the day and total RavenDB holds");
        Assert.That(batches[0].Rows.Select(row => ((ThroughputDayRow)row.Document).Endpoint.Name), Is.EqualTo(new[] { "Sales.Orders", "Sales.Orders" }), "the endpoint's name keeps its case, which the record's equality ignores");
    }

    [Test]
    public async Task A_resume_reads_only_the_endpoints_after_the_cursor_whatever_order_they_were_stored_in()
    {
        foreach (var name in new[] { "Z", "A", "M" })
        {
            var documentId = $"{name}/Broker";
            using var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName);
            await session.StoreAsync(new EndpointDocument(new EndpointIdentifier(name, ThroughputSource.Broker)), documentId);
            await session.SaveChangesAsync();

            session.IncrementalTimeSeriesFor(documentId, RavenLicensingDataStore.ThroughputTimeSeriesName).Increment(new DateTime(2026, 1, 1), 100);
            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingThroughput");

        var batches = await CollectBatches(source, category);
        var resumed = await CollectBatches(source, category, resumeAfter: batches[0].Cursor);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { "A/Broker", "M/Broker", "Z/Broker" }), "one endpoint per batch, in id order rather than the order RavenDB stored them");
            Assert.That(resumed.SelectMany(batch => batch.Rows).Select(row => ((ThroughputDayRow)row.Document).Endpoint.Name), Is.EqualTo(new[] { "M", "Z" }), "a resume reads only the days of endpoints after the cursor");
        }
    }

    [Test]
    public async Task Endpoints_whose_names_differ_only_in_case_and_trailing_spaces_come_in_one_batch()
    {
        foreach (var name in new[] { "Sales.Orders", "Sales.Orders.Archive", "SALES.ORDERS  " })
        {
            var documentId = $"{name}/Broker";
            using var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName);
            await session.StoreAsync(new EndpointDocument(new EndpointIdentifier(name, ThroughputSource.Broker)), documentId);
            await session.SaveChangesAsync();

            session.IncrementalTimeSeriesFor(documentId, RavenLicensingDataStore.ThroughputTimeSeriesName).Increment(new DateTime(2026, 1, 1), 100);
            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingThroughput");

        var batches = await CollectBatches(source, category);
        var resumed = await CollectBatches(source, category, resumeAfter: batches[0].Cursor);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(batches.Select(batch => batch.Cursor), Is.EqualTo(new[] { "Sales.Orders.Archive/Broker", "Sales.Orders/Broker" }), "each group cursors on its last id in ordinal order, so a resume never splits a group");
            Assert.That(batches[1].Rows.Select(row => ((ThroughputDayRow)row.Document).Endpoint.Name), Is.EquivalentTo(new[] { "Sales.Orders", "SALES.ORDERS  " }), "the writer can add the two endpoints' days only when they arrive in one batch");
            Assert.That(resumed.SelectMany(batch => batch.Rows).Select(row => ((ThroughputDayRow)row.Document).Endpoint.Name), Is.EquivalentTo(new[] { "Sales.Orders", "SALES.ORDERS  " }), "a resume after the first group reads the whole second group");
        }
    }

    [Test]
    public async Task Reads_more_endpoints_than_one_session_accepts_requests()
    {
        var endpointCount = new DocumentConventions().MaxNumberOfRequestsPerSession + 1;

        using (var session = DocumentStore.OpenAsyncSession(ThroughputDatabaseName))
        {
            for (var index = 0; index < endpointCount; index++)
            {
                await session.StoreAsync(new EndpointDocument(new EndpointIdentifier($"Endpoint{index}", ThroughputSource.Broker)), $"Endpoint{index}/Broker");
            }

            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingThroughput");

        var batches = await CollectBatches(source, category);

        Assert.That(batches, Has.Count.EqualTo(endpointCount), "each endpoint's series is its own request, and one session refuses this many");
        Assert.That(await source.Count(category), Is.Zero);
    }
}
