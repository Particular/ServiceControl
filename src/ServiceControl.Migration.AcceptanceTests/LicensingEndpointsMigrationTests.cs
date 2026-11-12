namespace ServiceControl.Migration.AcceptanceTests;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
[NonParallelizable]
class LicensingEndpointsMigrationTests : MigrationAcceptanceTest
{
    [Test]
    public async Task Copies_an_endpoint_and_reads_it_back_through_the_products_own_API()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker)
        {
            SanitizedName = "Sales_Orders",
            UserIndicator = "Sales team",
            Scope = "Sales",
            EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()]
        });

        await CopyCategory("LicensingEndpoints");

        var fromTarget = await TargetLicensingDataStore.GetEndpoint("Sales.Orders", ThroughputSource.Broker);
        var reportableEndpoints = await TargetLicensingDataStore.GetAllEndpoints(includePlatformEndpoints: false);
        var row = await QueryTarget(dbContext => dbContext.LicensingEndpoints.AsNoTracking().SingleAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fromTarget, Is.Not.Null);
            Assert.That(fromTarget!.UserIndicator, Is.EqualTo("Sales team"));
            Assert.That(fromTarget.Scope, Is.EqualTo("Sales"));
            Assert.That(row.SanitizedName, Is.EqualTo("Sales_Orders"));
            Assert.That(row.NormalizedSanitizedName, Is.EqualTo("sales_orders"), "GetEndpointThroughputByQueueName matches on this column, so an unnormalised value hides a migrated endpoint's throughput from the licensing report");
            Assert.That(fromTarget.EndpointIndicators, Is.EqualTo(new[] { EndpointIndicator.PlatformEndpoint.ToString() }), "the indicators column holds what EF itself writes: JSON on SQL Server, a text[] on PostgreSQL");
            Assert.That(reportableEndpoints, Is.Empty, "the product filters platform endpoints inside the database, so an indicator value EF cannot query puts a platform endpoint in the licensing report");
        }
    }

    [Test]
    public async Task The_same_name_under_two_sources_is_two_rows_and_not_a_collision()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await SeedThroughputSource(new Endpoint("SALES.ORDERS", ThroughputSource.Audit) { SanitizedName = "sales.orders" });

        var result = await CopyCategory("LicensingEndpoints");

        Assert.That(result.Copied, Is.EqualTo(2), "the key is (NormalizedName, ThroughputSource), so the throughput source must survive into the key or the second endpoint is treated as a duplicate of the first");
        Assert.That(result.Skipped, Is.Zero);
    }

    [Test]
    public async Task An_endpoint_the_target_already_holds_is_already_present_rather_than_skipped()
    {
        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });

        await CopyCategory("LicensingEndpoints");
        await DeleteCheckpoint("LicensingEndpoints");
        var second = await CopyCategory("LicensingEndpoints");

        Assert.That(second.AlreadyPresent, Is.EqualTo(1));
        Assert.That(second.Skipped, Is.Zero, "an endpoint the target already holds was not lost, and counting it as a skip makes --migration-verify subtract it twice");
    }

    [Test]
    public async Task Two_names_differing_only_in_a_trailing_space_never_fail_the_batch()
    {
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingEndpoints");
        var batch = new MigrationBatch(
            [
                new MigrationRow("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" }, new Dictionary<string, object>()),
                new MigrationRow("Sales.Orders /Broker", new Endpoint("Sales.Orders ", ThroughputSource.Broker) { SanitizedName = "sales.orders" }, new Dictionary<string, object>())
            ],
            "Sales.Orders/Broker");
        var checkpointToExtend = new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, batch.Cursor, 0, 0, null, null, null, null, null, null);

        var result = await Target.Write(category, batch, checkpointToExtend);

        var keyIgnoresTrailingSpace = (await QueryTarget(dbContext => Task.FromResult(dbContext.Database.ProviderName))).Contains("SqlServer");
        var storedNames = await QueryTarget(dbContext => dbContext.LicensingEndpoints.AsNoTracking().Select(row => row.Name).ToListAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(keyIgnoresTrailingSpace ? 1 : 2), "SQL Server's key ignores the trailing space and keeps the first row; PostgreSQL's does not and keeps both");
            Assert.That(result.AlreadyPresent, Is.EqualTo(keyIgnoresTrailingSpace ? 1 : 0), "the row SQL Server's key treats as the first's is already present, not lost");
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(await Target.Count(category), Is.EqualTo(result.Copied), "every row reported as copied is a row in the table");

            if (keyIgnoresTrailingSpace)
            {
                Assert.That(storedNames, Is.EqualTo(new[] { "Sales.Orders " }), "the row whose id sorts first by ordinal order is the one a shared key keeps, whatever order the batch arrives in");
            }
        }
    }

    [Test]
    public async Task An_endpoint_with_no_name_is_skipped_as_a_required_value_missing()
    {
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == "LicensingEndpoints");
        var batch = new MigrationBatch(
            [
                new MigrationRow("/Broker", new Endpoint(new EndpointIdentifier(null!, ThroughputSource.Broker)) { SanitizedName = "nameless" }, new Dictionary<string, object>()),
                new MigrationRow("Sales.Orders/Broker", new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = null! }, new Dictionary<string, object>())
            ],
            "Sales.Orders/Broker");
        var checkpointToExtend = new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, batch.Cursor, 0, 0, null, null, null, null, null, null);

        var result = await Target.Write(category, batch, checkpointToExtend);
        var stored = await QueryTarget(dbContext => dbContext.LicensingEndpoints.AsNoTracking().SingleAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.SanitizedName, Is.Empty, "a null sanitized name takes the empty string the product itself stores");
            Assert.That(stored.NormalizedSanitizedName, Is.Empty);
            Assert.That(result.Copied, Is.EqualTo(1));
            Assert.That(result.SkippedIds, Is.EqualTo(new[] { "/Broker" }));
            Assert.That(result.SkipReasons![MigrationSkipReason.RequiredValueMissing], Is.EqualTo(1), "Name and NormalizedName are NOT NULL and the product has no value of its own for a nameless endpoint, so the row is skipped and counted rather than halting the category");
        }
    }
}
