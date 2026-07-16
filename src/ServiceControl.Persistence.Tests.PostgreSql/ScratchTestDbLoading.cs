namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

public class ScratchTestDbLoading : PersistenceTestBase
{
    [Test]
    public async Task TestDbOperation()
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSqlServiceControlDbContext>();

        var result = await db.Database.ExecuteSqlAsync($"SELECT 1 as Hello");
        Assert.That(result, Is.EqualTo(1));
    }
}
