namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Entities;

class MigrationCheckpointTransactionTests : PersistenceTestBase
{
    static MigrationCheckpoint Checkpoint(DateTime now, string cursor, long copied, long version = 0) =>
        new("EndpointSettings", MigrationCategoryState.InProgress, cursor, copied, 0, 40, null, now, now, null, null, Version: version);

    async Task SeedCheckpoint(string cursor, long copied)
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        await dbContext.UpsertCheckpoint(Checkpoint(Now, cursor, copied));
    }

    [Test]
    public async Task A_failure_before_commit_leaves_neither_the_row_nor_the_checkpoint()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() => strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            dbContext.Settings.Add(new SettingEntity { Key = "migration-test-row", Value = "should-not-survive" });
            await dbContext.SaveChangesAsync();

            await dbContext.UpsertCheckpoint(Checkpoint(Now, "cursor-1", 1));

            throw new InvalidOperationException("simulated failure just before commit");
        }));

        // Without this the test also passes when UpsertCheckpoint throws before writing anything.
        Assert.That(thrown!.Message, Is.EqualTo("simulated failure just before commit"));

        using var verifyScope = ServiceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await verifyContext.Settings.AnyAsync(s => s.Key == "migration-test-row"), Is.False);
            Assert.That(await verifyContext.MigrationCheckpoints.AnyAsync(c => c.CategoryId == "EndpointSettings"), Is.False);
        }
    }

    [Test]
    public async Task A_successful_commit_lands_the_row_and_the_checkpoint_together()
    {
        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            dbContext.Settings.Add(new SettingEntity { Key = "migration-test-row", Value = "should-survive" });
            await dbContext.SaveChangesAsync();

            await dbContext.UpsertCheckpoint(Checkpoint(Now, "cursor-1", 1));

            await transaction.CommitAsync();
        });

        using var verifyScope = ServiceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await verifyContext.Settings.AnyAsync(s => s.Key == "migration-test-row"), Is.True);
            Assert.That(await verifyContext.MigrationCheckpoints.AnyAsync(c => c.CategoryId == "EndpointSettings"), Is.True);
        }
    }

    [Test]
    public async Task An_existing_checkpoint_is_advanced_by_a_commit_inside_the_callers_transaction()
    {
        await SeedCheckpoint("cursor-1", 1);

        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            dbContext.Settings.Add(new SettingEntity { Key = "migration-test-row", Value = "should-survive" });
            await dbContext.SaveChangesAsync();

            await dbContext.UpsertCheckpoint(Checkpoint(Now, "cursor-2", 2, version: 1));

            await transaction.CommitAsync();
        });

        using var verifyScope = ServiceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var stored = await verifyContext.MigrationCheckpoints.AsNoTracking().SingleAsync(c => c.CategoryId == "EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await verifyContext.Settings.AnyAsync(s => s.Key == "migration-test-row"), Is.True);
            Assert.That(stored.Cursor, Is.EqualTo("cursor-2"));
            Assert.That(stored.CopiedCount, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task An_existing_checkpoint_keeps_its_previous_cursor_when_the_transaction_rolls_back()
    {
        await SeedCheckpoint("cursor-1", 1);

        using var scope = ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var strategy = dbContext.Database.CreateExecutionStrategy();

        var thrown = Assert.ThrowsAsync<InvalidOperationException>(() => strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();

            dbContext.Settings.Add(new SettingEntity { Key = "migration-test-row", Value = "should-not-survive" });
            await dbContext.SaveChangesAsync();

            await dbContext.UpsertCheckpoint(Checkpoint(Now, "cursor-2", 2, version: 1));

            throw new InvalidOperationException("simulated failure just before commit");
        }));

        // Without this the test also passes when UpsertCheckpoint throws before writing anything.
        Assert.That(thrown!.Message, Is.EqualTo("simulated failure just before commit"));

        using var verifyScope = ServiceProvider.CreateScope();
        var verifyContext = verifyScope.ServiceProvider.GetRequiredService<ServiceControlDbContext>();
        var stored = await verifyContext.MigrationCheckpoints.AsNoTracking().SingleAsync(c => c.CategoryId == "EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await verifyContext.Settings.AnyAsync(s => s.Key == "migration-test-row"), Is.False);
            Assert.That(stored.Cursor, Is.EqualTo("cursor-1"));
            Assert.That(stored.CopiedCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task A_save_built_from_an_out_of_date_copy_is_refused_rather_than_silently_winning()
    {
        var store = ServiceProvider.GetRequiredService<IMigrationCheckpointStore>();
        await store.Upsert(Checkpoint(Now, "cursor-1", 1));

        // Both read the row at the same version, as a background copy and a required copy on two hosts would.
        var first = (await store.Read("EndpointSettings"))!;
        var stale = (await store.Read("EndpointSettings"))!;

        await store.Upsert(first with { Cursor = "cursor-first", CopiedCount = 2 });

        Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => store.Upsert(stale with { Cursor = "cursor-stale", CopiedCount = 99 }));

        var stored = (await store.Read("EndpointSettings"))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored.Cursor, Is.EqualTo("cursor-first"));
            Assert.That(stored.CopiedCount, Is.EqualTo(2));
            Assert.That(stored.Version, Is.EqualTo(first.Version + 1));
        }
    }
}