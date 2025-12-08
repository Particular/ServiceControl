namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Sql.Core.Abstractions;
using ServiceControl.Persistence.Sql.MySQL;
using Testcontainers.MySql;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    MySqlContainer mySqlContainer;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        mySqlContainer = new MySqlBuilder()
            .WithImage("mysql:8.0")
            .WithDatabase("servicecontrol")
            .WithUsername("root")
            .WithPassword("mysql")
            .Build();

        await mySqlContainer.StartAsync();

        var connectionString = mySqlContainer.GetConnectionString();

        PersistenceSettings = new MySqlPersisterSettings
        {
            ConnectionString = connectionString,
            CommandTimeout = 30,
            MaintenanceMode = false
        };

        var persistence = new MySqlPersistenceConfiguration().Create(PersistenceSettings);
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
        if (mySqlContainer != null)
        {
            await mySqlContainer.StopAsync();
            await mySqlContainer.DisposeAsync();
        }
    }

    public PersistenceSettings PersistenceSettings { get; private set; }

    public void CompleteDatabaseOperation()
    {
        // No-op for SQL (no async indexing like RavenDB)
    }
}
