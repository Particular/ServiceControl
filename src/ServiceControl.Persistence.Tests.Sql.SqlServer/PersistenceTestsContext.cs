namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Sql.Core.Abstractions;
using ServiceControl.Persistence.Sql.SqlServer;
using Testcontainers.MsSql;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    MsSqlContainer sqlServerContainer;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        sqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong@Passw0rd")
            .Build();

        await sqlServerContainer.StartAsync();

        var connectionString = sqlServerContainer.GetConnectionString();

        PersistenceSettings = new SqlServerPersisterSettings
        {
            ConnectionString = connectionString,
            CommandTimeout = 30,
            MaintenanceMode = false
        };

        var persistence = new SqlServerPersistenceConfiguration().Create(PersistenceSettings);
        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);
    }

    public async Task PostSetup(IHost host)
    {
        // Apply migrations
        var migrator = host.Services.GetRequiredService<IDatabaseMigrator>();
        await migrator.ApplyMigrations();
    }

    public async Task TearDown()
    {
        if (sqlServerContainer != null)
        {
            await sqlServerContainer.StopAsync();
            await sqlServerContainer.DisposeAsync();
        }
    }

    public PersistenceSettings PersistenceSettings { get; private set; }

    public void CompleteDatabaseOperation()
    {
        // No-op for SQL (no async indexing like RavenDB)
    }
}
