namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

class KnownEndpointSourceTests : RavenMigrationSourceTestBase
{
    [Test]
    public async Task Reads_known_endpoints_by_the_plural_prefix_in_batches_with_their_monitored_flag()
    {
        using (var session = await SessionProvider.OpenSession())
        {
            foreach (var (name, monitored) in new[] { ("Sales.Orders", true), ("Billing", false), ("Shipping", false) })
            {
                var endpoint = new KnownEndpoint
                {
                    EndpointDetails = new EndpointDetails { Name = name, HostId = Guid.NewGuid(), Host = "HOST01" },
                    HostDisplayName = "HOST01",
                    Monitored = monitored
                };

                await session.StoreAsync(endpoint, $"KnownEndpoints/{endpoint.EndpointDetails.GetDeterministicId()}");
            }

            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.KnownEndpoints);

        var batches = await CollectBatches(source, category, batchSize: 2);

        Assert.That(batches.Select(batch => batch.Rows.Count), Is.EqualTo(new[] { 2, 1 }));
        Assert.That(batches.SelectMany(batch => batch.Rows).Count(row => ((KnownEndpoint)row.Document).Monitored), Is.EqualTo(1));
    }

    // KnownEndpointsWriter skips these as RequiredValueMissing, which it can only do if the reader hands them over despite the required members.
    [Test]
    public async Task Reads_a_known_endpoint_missing_a_required_value_with_that_value_null()
    {
        using (var session = await SessionProvider.OpenSession())
        {
            await session.StoreAsync(new { EndpointDetails = new { HostId = Guid.NewGuid(), Host = "HOST01" }, Monitored = false }, "KnownEndpoints/no-name");
            await session.StoreAsync(new { EndpointDetails = new { Name = "Sales.Orders", HostId = Guid.NewGuid() }, Monitored = false }, "KnownEndpoints/no-host");
            await session.StoreAsync(new { Monitored = false }, "KnownEndpoints/no-details");
            await session.SaveChangesAsync();
        }

        await using var source = await OpenMigrationSource();
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.KnownEndpoints);

        var endpoints = (await CollectBatches(source, category)).SelectMany(batch => batch.Rows).ToDictionary(row => row.SourceId, row => (KnownEndpoint)row.Document);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(endpoints["KnownEndpoints/no-name"].EndpointDetails.Name, Is.Null);
            Assert.That(endpoints["KnownEndpoints/no-host"].EndpointDetails.Host, Is.Null);
            Assert.That(endpoints["KnownEndpoints/no-details"].EndpointDetails, Is.Null);
        }
    }
}
