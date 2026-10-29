namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class MigrationInsertDateKeyTests : PersistenceTestBase
{
    [Test]
    public async Task A_row_whose_key_holds_a_date_is_matched_back_to_the_row_that_produced_it()
    {
        await LicensingDataStore.SaveEndpoint(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        var dialect = ServiceProvider.GetRequiredService<IMigrationSqlDialect>();
        var day = new LicensingEndpointThroughputEntity { NormalizedName = "sales.orders", ThroughputSource = ThroughputSource.Broker, DateUtc = new DateOnly(2026, 1, 1), MessageCount = 100 };

        await using var scope = ServiceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        await dialect.Open(dbContext);

        var inserted = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var rows = await dialect.InsertMissing(dbContext, [day]);
            await transaction.CommitAsync();
            return rows;
        });

        Assert.That(inserted, Is.EqualTo(new[] { day }), "a date key comes back from the database as DateTime unless it is read as the type its mapping stores, and the exact lookup then refuses the row");
    }
}
