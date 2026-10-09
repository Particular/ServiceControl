namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class EndpointSettingsKeyCollationTests : PersistenceTestBase
{
    readonly TargetWarnings targetWarnings = new();

    public EndpointSettingsKeyCollationTests() => RegisterServices = services => services.AddSingleton<ILoggerProvider>(targetWarnings);

    [Test]
    public async Task A_case_merge_inside_one_batch_logs_both_names_and_the_one_kept()
    {
        await OpenACaseInsensitiveTarget("Sales", "sales");

        await Write(("EndpointSettings/1", "Sales"), ("EndpointSettings/2", "sales"));

        Assert.That(targetWarnings.Messages.Count(message => message.Contains("'sales'") && message.Contains("kept 'Sales'")), Is.EqualTo(1), string.Join(Environment.NewLine, targetWarnings.Messages));
    }

    [Test]
    public async Task A_case_merge_with_a_row_an_earlier_batch_wrote_is_logged_and_a_row_written_again_is_not()
    {
        await OpenACaseInsensitiveTarget("Sales", "sales");

        await Write(("EndpointSettings/1", "Sales"));
        await Write(("EndpointSettings/1", "Sales"), ("EndpointSettings/2", "sales"));

        Assert.That(targetWarnings.Messages, Has.Exactly(1).Contains("kept 'Sales'"), string.Join(Environment.NewLine, targetWarnings.Messages));
    }

    async Task OpenACaseInsensitiveTarget(params string[] knownEndpointNames)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var entityType = dbContext.Model.FindEntityType(typeof(EndpointSettingsEntity));
            var table = dbContext.GetService<ISqlGenerationHelper>().DelimitIdentifier(entityType.GetTableName(), entityType.GetSchema());

            // Set on the column, because the merge follows the column's collation and the test server's default can be either.
            var ignoreCase = $"""
                ALTER TABLE {table} DROP CONSTRAINT [PK_EndpointSettings];
                ALTER TABLE {table} ALTER COLUMN [Name] nvarchar(450) COLLATE Latin1_General_CI_AS NOT NULL;
                ALTER TABLE {table} ADD CONSTRAINT [PK_EndpointSettings] PRIMARY KEY ([Name]);
                """;

            await dbContext.Database.ExecuteSqlRawAsync(ignoreCase);
        }

        foreach (var name in knownEndpointNames)
        {
            await MonitoringDataStore.CreateIfNotExists(new EndpointDetails { Name = name, HostId = Guid.NewGuid(), Host = "HOST01" });
        }

        await ServiceProvider.GetRequiredService<IMigrationTarget>().Open();
    }

    async Task Write(params (string SourceId, string Name)[] rows)
    {
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.EndpointSettings);
        var batch = new MigrationBatch(
            [.. rows.Select(row => new MigrationRow(row.SourceId, new EndpointSettings { Name = row.Name, TrackInstances = true }, new Dictionary<string, object>()))],
            rows[^1].SourceId);
        var checkpoint = await ServiceProvider.GetRequiredService<IMigrationCheckpointStore>().Read(category.Id)
            ?? new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null);

        await ServiceProvider.GetRequiredService<IMigrationTarget>().Write(category, batch, checkpoint);
    }

    [Test]
    public async Task The_key_columns_own_collation_decides_a_merge_when_the_database_default_disagrees()
    {
        bool databaseIgnoresCase;

        using (var scope = ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var database = dbContext.Database;

            databaseIgnoresCase = await database
                .SqlQuery<int>($"SELECT CONVERT(int, DATABASEPROPERTYEX(DB_NAME(), 'ComparisonStyle')) & 1 AS [Value]")
                .SingleAsync() == 1;

            // Named from the model, because the table sits in the configured schema when the persister has one.
            var entityType = dbContext.Model.FindEntityType(typeof(EndpointSettingsEntity));
            var table = dbContext.GetService<ISqlGenerationHelper>().DelimitIdentifier(entityType.GetTableName(), entityType.GetSchema());

            // The column is given the opposite of the database default, because a test where the two agree cannot show which one decided.
            var recollate = $"""
                DECLARE @columnCollation sysname = IIF(CONVERT(int, DATABASEPROPERTYEX(DB_NAME(), 'ComparisonStyle')) & 1 = 1, N'Latin1_General_CS_AS', N'Latin1_General_CI_AS');
                ALTER TABLE {table} DROP CONSTRAINT [PK_EndpointSettings];
                EXEC (N'ALTER TABLE {table} ALTER COLUMN [Name] nvarchar(450) COLLATE ' + @columnCollation + N' NOT NULL');
                ALTER TABLE {table} ADD CONSTRAINT [PK_EndpointSettings] PRIMARY KEY ([Name]);
                """;

            await database.ExecuteSqlRawAsync(recollate);
        }

        foreach (var name in new[] { "Sales", "sales" })
        {
            await MonitoringDataStore.CreateIfNotExists(new EndpointDetails { Name = name, HostId = Guid.NewGuid(), Host = "HOST01" });
        }

        var target = ServiceProvider.GetRequiredService<IMigrationTarget>();
        await target.Open();

        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.EndpointSettings);
        var batch = new MigrationBatch(
            [
                new MigrationRow("EndpointSettings/1", new EndpointSettings { Name = "Sales", TrackInstances = true }, new Dictionary<string, object>()),
                new MigrationRow("EndpointSettings/2", new EndpointSettings { Name = "sales", TrackInstances = false }, new Dictionary<string, object>())
            ],
            "EndpointSettings/2");
        var checkpointToExtend = new MigrationCheckpoint(category.Id, MigrationCategoryState.InProgress, batch.Cursor, 0, 0, null, null, null, null, null, null);

        var result = await target.Write(category, batch, checkpointToExtend);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Copied, Is.EqualTo(databaseIgnoresCase ? 2 : 1), "the column's own collation decides: two keys where it respects case, one where it ignores case, whatever the database default says");
            Assert.That(
                ServiceProvider.GetRequiredService<IMigrationSqlDialect>().KeyComparer(typeof(EndpointSettingsEntity), nameof(EndpointSettingsEntity.Name)).Equals("Sales", "sales"),
                Is.EqualTo(!databaseIgnoresCase),
                "the dry run predicts merges with this comparer, so it follows the same column the statement does");
        }
    }

    // RecordingLoggerProvider lives in ServiceControl.Infrastructure.Tests, which this project does not reference.
    sealed class TargetWarnings : ILoggerProvider
    {
        readonly System.Collections.Concurrent.ConcurrentQueue<string> messages = new();

        public IReadOnlyList<string> Messages => [.. messages];

        public ILogger CreateLogger(string categoryName) =>
            categoryName == typeof(EFCoreMigrationTarget).FullName ? new WarningLogger(messages) : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
        }

        sealed class WarningLogger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (logLevel == LogLevel.Warning)
                {
                    messages.Enqueue(formatter(state, exception));
                }
            }
        }
    }
}
