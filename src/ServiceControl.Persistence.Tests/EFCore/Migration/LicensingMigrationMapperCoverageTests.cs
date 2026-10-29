namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class LicensingMigrationMapperCoverageTests : PersistenceTestBase
{
    [Test]
    public async Task The_endpoint_mapper_sets_every_mapped_property()
    {
        var endpoint = new Endpoint("Sales.Orders", ThroughputSource.Audit)
        {
            SanitizedName = "Sales.Orders",
            EndpointIndicators = [EndpointIndicator.PlatformEndpoint.ToString()],
            UserIndicator = "Sales team",
            Scope = "Sales"
        };

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(await Model(), LicensingEndpointRowMapper.Map(endpoint));
    }

    [Test]
    public async Task The_throughput_mapper_sets_every_mapped_property()
    {
        var day = new ThroughputDayRow(new EndpointIdentifier("Sales.Orders", ThroughputSource.Audit), new DateOnly(2026, 1, 1), 100);

        var row = LicensingThroughputRowMapper.Map(day);

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(await Model(), row);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.NormalizedName, Is.EqualTo("sales.orders"), "the key must be the name the endpoint row is stored under, or the foreign key refuses the day");
            Assert.That(row.ThroughputSource, Is.EqualTo(ThroughputSource.Audit));
            Assert.That(row.DateUtc, Is.EqualTo(new DateOnly(2026, 1, 1)));
            Assert.That(row.MessageCount, Is.EqualTo(100));
        }
    }

    async Task<IModel> Model()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        return scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;
    }
}
