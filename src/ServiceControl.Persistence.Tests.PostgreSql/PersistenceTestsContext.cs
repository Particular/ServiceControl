// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using EFCore.PostgreSql;
using EFCore.PostgreSql.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using ServiceControl.Persistence.EFCore.DbContexts;

public class PersistenceTestsContext : IPersistenceTestsContext
{
    IHost host;
    string databaseName;

    public async Task Setup(IHostApplicationBuilder hostBuilder)
    {
        databaseName = $"sc_test_{Guid.NewGuid():n}";

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(await PostgreSqlSharedContainer.GetConnectionStringAsync())
        {
            Database = databaseName
        };

        PersistenceSettings = new PostgreSqlPersisterSettings
        {
            ConnectionString = connectionStringBuilder.ConnectionString
        };

        var persistence = new PostgreSqlPersistenceConfiguration().Create(PersistenceSettings);

        persistence.AddPersistence(hostBuilder.Services);
        persistence.AddInstaller(hostBuilder.Services);
    }

    public async Task PostSetup(IHost host)
    {
        this.host = host;

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PostgreSqlServiceControlDbContext>();
        await db.Database.MigrateAsync();
    }

    // Dropped via a separate admin connection: EnsureDeletedAsync would run DROP DATABASE on the
    // same connection that's using it, which Postgres always rejects (error 55006).
    public async Task TearDown()
    {
        await using var connection = new NpgsqlConnection(await PostgreSqlSharedContainer.GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    // Reconcile the insert-only tables so that ingested data is visible to the data stores,
    // without waiting for the reconciler background services' timers
    public void CompleteDatabaseOperation() => DrainInsertOnlyTables().GetAwaiter().GetResult();

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
