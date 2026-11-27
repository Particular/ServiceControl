namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;

class SubscriptionRowMapperTests : PersistenceTestBase
{
    [Test]
    public async Task Sets_every_mapped_property()
    {
        await using var scope = ServiceProvider.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Model;

        var row = SubscriptionRowMapper.Map(new SubscriptionRow("MyMessages.OrderPlaced", new Version(1, 0, 0), "Billing@BILLING01", "Billing"));

        MigrationEntityCoverage.AssertEveryMappedPropertyIsSet(model, row);
    }
}
