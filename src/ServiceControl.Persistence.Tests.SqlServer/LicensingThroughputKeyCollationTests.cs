namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.DataMigration.Writers;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class LicensingThroughputKeyCollationTests : PersistenceTestBase
{
    [Test]
    public async Task Days_of_endpoints_an_accent_insensitive_key_treats_as_one_are_added_within_a_batch_and_across_batches()
    {
        await OpenAnAccentInsensitiveTarget();
        await LicensingDataStore.SaveEndpoint(new Endpoint("Café", ThroughputSource.Broker) { SanitizedName = "cafe" });
        var day = new DateOnly(2026, 1, 1);

        var first = await Write(
            new MigrationCheckpoint(MigrationCategoryIds.LicensingThroughput, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null),
            ("Café/Broker|2026-01-01", "Café", day, 100),
            ("Cafe/Broker|2026-01-01", "Cafe", day, 250));
        await Write(first.Saved, ("CAFE/Broker|2026-01-01", "CAFE", day, 50));

        long[] stored;
        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            stored = await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().LicensingEndpointThroughput.Select(row => row.MessageCount).ToArrayAsync();
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Copied, Is.EqualTo(1), "the column's collation makes the two names one day, which the statement writes once");
            Assert.That(first.AlreadyPresent, Is.EqualTo(1), "the day folded into the other is counted as already present and summed into the kept row");
            Assert.That(stored, Is.EqualTo(new[] { 400L }), "every count for the day is added under the key column's own collation, whichever batch it came in");
        }
    }

    [Test]
    public async Task Endpoints_an_accent_insensitive_key_treats_as_one_are_reported_as_a_merge()
    {
        await OpenAnAccentInsensitiveTarget();
        var batch = new MigrationBatch(
            [
                new MigrationRow("Café/Broker", new Endpoint("Café", ThroughputSource.Broker) { SanitizedName = "cafe" }, new Dictionary<string, object>()),
                new MigrationRow("Cafe/Broker", new Endpoint("Cafe", ThroughputSource.Broker) { SanitizedName = "cafe" }, new Dictionary<string, object>())
            ],
            "Cafe/Broker");

        var merges = await PreparedSkips.MergesFrom(ServiceProvider, new LicensingEndpointsWriter(ServiceProvider.GetRequiredService<IMigrationSqlDialect>()), batch);
        var result = await ServiceProvider.GetRequiredService<IMigrationTarget>().Write(
            MigrationCategoryRegistry.Find(MigrationCategoryIds.LicensingEndpoints)!,
            batch,
            new MigrationCheckpoint(MigrationCategoryIds.LicensingEndpoints, MigrationCategoryState.InProgress, null, 0, 0, null, null, null, null, null, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merges.Select(merge => merge.SourceId), Is.EqualTo(new[] { "Café/Broker" }), "the copy keeps the endpoint whose source id sorts first, and the column folds the accent, so the other is a merge");
            Assert.That(merges.Single().Detail, Does.Contain("kept 'cafe', 'Broker' and dropped the row for 'café', 'Broker'"));
            Assert.That((result.Copied, result.AlreadyPresent, result.Merged), Is.EqualTo((1, 1, 1)), "every row the insert drops on a first copy is a merge the log names");
        }
    }

    [TestCase("Café", "Cafe")]
    [TestCase("cafe", "ｃａｆｅ")]
    [TestCase("CAFE", "cafe")]
    [TestCase("cafe ", "cafe")]
    [TestCase("cafe", "cafes")]
    public async Task The_key_comparer_folds_exactly_what_the_columns_collation_folds(string first, string second)
    {
        await OpenAnAccentInsensitiveTarget();
        bool sqlServerFolds;

        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            sqlServerFolds = await scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>().Database
                .SqlQuery<int>($"SELECT CASE WHEN {first} = {second} COLLATE Latin1_General_CI_AI THEN 1 ELSE 0 END AS [Value]")
                .SingleAsync() == 1;
        }

        var comparer = ServiceProvider.GetRequiredService<IMigrationSqlDialect>().KeyComparer(typeof(LicensingEndpointEntity), nameof(LicensingEndpointEntity.NormalizedName));

        Assert.That(comparer.Equals(first, second), Is.EqualTo(sqlServerFolds), $"the merge lines are found with this comparer, so it must agree with SQL Server on '{first}' and '{second}'");
    }

    // Set on both tables, because the writer looks up each day's endpoint by name before it writes, and the test server's default respects accents.
    async Task OpenAnAccentInsensitiveTarget()
    {
        await using (var scope = ServiceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            var endpoints = TableOf<LicensingEndpointEntity>(dbContext);
            var throughput = TableOf<LicensingEndpointThroughputEntity>(dbContext);

            var ignoreAccents = $"""
                ALTER TABLE {throughput} DROP CONSTRAINT [FK_LicensingEndpointThroughput_LicensingEndpoints_NormalizedName_ThroughputSource];
                ALTER TABLE {throughput} DROP CONSTRAINT [PK_LicensingEndpointThroughput];
                ALTER TABLE {endpoints} DROP CONSTRAINT [PK_LicensingEndpoints];
                ALTER TABLE {endpoints} ALTER COLUMN [NormalizedName] nvarchar(300) COLLATE Latin1_General_CI_AI NOT NULL;
                ALTER TABLE {throughput} ALTER COLUMN [NormalizedName] nvarchar(300) COLLATE Latin1_General_CI_AI NOT NULL;
                ALTER TABLE {endpoints} ADD CONSTRAINT [PK_LicensingEndpoints] PRIMARY KEY ([NormalizedName], [ThroughputSource]);
                ALTER TABLE {throughput} ADD CONSTRAINT [PK_LicensingEndpointThroughput] PRIMARY KEY ([NormalizedName], [ThroughputSource], [DateUtc]);
                ALTER TABLE {throughput} ADD CONSTRAINT [FK_LicensingEndpointThroughput_LicensingEndpoints_NormalizedName_ThroughputSource]
                    FOREIGN KEY ([NormalizedName], [ThroughputSource]) REFERENCES {endpoints} ([NormalizedName], [ThroughputSource]) ON DELETE CASCADE;
                """;

            await dbContext.Database.ExecuteSqlRawAsync(ignoreAccents);
        }

        await ServiceProvider.GetRequiredService<IMigrationTarget>().Open();
    }

    Task<MigrationWriteResult> Write(MigrationCheckpoint checkpointToExtend, params (string SourceId, string Name, DateOnly Day, long Count)[] days)
    {
        var category = MigrationCategoryRegistry.All.Single(entry => entry.Id == MigrationCategoryIds.LicensingThroughput);
        var batch = new MigrationBatch(
            [.. days.Select(day => new MigrationRow(day.SourceId, new ThroughputDayRow(new EndpointIdentifier(day.Name, ThroughputSource.Broker), day.Day, day.Count), new Dictionary<string, object>()))],
            days[^1].SourceId);

        return ServiceProvider.GetRequiredService<IMigrationTarget>().Write(category, batch, checkpointToExtend);
    }

    // Named from the model, because the table sits in the configured schema when the persister has one.
    static string TableOf<TEntity>(ServiceControlDbContext dbContext)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity));
        return dbContext.GetService<ISqlGenerationHelper>().DelimitIdentifier(entityType.GetTableName(), entityType.GetSchema());
    }
}
