namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

public class DbMigrationsTests
{
    [Test]
    public async Task ApplyMigrations_runs_successfully()
    {
        var ctx = new PersistenceTestsContext();
        var hostBuilder = new HostApplicationBuilder();
        await ctx.Setup(hostBuilder);
        var host = hostBuilder.Build();
        var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>().ApplyMigrations();
    }
}