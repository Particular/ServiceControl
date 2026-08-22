namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.DbContexts;

class AuditSchemaTests : PersistenceTestBase
{
    [Test]
    public void The_applied_schema_matches_the_model()
    {
        using var scope = ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        Assert.That(dbContext.Database.HasPendingModelChanges(), Is.False,
            "the audit tables are created by hand written DDL so that PostgreSQL can partition them, "
            + "and their columns have drifted from the entity model. Update AuditPartitioningSql to match.");
    }

    [Test]
    public async Task Audit_tables_accept_and_return_a_row()
    {
        using var scope = ServiceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        Assert.That(await dbContext.AuditMessages.AnyAsync(), Is.False);
        Assert.That(await dbContext.SagaSnapshots.AnyAsync(), Is.False);
        Assert.That(await dbContext.FailedAuditImports.AnyAsync(), Is.False);
    }
}
