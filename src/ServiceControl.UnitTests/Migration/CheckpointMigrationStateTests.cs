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
        await store.Upsert(Checkpoint("EndpointSettings", MigrationCategoryState.Complete));

        var state = new CheckpointMigrationState(store);
        await state.Seed(["KnownEndpoints", "EndpointSettings"], CancellationToken.None);

        Assert.That(state.AnyCategoryIncomplete, Is.False);
    }

    [TestCase(MigrationCategoryState.InProgress)]
    [TestCase(MigrationCategoryState.NotStarted)]
    [TestCase(MigrationCategoryState.Halted)]
    [TestCase(MigrationCategoryState.Blocked)]
    [TestCase(MigrationCategoryState.CompleteWithErrors)]
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
    public void Exactly_complete_and_abandoned_are_finished() =>
        Assert.That(
            Enum.GetValues<MigrationCategoryState>().Where(state => state.IsFinished()),
            Is.EquivalentTo(new[] { MigrationCategoryState.Complete, MigrationCategoryState.Abandoned }));

    [Test]
    public void Exactly_halted_and_complete_with_errors_are_failed() =>
        Assert.That(
            Enum.GetValues<MigrationCategoryState>().Where(state => state.IsFailed()),
            Is.EquivalentTo(new[] { MigrationCategoryState.Halted, MigrationCategoryState.CompleteWithErrors }));

    [Test]
    public void Exactly_the_two_harmless_reasons_are_harmless_and_every_other_skip_reason_is_a_fault() =>
        Assert.That(
            Enum.GetValues<MigrationSkipReason>().Where(reason => reason.IsBenign()),
            Is.EquivalentTo(new[] { MigrationSkipReason.PastRetention, MigrationSkipReason.BlankGroupComment }),
            "a harmless reason is exempt from the halt threshold and the settle rule, so any number of rows lost to one settles the category Done");

    [Test]
    public void Unknown_stays_the_last_skip_reason() =>
        Assert.That(Enum.GetValues<MigrationSkipReason>().Last(), Is.EqualTo(MigrationSkipReason.Unknown));

    [Test]
    public void Exactly_the_reasons_no_retry_can_fix_are_permanent() =>
        Assert.That(
            Enum.GetValues<MigrationSkipReason>().Where(reason => reason.IsPermanent()),
            Is.EquivalentTo(new[] { MigrationSkipReason.RequiredValueMissing }));

    static MigrationCheckpoint Checkpoint(string categoryId, MigrationCategoryState state) =>
        new(categoryId, state, null, 0, 0, null, null, null, null, null, null);
}
