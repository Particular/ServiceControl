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
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "long/Broker|2026-01-01" }));
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
            Assert.That(result.AlreadyPresent, Is.EqualTo(1), "PostgreSQL refuses a statement that updates one row twice, so the repeat is dropped before it and counted rather than failing the batch");
            Assert.That(stored, Is.EqualTo(250), "the row whose id sorts last by ordinal order sets the day, whatever order the batch arrives in");
        }
    }

    [Test]
    public async Task A_later_batch_replaces_the_count_an_earlier_batch_stored_for_the_same_day()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var day = new DateOnly(2026, 1, 1);

        var first = await Target.Write(LicensingThroughputCategory, BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 100))), CheckpointAfter("Sales.Orders/Broker"));
        var second = await Target.Write(LicensingThroughputCategory, BatchOf(("Sales.Orders/Broker|2026-01-01", new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Broker), day, 250))), first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.Copied, Is.EqualTo(1), "an update is a row the statement wrote");
            Assert.That(await StoredCounts(), Is.EqualTo(new[] { 250L }), "the count RavenDB holds now replaces the stored one, and is never added to it or ignored");
        }
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
    public async Task A_later_batch_sets_a_day_stored_under_a_name_that_differs_only_by_a_trailing_space()
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
            Assert.That(await StoredCounts(), Is.EqualTo(keyIgnoresTrailingSpace ? new[] { 250L } : new[] { 100L, 250L }), "SQL Server's key makes the two names one day, which the later count sets; PostgreSQL's keeps two");
        }
    }

    async Task<long[]> StoredCounts()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().LicensingEndpointThroughput.Select(row => row.MessageCount).OrderBy(count => count).ToArrayAsync();
    }

    static MigrationBatch BatchOf(params (string Id, object Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.LicensingThroughput, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
