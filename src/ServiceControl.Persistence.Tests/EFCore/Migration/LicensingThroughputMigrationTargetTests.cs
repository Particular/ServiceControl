namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class LicensingThroughputMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory LicensingThroughputCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.LicensingThroughput);

    [Test]
    public async Task Count_is_the_daily_throughput_rows_the_target_holds()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await LicensingDataStore.SaveEndpoint(new Endpoint("Billing", ThroughputSource.Broker) { SanitizedName = "billing" });
        await LicensingDataStore.RecordEndpointThroughput("Sales.Orders", ThroughputSource.Broker, new DateOnly(2026, 1, 1), 5);
        await LicensingDataStore.RecordEndpointThroughput("Sales.Orders", ThroughputSource.Broker, new DateOnly(2026, 1, 2), 5);
        await LicensingDataStore.RecordEndpointThroughput("Billing", ThroughputSource.Broker, new DateOnly(2026, 1, 1), 5);

        Assert.That(await Target.Count(LicensingThroughputCategory), Is.EqualTo(3), "one row per endpoint and day, and not the two endpoint rows they hang off");
    }

    [Test]
    public async Task A_day_whose_endpoint_name_is_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var day = new DateOnly(2026, 1, 1);
        var batch = BatchOf(
            ("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 100)),
            ("long/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier(new string('n', 301), ThroughputSource.Broker), day, 250)));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new LicensingThroughputWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("long/Broker|2026-01-01"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedRows.Select(row => row.SourceId), Is.EqualTo(new[] { "long/Broker|2026-01-01" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 1 }), "the target cannot hold an endpoint with this name, so the day must be skipped here rather than fail the whole batch at the endpoint check");
            Assert.That(details, Is.EqualTo(new[]
            {
                $"{PreparedSkips.TableAndColumnOf<LicensingEndpointThroughputEntity>(ServiceProvider, nameof(LicensingEndpointThroughputEntity.NormalizedName))} takes at most 300 characters and the value holds 301, on document 'long/Broker|2026-01-01'"
            }));
        }
    }

    [Test]
    public async Task A_day_repeated_in_one_batch_keeps_its_last_row_and_counts_the_other_as_already_present()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var day = new DateOnly(2026, 1, 1);
        var batch = BatchOf(
            ("Sales.Orders/Broker|2026-01-01|b", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 250)),
            ("Sales.Orders/Broker|2026-01-01|a", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 100)));

        var result = await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("Sales.Orders/Broker|2026-01-01|b"));
        await using var scope = ServiceProvider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().LicensingEndpointThroughput.Select(row => row.MessageCount).SingleAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1), "the repeat is the same day of the same endpoint, so it is dropped before the statement and counted");
            Assert.That(stored, Is.EqualTo(250), "the row whose id sorts last by ordinal order sets the day rather than adding to it, whatever order the batch arrives in");
        }
    }

    [Test]
    public async Task Days_of_two_endpoints_the_key_treats_as_one_are_added_when_they_arrive_in_one_batch()
    {
        var endpoints = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.LicensingEndpoints);
        var endpointBatch = BatchOf(
            ("Sales.Orders /Broker", new Endpoint("Sales.Orders ", ThroughputSource.Broker) { SanitizedName = "sales.orders" }),
            ("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" }));
        await Target.Write(endpoints, endpointBatch, new MigrationCheckpoint(endpoints.Id, MigrationCategoryState.InProgress, endpointBatch.Cursor, 0, 0, null, null, null, null, null, null));
        var day = new DateOnly(2026, 1, 1);
        var batch = BatchOf(
            ("Sales.Orders /Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders ", ThroughputSource.Broker), day, 100)),
            ("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 250)));

        var first = await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("Sales.Orders/Broker"));

        await using var scope = ServiceProvider.CreateAsyncScope();
        var keyIgnoresTrailingSpace = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Database.ProviderName!.Contains("SqlServer");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Copied, Is.EqualTo(keyIgnoresTrailingSpace ? 1 : 2));
            Assert.That(first.AlreadyPresent, Is.EqualTo(keyIgnoresTrailingSpace ? 1 : 0), "the folded endpoint's days are summed into the kept row, and the endpoint writer reports the fold");
            Assert.That(await StoredCounts(), Is.EqualTo(keyIgnoresTrailingSpace ? new[] { 350L } : new[] { 100L, 250L }), "SQL Server's key makes the two one day, which holds both counts, as the product's own throughput write would");
        }
    }

    [Test]
    public async Task Days_of_two_endpoints_differing_only_in_case_are_added_when_they_arrive_in_one_batch()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var day = new DateOnly(2026, 1, 1);
        var batch = BatchOf(
            ("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 100)),
            ("SALES.ORDERS/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("SALES.ORDERS", ThroughputSource.Broker), day, 250)));

        var result = await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("SALES.ORDERS/Broker"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.AlreadyPresent, Is.EqualTo(1), "the folded endpoint's days are summed into the kept row, and the endpoint writer reports the fold");
            Assert.That(await StoredCounts(), Is.EqualTo(new[] { 350L }), "two source endpoints store the same lower-cased name, so their days are added, not one kept as a repeat of the other");
        }
    }

    [Test]
    public async Task A_later_batch_adds_its_count_to_the_day_an_earlier_batch_stored_for_a_name_differing_only_in_case()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var day = new DateOnly(2026, 1, 1);

        var first = await Target.Write(LicensingThroughputCategory, BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 100))), CheckpointAfter("Sales.Orders/Broker"));
        var second = await Target.Write(LicensingThroughputCategory, BatchOf(("SALES.ORDERS/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("SALES.ORDERS", ThroughputSource.Broker), day, 250))), first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Copied, Is.EqualTo(1), "an update is a row the statement wrote");
            Assert.That(await StoredCounts(), Is.EqualTo(new[] { 350L }), "both endpoints store the same lower-cased name, so the second batch adds its count to the day rather than replacing it");
        }
    }

    [Test]
    public async Task A_batch_written_again_from_the_checkpoint_it_already_extended_is_refused_and_adds_nothing()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var batch = BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), new DateOnly(2026, 1, 1), 100)));
        var checkpointToExtend = CheckpointAfter("Sales.Orders/Broker");

        await Target.Write(LicensingThroughputCategory, batch, checkpointToExtend);

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => Target.Write(LicensingThroughputCategory, batch, checkpointToExtend));
        Assert.That(await StoredCounts(), Is.EqualTo(new[] { 100L }), "the add and the checkpoint commit together, so a replay of a committed batch rolls back instead of counting the day twice");
    }

    [Test]
    public async Task A_retry_deletes_the_copied_days_so_copying_again_gives_the_same_totals()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var batch = BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), new DateOnly(2026, 1, 1), 100)));
        var first = await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("Sales.Orders/Broker"));

        var reset = await Target.ResetForRetry(ResetForRetry(first.Saved));
        var afterReset = await StoredCounts();
        await Target.Write(LicensingThroughputCategory, batch, reset);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(afterReset, Is.Empty, "the reset deletes the days the first copy added, because copying them again adds each count a second time");
            Assert.That(await StoredCounts(), Is.EqualTo(new[] { 100L }), "copying again after the retry gives the totals one copy gave, not double");
        }
    }

    [Test]
    public async Task A_stale_retry_reset_deletes_no_copied_day()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var batch = BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), new DateOnly(2026, 1, 1), 100)));
        var stale = (await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("Sales.Orders/Broker"))).Saved;
        await ServiceProvider.GetRequiredService<IMigrationCheckpointStore>().Upsert(stale);

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => Target.ResetForRetry(ResetForRetry(stale)));
        Assert.That(await StoredCounts(), Is.EqualTo(new[] { 100L }), "the delete shares the reset's transaction, so a refused reset leaves every copied day");
    }

    [Test]
    public async Task A_retry_reset_after_ServiceControl_opened_on_the_target_is_refused_and_deletes_nothing()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var batch = BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), new DateOnly(2026, 1, 1), 100)));
        var copied = (await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("Sales.Orders/Broker"))).Saved;
        await ServiceProvider.GetRequiredService<IMigrationTargetReadiness>().RecordHostOpened();

        var refused = Assert.ThrowsAsync<InvalidOperationException>(() => Target.ResetForRetry(ResetForRetry(copied)));
        var stored = await ServiceProvider.GetRequiredService<IMigrationCheckpointStore>().Read(MigrationCategoryIds.LicensingThroughput);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused.Message, Does.Contain(MigrationCategoryIds.LicensingThroughput).And.Contain("only possible before ServiceControl first opens on this database").And.Contain("--start-data-migration --abandon=LicensingThroughput"));
            Assert.That(await StoredCounts(), Is.EqualTo(new[] { 100L }), "the days may now include counts the running instance collected, so deleting them would lose live throughput");
            Assert.That((stored!.Version, stored.State, stored.Cursor, stored.CopiedCount), Is.EqualTo((copied.Version, copied.State, copied.Cursor, copied.CopiedCount)), "a refused reset saves nothing");
        }
    }

    [Test]
    public async Task A_retry_reset_that_deletes_nothing_still_succeeds_after_ServiceControl_opened_on_the_target()
    {
        var checkpoints = ServiceProvider.GetRequiredService<IMigrationCheckpointStore>();
        var stopped = await checkpoints.Upsert(new MigrationCheckpoint(MigrationCategoryIds.ArchivedAndResolvedFailedMessages, MigrationCategoryState.InProgress, "FailedMessages/1", 1, 0, null, null, null, null, null, "RavenDB is unreachable"));
        await ServiceProvider.GetRequiredService<IMigrationTargetReadiness>().RecordHostOpened();

        var reset = await Target.ResetForRetry(ResetForRetry(stopped));

        Assert.That((reset.State, reset.Cursor, reset.LastError), Is.EqualTo((MigrationCategoryState.NotStarted, (string)null, (string)null)), "an optional category copies after ServiceControl opens, and its reset deletes nothing, so it is still allowed");
    }

    [Test]
    public async Task One_endpoint_with_more_days_than_one_statement_takes_stores_every_day_with_its_own_count()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        int dayCount;

        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            dayCount = dialect.RowsPerStatementFor<LicensingEndpointThroughputEntity>(scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>()) + 1;
        }

        var firstDay = new DateOnly(2026, 1, 1);
        var days = Enumerable.Range(0, dayCount).Select(offset => (Date: firstDay.AddDays(offset), Count: 1000L + offset)).ToArray();
        var batch = BatchOf([.. days.Select(day => ($"Sales.Orders/Broker|{day.Date:yyyy-MM-dd}", (object)new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day.Date, day.Count)))]);

        var result = await Target.Write(LicensingThroughputCategory, batch, CheckpointAfter("Sales.Orders/Broker"));

        await using var readScope = ServiceProvider.CreateAsyncScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().LicensingEndpointThroughput
            .OrderBy(row => row.DateUtc)
            .Select(row => new { row.DateUtc, row.MessageCount })
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(dayCount), "the batch runs as two statements, and Copied adds up what both wrote");
            Assert.That(result.AlreadyPresent, Is.Zero, "a day the second statement wrote must not be counted as already present");
            Assert.That(stored.Select(row => (row.DateUtc, row.MessageCount)), Is.EqualTo(days), "every day is stored with its own count, including the day past the first statement");
        }
    }

    [Test]
    public async Task A_later_batch_adds_to_a_day_stored_under_a_name_that_differs_only_by_a_trailing_space()
    {
        var endpoints = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.LicensingEndpoints);
        var endpointBatch = BatchOf(
            ("Sales.Orders /Broker", new Endpoint("Sales.Orders ", ThroughputSource.Broker) { SanitizedName = "sales.orders" }),
            ("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" }));
        await Target.Write(endpoints, endpointBatch, new MigrationCheckpoint(endpoints.Id, MigrationCategoryState.InProgress, endpointBatch.Cursor, 0, 0, null, null, null, null, null, null));
        var day = new DateOnly(2026, 1, 1);

        var first = await Target.Write(LicensingThroughputCategory, BatchOf(("Sales.Orders /Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders ", ThroughputSource.Broker), day, 100))), CheckpointAfter("Sales.Orders /Broker"));
        var second = await Target.Write(LicensingThroughputCategory, BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 250))), first.Saved);

        await using var scope = ServiceProvider.CreateAsyncScope();
        var keyIgnoresTrailingSpace = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Database.ProviderName!.Contains("SqlServer");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Copied, Is.EqualTo(1), "the second batch's day is matched back to the row that produced it, not refused because the stored name keeps its trailing space");
            Assert.That(await StoredCounts(), Is.EqualTo(keyIgnoresTrailingSpace ? new[] { 350L } : new[] { 100L, 250L }), "SQL Server's key makes the two names one day, which the later count adds to; PostgreSQL's keeps two");
        }
    }

    async Task<long[]> StoredCounts()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().LicensingEndpointThroughput.Select(row => row.MessageCount).OrderBy(count => count).ToArrayAsync();
    }

    static MigrationBatch BatchOf(params (string Id, object Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    // The reset a retry makes.
    static MigrationCheckpoint ResetForRetry(MigrationCheckpoint checkpoint) => checkpoint with
    {
        State = MigrationCategoryState.NotStarted,
        Cursor = null,
        CopiedCount = 0,
        SkippedCount = 0,
        AlreadyPresentCount = 0,
        SkipReasons = null,
        SettledAt = null,
        LastError = null
    };

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.LicensingThroughput, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
