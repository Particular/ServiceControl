// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using EFCore.SqlServer;
using EFCore.SqlServer.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.EFCore.DbContexts;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    IHost host;
    string databaseName;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        databaseName = $"sc_test_{Guid.NewGuid():n}";

        var connectionStringBuilder = new SqlConnectionStringBuilder(await SqlServerSharedContainer.GetConnectionStringAsync())
        {
            InitialCatalog = databaseName
        };

        PersistenceSettings = new SqlServerPersisterSettings
        {
            ConnectionString = connectionStringBuilder.ConnectionString
        };

        var persistence = new SqlServerPersistenceConfiguration().Create(PersistenceSettings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);
    }

    public async Task PostSetup(IHost host)
    {
        this.host = host;

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SqlServerServiceControlDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    // Dropped via a separate admin connection to master: SQL Server refuses to drop a database
    // that the issuing connection (or any other session) is still attached to, so we force
    // everyone off with SINGLE_USER ROLLBACK IMMEDIATE first.
    public async Task TearDown()
    {
        await using var connection = new SqlConnection(await SqlServerSharedContainer.GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID('{databaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{databaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();
    }

    // Reconcile the insert-only tables so that ingested data is visible to the data stores,
    // without waiting for the reconciler background services' timers
    public Task CompleteDatabaseOperation() => DrainInsertOnlyTables();

    async Task DrainInsertOnlyTables()
    {
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        while (await dbContext.KnownEndpointsInsertOnly.AnyAsync())
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync();
                await KnownEndpointsReconciler.ReconcileBatch(dbContext, batchSize: 1000, CancellationToken.None);
                await transaction.CommitAsync();
            });
        }
    }

    public PersistenceSettings PersistenceSettings { get; set; }

    public string GenerateFailedMessageRecordId(string messageId) => messageId;
}