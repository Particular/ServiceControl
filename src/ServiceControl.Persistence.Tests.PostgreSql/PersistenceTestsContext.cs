// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        PersistenceSettings = new PostgreSqlPersisterSettings
        {
            ConnectionString = await PostgreSqlSharedContainer.GetConnectionStringAsync()
        };

        var persistence = new PostgreSqlPersistenceConfiguration().Create(PersistenceSettings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);
    }

    public async Task PostSetup(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSqlServiceControlDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public Task TearDown() => Task.CompletedTask;

    public void CompleteDatabaseOperation() { }

    public PersistenceSettings PersistenceSettings { get; set; }

    public string GenerateFailedMessageRecordId(string messageId) => messageId;
}
