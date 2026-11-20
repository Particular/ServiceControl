namespace ServiceControl.Persistence.Tests;

using System;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Contracts.CustomChecks;
using ServiceControl.Operations;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class CustomCheckRowMapperTests : PersistenceTestBase
{
    [Test]
    public void Sets_every_mapped_property_of_a_custom_check()
    {
        var entity = CustomCheckRowMapper.Map(new CustomCheckDetail
        {
            OriginatingEndpoint = new EndpointDetails { Name = "Sales", Host = "MACHINE", HostId = Guid.NewGuid() },
            CustomCheckId = "Sales.HealthCheck",
            Category = "Ping",
            ReportedAt = new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            // Passing, because Status.Fail is the enum's zero and would read as unset.
            HasFailed = false,
            FailureReason = "recovered after a timeout"
        });

        using var scope = ServiceProvider.CreateScope();
        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model, entity);
    }
}
