// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using EFCore.SqlServer;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

[TestFixture]
class MigrationsHistoryTableTests
{
    [Test]
    public void The_history_table_is_named_for_the_error_instance()
    {
        using var dbContext = new SqlServerServiceControlDbContextFactory().CreateDbContext([]);

        var createScript = dbContext.GetService<IHistoryRepository>().GetCreateScript();

        Assert.That(createScript, Does.Contain("__ErrorMigrationsHistory"));
    }
}
