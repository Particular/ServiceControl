namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Sql.Core.Abstractions;
using ServiceControl.Persistence.Sql.PostgreSQL;
using Testcontainers.PostgreSql;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    PostgreSqlContainer postgreSqlContainer;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        postgreSqlContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("servicecontrol")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await postgreSqlContainer.StartAsync();

        var connectionString = postgreSqlContainer.GetConnectionString();

        PersistenceSettings = new PostgreSqlPersisterSettings
        {
            ConnectionString = connectionString,
            CommandTimeout = 30,
            MaintenanceMode = false
        };

        var persistence = new PostgreSqlPersistenceConfiguration().Create(PersistenceSettings);
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
        if (postgreSqlContainer != null)
        {
            await postgreSqlContainer.StopAsync();
            await postgreSqlContainer.DisposeAsync();
        }
    }

    public PersistenceSettings PersistenceSettings { get; private set; }

    public void CompleteDatabaseOperation()
    {
        // No-op for SQL (no async indexing like RavenDB)
    }
}
