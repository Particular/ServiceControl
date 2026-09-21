namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class EndpointSettingsKeyCollationTests : PersistenceTestBase
{
    [Test]
    public async Task The_key_columns_own_collation_decides_a_merge_when_the_database_default_disagrees()
    {
        bool databaseIgnoresCase;

        using (var scope = ServiceProvider.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Database;

            databaseIgnoresCase = await database
                .SqlQuery<int>($"SELECT CONVERT(int, DATABASEPROPERTYEX(DB_NAME(), 'ComparisonStyle')) & 1 AS [Value]")
                .SingleAsync() == 1;

            // The column is given the opposite of the database default, because a test where the two agree cannot show which one decided.
            await database.ExecuteSqlRawAsync("""
                DECLARE @columnCollation sysname = IIF(CONVERT(int, DATABASEPROPERTYEX(DB_NAME(), 'ComparisonStyle')) & 1 = 1, N'Latin1_General_CS_AS', N'Latin1_General_CI_AS');
                ALTER TABLE [EndpointSettings] DROP CONSTRAINT [PK_EndpointSettings];
                EXEC (N'ALTER TABLE [EndpointSettings] ALTER COLUMN [Name] nvarchar(450) COLLATE ' + @columnCollation + N' NOT NULL');
                ALTER TABLE [EndpointSettings] ADD CONSTRAINT [PK_EndpointSettings] PRIMARY KEY ([Name]);
                """);
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
}
