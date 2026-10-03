#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.UnitTests.Migration.Fakes;

[TestFixture]
class CheckpointMigrationStateTests
{
    [Test]
    public async Task An_instance_that_has_never_migrated_has_no_store_and_nothing_incomplete()
    {
        var state = new CheckpointMigrationState();

        await state.Seed(["EndpointSettings"], CancellationToken.None);

        Assert.That(state.AnyCategoryIncomplete, Is.False);
    }

    [Test]
    public async Task Selected_categories_that_all_finished_leave_the_guard_off()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("KnownEndpoints", MigrationCategoryState.Complete));
        await store.Upsert(Checkpoint("EndpointSettings", MigrationCategoryState.CompleteWithErrors));

        var state = new CheckpointMigrationState(store);
        await state.Seed(["KnownEndpoints", "EndpointSettings"], CancellationToken.None);

        Assert.That(state.AnyCategoryIncomplete, Is.False);
    }

    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.NotStarted)]
    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.Blocked)]
    public async Task One_selected_category_short_of_finished_holds_the_guard_on(MigrationCategoryState unfinished)
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("KnownEndpoints", MigrationCategoryState.Complete));
        await store.Upsert(Checkpoint("EndpointSettings", unfinished));

        var state = new CheckpointMigrationState(store);
        await state.Seed(["KnownEndpoints", "EndpointSettings"], CancellationToken.None);

        Assert.That(state.AnyCategoryIncomplete, Is.True);
    }

    [Test]
    public async Task A_selected_category_with_no_row_at_all_has_not_started()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(Checkpoint("KnownEndpoints", MigrationCategoryState.Complete));

        var state = new CheckpointMigrationState(store);
        await state.Seed(["KnownEndpoints", "EndpointSettings"], CancellationToken.None);

        Assert.That(state.AnyCategoryIncomplete, Is.True);
    }

    [Test]
    public async Task An_unselected_category_that_never_ran_does_not_hold_the_guard_on_forever()
    {
        var store = new InMemoryMigrationCheckpointStore();
        await store.Upsert(new MigrationCheckpoint("EndpointSettings", MigrationCategoryState.Complete, null, 3, 0, 3, null, null, null, null, null));
        await store.Upsert(new MigrationCheckpoint("EventLog", MigrationCategoryState.NotStarted, null, 0, 0, null, null, null, null, null, null));

        var state = new CheckpointMigrationState(store);
        await state.Seed(["EndpointSettings"], CancellationToken.None);

        Assert.That(state.AnyCategoryIncomplete, Is.False);
    }

    [Test]
    public void Exactly_complete_complete_with_errors_and_abandoned_are_finished() =>
        Assert.That(
            Enum.GetValues<MigrationCategoryState>().Where(state => state.IsFinished()),
            Is.EquivalentTo(new[] { MigrationCategoryState.Complete, MigrationCategoryState.CompleteWithErrors, MigrationCategoryState.Abandoned }));

    [Test]
    public void Exactly_EndpointNotKnown_is_benign_and_every_other_skip_reason_counts_as_a_loss() =>
        Assert.That(
            Enum.GetValues<MigrationSkipReason>().Where(reason => reason.IsBenign()),
            Is.EquivalentTo(new[] { MigrationSkipReason.EndpointNotKnown }),
            "a benign reason is exempt from the halt threshold, so any number of rows lost to one settles the category as complete");

    static MigrationCheckpoint Checkpoint(string categoryId, MigrationCategoryState state) =>
        new(categoryId, state, null, 0, 0, null, null, null, null, null, null);
}
