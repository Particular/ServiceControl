// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using EFCore.SqlServer;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

// EF Core makes this check itself during Migrate, but only when no schema is configured: a
// configured schema moves every table and so has to suppress it. Every test configures a schema,
// so without this the suites would no longer notice a model change that has no migration.
[TestFixture]
class PendingModelChangesTests
{
    [Test]
    public void The_model_matches_the_migrations()
    {
        using var dbContext = new SqlServerServiceControlDbContextFactory().CreateDbContext([]);

        Assert.That(dbContext.Database.HasPendingModelChanges(), Is.False,
            "The model has changed and no migration matches it. Run 'dotnet ef migrations add <name>' in ServiceControl.Persistence.EFCore.SqlServer.");
    }
}
