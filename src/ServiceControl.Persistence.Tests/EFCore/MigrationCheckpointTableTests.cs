namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

class MigrationCheckpointTableTests : PersistenceTestBase
{
    [Test]
    public async Task The_table_round_trips_every_column_written_directly_through_the_DbContext()
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            dbContext.MigrationCheckpoints.Add(new MigrationCheckpointEntity
            {
                CategoryId = "EndpointSettings",
                State = MigrationCategoryState.InProgress,
                Cursor = "cursor-1",
                CopiedCount = 3,
                SkippedCount = 1,
                SourceTotal = 40,
                SkipReasons = new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 1 },
                StartedAt = Now,
                LastProgressAt = Now,
                SettledAt = null,
                LastError = null,
                AlreadyPresentCount = 36,
                Version = 7
            });
            await dbContext.SaveChangesAsync();
        }

        using var readScope = ServiceProvider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var stored = await readContext.MigrationCheckpoints.AsNoTracking().SingleAsync(c => c.CategoryId == "EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.State, Is.EqualTo(MigrationCategoryState.InProgress));
            Assert.That(stored.Cursor, Is.EqualTo("cursor-1"));
            Assert.That(stored.CopiedCount, Is.EqualTo(3));
            Assert.That(stored.SkippedCount, Is.EqualTo(1));
            Assert.That(stored.SourceTotal, Is.EqualTo(40));
            Assert.That(stored.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 1 }));
            Assert.That(stored.AlreadyPresentCount, Is.EqualTo(36));
            Assert.That(stored.Version, Is.EqualTo(7));
            Assert.That(stored.StartedAt, Is.EqualTo(Now));
            Assert.That(stored.LastProgressAt, Is.EqualTo(Now));
            Assert.That(stored.SettledAt, Is.Null);
            Assert.That(stored.LastError, Is.Null);
        }
    }

    [Test]
    public async Task Every_timestamp_column_comes_back_as_a_Utc_DateTime()
    {
        Assert.That(Now.Kind, Is.EqualTo(DateTimeKind.Utc), "the engine stamps timings from TimeProvider.GetUtcNow().UtcDateTime, so the test clock has to match");

        using (var scope = ServiceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
            dbContext.MigrationCheckpoints.Add(new MigrationCheckpointEntity
            {
                CategoryId = "KnownEndpoints",
                State = MigrationCategoryState.Complete,
                Cursor = null,
                CopiedCount = 1,
                SkippedCount = 0,
                SourceTotal = 1,
                SkipReasons = null,
                StartedAt = Now,
                LastProgressAt = Now,
                SettledAt = Now,
                LastError = null
            });
            await dbContext.SaveChangesAsync();
        }

        using var readScope = ServiceProvider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var stored = await readContext.MigrationCheckpoints.AsNoTracking().SingleAsync(c => c.CategoryId == "KnownEndpoints");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.StartedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(stored.LastProgressAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(stored.SettledAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    [Test]
    public void The_skip_reasons_JSON_is_keyed_by_reason_name_not_by_its_number()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var converter = dbContext.Model.FindEntityType(typeof(MigrationCheckpointEntity))!.FindProperty("SkipReasons")!.GetValueConverter()!;

        var json = (string)converter.ConvertToProvider(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 2 })!;

        // Enum.Parse accepts "0" as readily as "BodyUnreadable", so a round trip cannot tell the two encodings apart.
        Assert.That(json, Does.Contain("BodyUnreadable"));
    }

    [Test]
    public void A_reason_name_this_build_does_not_know_is_read_as_Unknown_rather_than_throwing()
    {
        // The rows a newer build wrote are what a downgraded instance meets, and the read path that would
        // throw is ReadAll, which is what decides whether the host may start.
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var converter = dbContext.Model.FindEntityType(typeof(MigrationCheckpointEntity))!.FindProperty("SkipReasons")!.GetValueConverter()!;

        var reasons = (IReadOnlyDictionary<MigrationSkipReason, long>)converter.ConvertFromProvider(
            """{"BodyUnreadable":2,"SomethingFromTheFuture":3,"AlsoFromTheFuture":1}""")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reasons[MigrationSkipReason.BodyUnreadable], Is.EqualTo(2));
            Assert.That(reasons[MigrationSkipReason.Unknown], Is.EqualTo(4), "several unknown names collapse onto the one member, so their counts are summed rather than one of them winning");
            Assert.That(reasons, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void The_table_carries_exactly_the_columns_the_checkpoint_needs()
    {
        // The one thing the one-way door makes permanent. Adding, dropping or renaming one after this
        // ships costs a migration against customer data, so it fails here rather than in a customer's
        // database. Property names rather than column names, because each provider spells the columns
        // its own way and the spelling is a convention the generated migrations already pin.
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var entityType = dbContext.Model.FindEntityType(typeof(MigrationCheckpointEntity))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(entityType.GetProperties().Select(property => property.Name), Is.EquivalentTo(new[]
            {
                "CategoryId", "State", "Cursor", "CopiedCount", "SkippedCount", "SourceTotal",
                "SkipReasons", "StartedAt", "LastProgressAt", "SettledAt", "LastError", "AlreadyPresentCount", "Version"
            }));

            // Underscores stripped so one assertion covers MigrationCheckpoints and migration_checkpoints.
            Assert.That(entityType.GetTableName()!.Replace("_", ""), Is.EqualTo("MigrationCheckpoints").IgnoreCase);
        }
    }
}