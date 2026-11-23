namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class LicensingEndpointsMigrationTargetTests : PersistenceTestBase
{
    [SetUp]
    public Task OpenTarget() => Target.Open();

    IMigrationTarget Target => ServiceProvider.GetRequiredService<IMigrationTarget>();

    static readonly MigrationCategory LicensingEndpointsCategory = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.LicensingEndpoints);

    [Test]
    public async Task Count_is_the_licensing_endpoint_rows_the_target_holds()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Audit) { SanitizedName = "sales.orders" });
        await LicensingDataStore.RecordEndpointThroughput("Sales.Orders", ThroughputSource.Broker, new DateOnly(2026, 1, 1), 5);
        await LicensingDataStore.RecordEndpointThroughput("Sales.Orders", ThroughputSource.Broker, new DateOnly(2026, 1, 2), 5);
        await LicensingDataStore.RecordEndpointThroughput("Sales.Orders", ThroughputSource.Audit, new DateOnly(2026, 1, 1), 5);

        Assert.That(await Target.Count(LicensingEndpointsCategory), Is.EqualTo(2), "one row per name and source, and not the three throughput days that hang off them");
    }

    [Test]
    public async Task A_licensing_endpoint_with_a_value_longer_than_its_column_is_skipped_as_value_too_long_and_the_rest_copy()
    {
        var fitsExactly = new Endpoint(new string('n', 300), ThroughputSource.Broker) { SanitizedName = new string('s', 450) };
        var longName = new Endpoint(new string('n', 301), ThroughputSource.Audit) { SanitizedName = "sales.orders" };
        var longSanitizedName = new Endpoint("Billing", ThroughputSource.Broker) { SanitizedName = new string('s', 451) };
        var batch = BatchOf(("Endpoint/1", fitsExactly), ("Endpoint/2", longName), ("Endpoint/3", longSanitizedName));

        var details = await PreparedSkips.DetailsFrom(ServiceProvider, new LicensingEndpointsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await Target.Write(LicensingEndpointsCategory, batch, CheckpointAfter("Endpoint/3"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(1), "a value exactly as long as its column fits");
            Assert.That(result.SkippedRows.Select(row => row.SourceId), Is.EqualTo(new[] { "Endpoint/2", "Endpoint/3" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 2 }), "an over-long value would fail the whole insert and halt the category");
            Assert.That((await LicensingDataStore.GetAllEndpoints(includePlatformEndpoints: true)).Select(endpoint => endpoint.Id.Name), Is.EqualTo(new[] { new string('n', 300) }));
            Assert.That(details, Has.Count.EqualTo(2));
            Assert.That(details[0], Does.EndWith("takes at most 300 characters and the value holds 301, on document 'Endpoint/2'"));
            Assert.That(details[1], Does.EndWith("takes at most 450 characters and the value holds 451, on document 'Endpoint/3'"));
        }
    }

    [Test]
    public async Task Two_names_differing_only_in_a_trailing_space_in_one_batch_are_reported_as_one_merge_where_the_key_folds_them()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(
            ("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" }),
            ("Sales.Orders /Broker", new Endpoint("Sales.Orders ", ThroughputSource.Broker) { SanitizedName = "sales.orders" }));
        var keyIgnoresTrailingSpace = KeyIgnoresTrailingSpace(dialect);

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new LicensingEndpointsWriter(dialect), batch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(keyIgnoresTrailingSpace ? new[] { "Sales.Orders/Broker" } : Array.Empty<string>()),
                "the row whose id sorts first is kept, and the other is reported only where the key folds a trailing space");

            if (keyIgnoresTrailingSpace)
            {
                Assert.That(merges[0].Detail, Does.Contain("kept 'sales.orders ', 'Broker' and dropped the row for 'sales.orders', 'Broker'"), "the line names both endpoints and the one kept");
            }
        }
    }

    [Test]
    public async Task A_name_differing_only_in_a_trailing_space_from_a_stored_endpoint_is_reported_as_a_merge_where_the_key_folds_them()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var first = await Target.Write(LicensingEndpointsCategory, BatchOf(("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" })), CheckpointAfter("Sales.Orders/Broker"));
        var second = BatchOf(("Sales.Orders /Broker", new Endpoint("Sales.Orders ", ThroughputSource.Broker) { SanitizedName = "sales.orders" }));
        var keyIgnoresTrailingSpace = KeyIgnoresTrailingSpace(dialect);

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new LicensingEndpointsWriter(dialect), second);
        var result = await Target.Write(LicensingEndpointsCategory, second, first.Saved with { Cursor = second.Cursor });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(keyIgnoresTrailingSpace ? new[] { "Sales.Orders /Broker" } : Array.Empty<string>()));

            if (keyIgnoresTrailingSpace)
            {
                Assert.That(merges[0].Detail, Does.Contain("kept 'sales.orders', 'Broker' and dropped the row for 'sales.orders ', 'Broker'"), "the stored endpoint is the one kept");
            }

            Assert.That(result.AlreadyPresent, Is.EqualTo(keyIgnoresTrailingSpace ? 1 : 0), "the folded endpoint is counted as already present");
        }
    }

    [Test]
    public async Task An_exact_replay_of_a_stored_endpoint_is_already_present_and_not_a_merge()
    {
        var batch = BatchOf(("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" }));
        var first = await Target.Write(LicensingEndpointsCategory, batch, CheckpointAfter("Sales.Orders/Broker"));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new LicensingEndpointsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var again = await Target.Write(LicensingEndpointsCategory, batch, first.Saved);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges, Is.Empty, "a retry re-reads the rows an earlier pass copied, and the stored row is that same endpoint");
            Assert.That((again.Copied, again.AlreadyPresent, again.Merged), Is.EqualTo((0, 1, 0)));
        }
    }

    [Test]
    public async Task Two_names_differing_only_in_case_are_reported_as_one_merge_on_every_provider()
    {
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var batch = BatchOf(
            ("SALES.ORDERS/Broker", new Endpoint("SALES.ORDERS", ThroughputSource.Broker) { SanitizedName = "sales.orders" }),
            ("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" }));

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new LicensingEndpointsWriter(dialect), batch);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(new[] { "Sales.Orders/Broker" }), "the product lower-cases the name, so both providers store one row");
            Assert.That(merges[0].Detail, Does.Contain("kept 'sales.orders', 'Broker' and dropped the row for 'sales.orders', 'Broker'"));
        }
    }

    static bool KeyIgnoresTrailingSpace(IMigrationSqlDialect dialect) =>
        dialect.KeyComparer(typeof(LicensingEndpointEntity), nameof(LicensingEndpointEntity.NormalizedName)).Equals("a", "a ");

    static MigrationBatch BatchOf(params (string Id, object Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.LicensingEndpoints, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
