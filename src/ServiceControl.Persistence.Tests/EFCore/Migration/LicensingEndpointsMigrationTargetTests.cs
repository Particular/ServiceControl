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
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "Endpoint/2", "Endpoint/3" }));
            Assert.That(result.SkipReasons, Is.EqualTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.ValueTooLong] = 2 }), "an over-long value would fail the whole insert and halt the category");
            Assert.That((await LicensingDataStore.GetAllEndpoints(includePlatformEndpoints: true)).Select(endpoint => endpoint.Id.Name), Is.EqualTo(new[] { new string('n', 300) }));
            Assert.That(details, Has.Count.EqualTo(2));
            Assert.That(details[0], Does.EndWith("takes at most 300 characters and the value holds 301, on document 'Endpoint/2'"));
            Assert.That(details[1], Does.EndWith("takes at most 450 characters and the value holds 451, on document 'Endpoint/3'"));
        }
    }

    static MigrationBatch BatchOf(params (string Id, object Document)[] rows) =>
        new([.. rows.Select(row => new MigrationRow(row.Id, row.Document, new Dictionary<string, object>()))], rows[^1].Id);

    static MigrationCheckpoint CheckpointAfter(string cursor) =>
        new(MigrationCategoryIds.LicensingEndpoints, MigrationCategoryState.InProgress, cursor, 0, 0, null, null, null, null, null, null);
}
