namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

class EFMigrationCheckpointStoreTests : PersistenceTestBase
{
    IMigrationCheckpointStore Store => ServiceProvider.GetRequiredService<IMigrationCheckpointStore>();

    [Test]
    public async Task Read_returns_null_for_a_category_that_has_never_run()
    {
        Assert.That(await Store.Read("EndpointSettings"), Is.Null);
    }

    [Test]
    public async Task Upsert_then_Read_round_trips_every_field()
    {
        var checkpoint = new MigrationCheckpoint(
            "EndpointSettings", MigrationCategoryState.CompleteWithErrors, "cursor-1", 5, 2, 40,
            new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 2 }, Now, Now, Now, "body storage unavailable", AlreadyPresentCount: 33);

        var saved = await Store.Upsert(checkpoint);
        var stored = await Store.Read("EndpointSettings");

        // Record equality compares SkipReasons by reference, and a round-tripped dictionary is a new instance.
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored! with { SkipReasons = null, Version = 0 }, Is.EqualTo(checkpoint with { SkipReasons = null }));
            Assert.That(stored.SkipReasons, Is.EquivalentTo(checkpoint.SkipReasons));
            Assert.That(stored.Version, Is.EqualTo(1), "the first save leaves the row at version 1");
            // The engine saves from what Upsert hands back, so a returned version that is not the stored one
            // makes the next save conflict against a row nothing else touched.
            Assert.That(saved.Version, Is.EqualTo(stored.Version));
        }
    }

    [Test]
    public async Task A_second_Upsert_for_the_same_category_updates_rather_than_duplicates()
    {
        await Store.Upsert(new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.InProgress, "cursor-1", 5, 0, 40, null, Now, Now, null, null));
        var saved = await Store.Upsert(new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.Complete, "cursor-2", 9, 1, 40, null, Now, Now, Now, null, Version: 1));

        var all = await Store.ReadAll();
        var stored = await Store.Read("EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(all, Has.Count.EqualTo(1));
            Assert.That(saved.Version, Is.EqualTo(stored!.Version), "an update hands back the version it landed on, not the one it was saved from");
            Assert.That(stored.State, Is.EqualTo(MigrationCategoryState.Complete));
            Assert.That(stored.Cursor, Is.EqualTo("cursor-2"));
            Assert.That(stored.CopiedCount, Is.EqualTo(9));
            Assert.That(stored.SettledAt, Is.EqualTo(Now));
        }
    }

    [Test]
    public async Task A_checkpoint_saved_as_Abandoned_stores_its_counts_its_reasons_and_when_it_was_abandoned()
    {
        var reasons = new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 400 };
        var halted = Now.AddMinutes(-5);
        await Store.Upsert(new MigrationCheckpoint("GroupComments", MigrationCategoryState.Halted, "g-1", 12, 400, 412, reasons, Now, Now, halted, "body storage unavailable"));
        await Store.Upsert(new MigrationCheckpoint("GroupComments", MigrationCategoryState.Abandoned, "g-1", 12, 400, 412, reasons, Now, Now, Now, "body storage unavailable", Version: 1));

        var stored = await Store.Read("GroupComments");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored!.State, Is.EqualTo(MigrationCategoryState.Abandoned));
            // State says which terminal state was reached and SettledAt says when, so abandoning a halted
            // category moves the one timestamp on rather than filling a second column beside it.
            Assert.That(stored.SettledAt, Is.EqualTo(Now));
            Assert.That(stored.SettledAt, Is.Not.EqualTo(halted));
            Assert.That(stored.SkippedCount, Is.EqualTo(400));
            Assert.That(stored.SkipReasons, Is.EquivalentTo(new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 400 }));
            Assert.That(stored.LastError, Is.EqualTo("body storage unavailable"));
        }
    }

    [Test]
    public async Task A_restart_after_a_halt_clears_the_fields_the_previous_save_set()
    {
        await Store.Upsert(new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.Halted, "cursor-1", 5, 2, 40,
            new Dictionary<MigrationSkipReason, long> { [MigrationSkipReason.BodyUnreadable] = 2 }, Now, Now, Now, "body storage unavailable"));

        await Store.Upsert(new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.InProgress, "cursor-1", 5, 2, 40,
            null, Now, Now, null, null, Version: 1));

        var stored = await Store.Read("EndpointSettings");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stored!.SettledAt, Is.Null, "a resumed category must not look finished");
            Assert.That(stored.LastError, Is.Null);
            Assert.That(stored.SkipReasons, Is.Null);
        }
    }

    [Test]
    public void An_upsert_for_a_category_that_has_never_run_is_refused_when_it_carries_a_version()
    {
        var conflict = Assert.ThrowsAsync<MigrationCheckpointConflictException>(() => Store.Upsert(
            new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.InProgress, "cursor-1", 1, 0, 40, null, Now, Now, null, null, Version: 1)));

        Assert.That(conflict!.Message, Does.Contain("EndpointSettings"));
    }

    [Test]
    public async Task ReadAll_returns_every_category_that_has_run()
    {
        await Store.Upsert(new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.Complete, "e-1", 1, 0, 1, null, Now, Now, Now, null));
        await Store.Upsert(new MigrationCheckpoint("KnownEndpoints", MigrationCategoryState.InProgress, "k-1", 4, 0, 9, null, Now, Now, null, null));

        var all = await Store.ReadAll();

        Assert.That(all.Select(c => c.CategoryId), Is.EquivalentTo(new[] { "EndpointSettings", "KnownEndpoints" }));
    }
}