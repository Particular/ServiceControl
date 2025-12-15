namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Sql.Core.Abstractions;
using ServiceControl.Persistence.Sql.Core.DbContexts;
using ServiceControl.Persistence.Sql.SqlServer;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    IHost host;

    public Task Setup(IHostApplicationBuilder hostBuilder)
    {
        PersistenceSettings = new SqlServerPersisterSettings
        {
            ConnectionString = ManageDatabaseServer.ConnectionString,
            CommandTimeout = 30,
            MaintenanceMode = false
        };

        var persistence = new SqlServerPersistenceConfiguration().Create(PersistenceSettings);
        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);

        return Task.CompletedTask;
    }

    public async Task PostSetup(IHost host)
    {
        // Apply migrations
        var migrator = host.Services.GetRequiredService<IDatabaseMigrator>();
        await migrator.ApplyMigrations();
        this.host = host;
    }

    public async Task TearDown()
    {
        if (host != null)
        {
            using var scope = host.Services.CreateScope();
            using var context = scope.ServiceProvider.GetService<ServiceControlDbContextBase>();

            var tableNames = context.Model.GetEntityTypes()
                                        .Select(t => t.GetTableName())
                                        .Distinct()
                                        .ToList();

            foreach (var tableName in tableNames)
            {
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
                _ = await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE [{tableName}];", CancellationToken.None);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
            }
        }
    }

    public PersistenceSettings PersistenceSettings { get; private set; }

    public void CompleteDatabaseOperation()
    {
        // No-op for SQL (no async indexing like RavenDB)
    }
}
