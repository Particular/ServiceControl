namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

#pragma warning disable CA2007

public class ScratchTestDbLoading : PersistenceTestBase
{
    [Test]
    public async Task TestDbOperation()
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqlServerServiceControlDbContext>();

        var result = await db.Database.ExecuteSqlAsync($"SELECT 1 as Hello");
        Assert.That(result, Is.EqualTo(1));
    }
}