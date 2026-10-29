namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class LicensingThroughputMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public void Writing_counts_before_their_endpoint_is_refused_naming_the_endpoint()
    {
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingThroughput");
        var day = new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), new DateOnly(2026, 1, 1), 100);
        var batch = new MigrationBatch([new MigrationRow("Sales.Orders/Broker|2026-01-01", day, new Dictionary<string, object>())], "Sales.Orders/Broker");
        var checkpointToExtend = new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, batch.Cursor, 0, 0, null, null, null, null, null, null);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(() => Target.Write(category, batch, checkpointToExtend));

        Assert.That(exception!.Message, Does.Contain("Endpoint Sales.Orders from Broker does not exist"), "a missing endpoint is refused by name and source, as the product's own throughput write refuses it, rather than surfacing as a raw foreign key violation");
    }

    [Test]
    public async Task An_endpoint_skipped_by_its_own_copy_blocks_the_counts_and_none_are_written()
    {
        await SeedThroughputSource(new Endpoint("Billing", ThroughputSource.Broker) { SanitizedName = new string('s', 451) });
        await SeedThroughputTimeSeries("Billing/Broker", new DateOnly(2026, 1, 1), 50);

        await CopyCategory("LicensingEndpoints");
        Assert.ThrowsAsync<InvalidOperationException>(() => CopyCategory("LicensingThroughput"));

        var endpoints = await ReadCheckpoint("LicensingEndpoints");
        var throughput = await ReadCheckpoint("LicensingThroughput");
        var written = await QueryTarget(dbContext => dbContext.LicensingEndpointThroughput.CountAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoints.State, Is.EqualTo(MigrationCategoryState.CompleteWithErrors), "the endpoint whose sanitized name is too long is skipped as a fault");
            Assert.That(throughput.State, Is.EqualTo(MigrationCategoryState.Blocked), "a Failed predecessor blocks the counts, so the writer never meets a day whose endpoint was skipped");
            Assert.That(throughput.LastError, Does.Contain("must follow LicensingEndpoints, which is CompleteWithErrors"));
            Assert.That(written, Is.Zero);
        }
    }

    [Test]
    public async Task A_document_of_another_collection_is_not_read_so_the_endpoints_complete_and_the_counts_copy()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await SeedThroughputTimeSeries("Sales.Orders/Broker", new DateOnly(2026, 1, 1), 100);

        using (var session = SourceStore.OpenAsyncSession(Source.ThroughputDatabase))
        {
            var stray = new { Setting = "left by an older version" };
            await session.StoreAsync(stray, "LegacySettings/1");
            session.Advanced.GetMetadataFor(stray)["@collection"] = "LegacySettings";
            await session.SaveChangesAsync();
        }

        var endpoints = await CopyCategory("LicensingEndpoints");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoints.Saved.State, Is.EqualTo(MigrationCategoryState.Complete), "a document outside the EndpointDocuments collection is not an endpoint, so it is not skipped as a nameless one that blocks the counts");
            Assert.That(endpoints.Copied, Is.EqualTo(1));
            Assert.That(endpoints.Skipped, Is.Zero);
        }

        var throughput = await CopyCategory("LicensingThroughput");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(throughput.Saved.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(throughput.Copied, Is.EqualTo(1));
            Assert.That(await QueryTarget(dbContext => dbContext.LicensingEndpointThroughput.CountAsync()), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task The_registry_order_blocks_the_counts_until_their_endpoints_are_copied()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await SeedThroughputTimeSeries("Sales.Orders/Broker", new DateOnly(2026, 1, 1), 100);

        Assert.ThrowsAsync<InvalidOperationException>(() => CopyCategory("LicensingThroughput"));
        var blocked = await ReadCheckpoint("LicensingThroughput");

        await CopyCategory("LicensingEndpoints");
        var result = await CopyCategory("LicensingThroughput");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(blocked.State, Is.EqualTo(MigrationCategoryState.Blocked));
            Assert.That(blocked.LastError, Does.Contain("must follow LicensingEndpoints"));
            Assert.That(result.Copied, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Running_the_category_twice_produces_the_same_count()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await SeedThroughputTimeSeries("Sales.Orders/Broker", new DateOnly(2026, 1, 1), 100);
        await CopyCategory("LicensingEndpoints");

        await CopyCategory("LicensingThroughput");
        await DeleteCheckpoint("LicensingThroughput");
        var second = await CopyCategory("LicensingThroughput");

        var total = await QueryTarget(dbContext => dbContext.LicensingEndpointThroughput
            .Where(row => row.NormalizedName == "sales.orders" && row.ThroughputSource == ThroughputSource.Broker && row.DateUtc == new DateOnly(2026, 1, 1))
            .Select(row => row.MessageCount)
            .SingleAsync());

        Assert.That(second.Copied, Is.EqualTo(1), "the second run wrote nothing, so the count below cannot tell setting from adding");
        Assert.That(total, Is.EqualTo(100), "running the category twice must set the day's count, not add to it");
    }

    [Test]
    public async Task Two_names_differing_only_in_a_trailing_space_set_one_day_without_failing_the_batch()
    {
        var endpoints = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingEndpoints");
        var endpointBatch = new MigrationBatch(
            [
                new MigrationRow("Sales.Orders /Broker", new Endpoint("Sales.Orders ", ThroughputSource.Broker) { SanitizedName = "sales.orders" }, new Dictionary<string, object>()),
                new MigrationRow("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" }, new Dictionary<string, object>())
            ],
            "Sales.Orders/Broker");
        await Target.Write(endpoints, endpointBatch, new MigrationCheckpoint(endpoints.Id, MigrationCategoryState.InProgress, endpointBatch.Cursor, 0, 0, null, null, null, null, null, null));

        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingThroughput");
        var day = new DateOnly(2026, 1, 1);
        var batch = new MigrationBatch(
            [
                new MigrationRow("Sales.Orders /Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders ", ThroughputSource.Broker), day, 100), new Dictionary<string, object>()),
                new MigrationRow("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 250), new Dictionary<string, object>())
            ],
            "Sales.Orders/Broker");
        var checkpointToExtend = new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, batch.Cursor, 0, 0, null, null, null, null, null, null);

        var result = await Target.Write(category, batch, checkpointToExtend);

        var keyIgnoresTrailingSpace = (await QueryTarget(dbContext => Task.FromResult(dbContext.Database.ProviderName))).Contains("SqlServer");
        var count = await QueryTarget(dbContext => dbContext.LicensingEndpointThroughput
            .Where(row => row.NormalizedName == "sales.orders" && row.ThroughputSource == ThroughputSource.Broker && row.DateUtc == day)
            .Select(row => row.MessageCount)
            .SingleAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(keyIgnoresTrailingSpace ? 1 : 2), "SQL Server's key ignores the trailing space, so the two days are one row; PostgreSQL's does not, so they are two");
            Assert.That(result.AlreadyPresent, Is.EqualTo(keyIgnoresTrailingSpace ? 1 : 0), "the day SQL Server's key folds into the other is already present, not lost or failed");
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(await Target.Count(category), Is.EqualTo(result.Copied), "in a single batch, every row reported as copied is a row in the table");
            Assert.That(count, Is.EqualTo(250), "the later row sets the day's count where the key makes the two one, and never adds to the earlier row's");
        }
    }

    [Test]
    public async Task The_copied_counts_are_what_the_collector_reads_as_already_collected()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await SeedThroughputTimeSeries("Sales.Orders/Broker", new DateOnly(2026, 1, 1), 100);
        await SeedThroughputTimeSeries("Sales.Orders/Broker", new DateOnly(2026, 1, 2), 200);

        await CopyCategory("LicensingEndpoints");
        await CopyCategory("LicensingThroughput");

        var endpoint = await TargetLicensingDataStore.GetEndpoint(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker));

        Assert.That(endpoint!.LastCollectedDate, Is.EqualTo(new DateOnly(2026, 1, 2)),
            "LastCollectedDate is derived from MAX(DateUtc) over the copied rows, and it is the only thing stopping the collector re-collecting these two days and adding to them");
    }

    [Test]
    public async Task A_day_whose_endpoint_has_no_name_is_skipped_as_a_required_value_missing()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await CopyCategory("LicensingEndpoints");

        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingThroughput");
        var day = new DateOnly(2026, 1, 1);
        var batch = new MigrationBatch(
            [
                new MigrationRow("/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier(null!, ThroughputSource.Broker), day, 100), new Dictionary<string, object>()),
                new MigrationRow("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 250), new Dictionary<string, object>())
            ],
            "Sales.Orders/Broker");
        var checkpointToExtend = new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, batch.Cursor, 0, 0, null, null, null, null, null, null);

        var result = await Target.Write(category, batch, checkpointToExtend);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "/Broker|2026-01-01" }));
            Assert.That(result.SkipReasons![MigrationSkipReason.RequiredValueMissing], Is.EqualTo(1), "a nameless endpoint's day has no NormalizedName to key under, so it is skipped and counted rather than halting the category");
        }
    }
}
