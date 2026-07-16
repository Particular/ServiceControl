// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    public Task Setup(IHostApplicationBuilder hostBuilder)
    {
        PersistenceSettings = new SqlServerPersisterSettings()
        {
            ConnectionString = "Server=localhost;Database=ServiceControl;User=sa;Password=Password1!;TrustServerCertificate=True"
        };

        var persistence = new SqlServerPersistenceConfiguration().Create(PersistenceSettings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);
        return Task.CompletedTask;
    }

    public async Task PostSetup(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqlServerServiceControlDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public Task TearDown() => Task.CompletedTask;

    public void CompleteDatabaseOperation() { }

    public PersistenceSettings PersistenceSettings { get; set; }

    public string GenerateFailedMessageRecordId(string messageId) => messageId;
}