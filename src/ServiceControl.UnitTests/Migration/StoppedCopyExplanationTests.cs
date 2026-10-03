#nullable enable
namespace ServiceControl.UnitTests.Migration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Migration;
using ServiceControl.Persistence.DataMigration;

// The copy can stop three ways that are not the engine's to report: the stall watchdog cancelling it, the
// checkpoint store going down with the stall, and a second instance writing to the same database.
[TestFixture]
class StoppedCopyExplanationTests
{
    [Test]
    public async Task A_copy_the_stall_watchdog_stopped_comes_back_with_what_the_store_holds()
    {
        var store = Holding(InProgress(MigrationCategoryIds.KnownEndpoints), Finished(MigrationCategoryIds.EndpointSettings));

        var finished = await MigrationStartup.CopyOrExplainWhyItStopped(
            CancelledCopy,
            () => MigrationCategoryIds.KnownEndpoints,
            store,
            Attempted(MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings),
            NewSettings());

        Assert.That(finished.Select(checkpoint => checkpoint.CategoryId).ToArray(), Is.EquivalentTo(new[] { MigrationCategoryIds.KnownEndpoints, MigrationCategoryIds.EndpointSettings }),
            "the refusal names what is outstanding from these, so a stall that lets the cancellation out instead tells the operator only that something stopped");
    }

    [Test]
    public async Task A_category_this_run_never_attempted_is_left_out_of_what_comes_back()
    {
        var store = Holding(InProgress(MigrationCategoryIds.KnownEndpoints), InProgress(MigrationCategoryIds.EndpointSettings));

        var finished = await MigrationStartup.CopyOrExplainWhyItStopped(
            CancelledCopy,
            () => MigrationCategoryIds.KnownEndpoints,
            store,
            Attempted(MigrationCategoryIds.KnownEndpoints),
            NewSettings());

        Assert.That(finished.Select(checkpoint => checkpoint.CategoryId).ToArray(), Is.EqualTo(new[] { MigrationCategoryIds.KnownEndpoints }),
            "a row an earlier run left in progress would otherwise keep this host closed over a category it never tried to copy");
    }

    [Test]
    public void A_cancellation_with_no_stall_behind_it_stays_a_cancellation()
    {
        var store = Holding(InProgress(MigrationCategoryIds.KnownEndpoints));

        Assert.That(async () => await MigrationStartup.CopyOrExplainWhyItStopped(CancelledCopy, NoStall, store, Attempted(MigrationCategoryIds.KnownEndpoints), NewSettings()),
            Throws.InstanceOf<OperationCanceledException>(),
            "only the watchdog turns a cancellation into a refusal, and it did not fire here");

        Assert.That(store.Reads, Is.Zero, "reading the store means the recovery ran for a stall that never happened");
    }

    [Test]
    public void A_host_shutting_down_while_a_category_is_stalled_stays_a_cancellation()
    {
        using var shuttingDown = new CancellationTokenSource();
        shuttingDown.Cancel();
        var store = Holding(InProgress(MigrationCategoryIds.KnownEndpoints));

        Assert.That(async () => await MigrationStartup.CopyOrExplainWhyItStopped(
                CancelledCopy,
                () => MigrationCategoryIds.KnownEndpoints,
                store,
                Attempted(MigrationCategoryIds.KnownEndpoints),
                NewSettings(),
                shuttingDown.Token),
            Throws.InstanceOf<OperationCanceledException>(),
            "a shutdown is not a failure, so it must not come out as a refusal telling the operator to go looking at the source and the target");
    }

    [Test]
    public void A_stall_the_store_cannot_be_read_after_reports_the_stall_first_and_the_store_second()
    {
        var unreachable = new TimeoutException("checkpoint store unreachable");

        var exception = Assert.ThrowsAsync<Exception>(async () => await MigrationStartup.CopyOrExplainWhyItStopped(
            CancelledCopy,
            () => MigrationCategoryIds.KnownEndpoints,
            Failing(unreachable),
            Attempted(MigrationCategoryIds.KnownEndpoints),
            NewSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain($"committed nothing for {MigrationStartup.ClosedWindowProgress.StallLimit.TotalMinutes:0.#} minutes"),
                "the stall is why the copy stopped, and a store error reported in its place sends the operator after the wrong thing");
            Assert.That(exception.Message, Does.Contain("the checkpoint store is unreachable as well: checkpoint store unreachable"),
                "without the store's own words there is nothing to act on");
            Assert.That(exception.Message, Does.Contain("Nothing has opened on SQLServer yet"), "every other refusal says how to go back, and this one is the worst to be stuck in");
            Assert.That(exception.InnerException, Is.SameAs(unreachable));
        });
    }

    [Test]
    public void A_shutdown_that_lands_during_the_read_back_is_not_reported_as_an_unreachable_store()
    {
        using var shuttingDown = new CancellationTokenSource();
        var store = Failing(new OperationCanceledException(), shuttingDown.Cancel);

        Assert.That(async () => await MigrationStartup.CopyOrExplainWhyItStopped(
                CancelledCopy,
                () => MigrationCategoryIds.KnownEndpoints,
                store,
                Attempted(MigrationCategoryIds.KnownEndpoints),
                NewSettings(),
                shuttingDown.Token),
            Throws.InstanceOf<OperationCanceledException>(),
            "the host is stopping, so the store is not unreachable and saying it is would have an operator checking a database that is fine");
    }

    [Test]
    public void A_checkpoint_saved_by_another_instance_says_which_instance_to_stop()
    {
        var conflict = new MigrationCheckpointConflictException("Checkpoint KnownEndpoints was saved from version 3, but the stored row is at version 4.");

        var exception = Assert.ThrowsAsync<Exception>(async () => await MigrationStartup.CopyOrExplainWhyItStopped(
            Task.FromException<IReadOnlyList<MigrationCheckpoint>>(conflict),
            NoStall,
            Holding(),
            Attempted(MigrationCategoryIds.KnownEndpoints),
            NewSettings()));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("a second ServiceControl pointed at the same SQLServer database"),
                "the store's own message names a version, which tells nobody what is actually wrong");
            Assert.That(exception.Message, Does.Contain($"Stop the other instance, then restart with {MigrationSettings.EnabledKey} still on"),
                "this is the only refusal in the subsystem where doing nothing makes it worse");
            Assert.That(exception.Message, Does.Contain(conflict.Message).And.Contain("Nothing has opened on SQLServer yet"));
            Assert.That(exception.InnerException, Is.SameAs(conflict));
        });
    }

    [Test]
    public async Task A_copy_that_finished_is_reported_as_it_returned_without_the_store_being_asked()
    {
        var copied = new[] { Finished(MigrationCategoryIds.KnownEndpoints) };
        var store = Holding(InProgress(MigrationCategoryIds.EndpointSettings));

        var finished = await MigrationStartup.CopyOrExplainWhyItStopped(
            Task.FromResult<IReadOnlyList<MigrationCheckpoint>>(copied),
            NoStall,
            store,
            Attempted(MigrationCategoryIds.KnownEndpoints),
            NewSettings());

        Assert.Multiple(() =>
        {
            Assert.That(finished, Is.SameAs(copied), "what the engine returned is what the run did; the store is only for a copy that did not get to return anything");
            Assert.That(store.Reads, Is.Zero);
        });
    }

    static Task<IReadOnlyList<MigrationCheckpoint>> CancelledCopy => Task.FromCanceled<IReadOnlyList<MigrationCheckpoint>>(new CancellationToken(canceled: true));

    static string NoStall() => null!;

    static IReadOnlySet<string> Attempted(params string[] categoryIds) => categoryIds.ToHashSet(StringComparer.Ordinal);

    static MigrationCheckpoint InProgress(string categoryId) =>
        new(categoryId, MigrationCategoryState.InProgress, "a", 3, 0, null, null, null, null, null, null);

    static MigrationCheckpoint Finished(string categoryId) =>
        new(categoryId, MigrationCategoryState.Complete, null, 3, 0, null, null, null, null, null, null);

    static ReadAllCheckpointStore Holding(params MigrationCheckpoint[] checkpoints) => new(_ => checkpoints);

    static ReadAllCheckpointStore Failing(Exception failure, Action? firstDo = null) => new(_ =>
    {
        firstDo?.Invoke();
        throw failure;
    });

    static Settings NewSettings() =>
        new(transportType: "LearningTransport", persisterType: "SQLServer", errorRetentionPeriod: TimeSpan.FromDays(10));

    // Only ReadAll is reachable from here, and the store shares the target's database, so a target that has gone
    // away takes this read with it.
    sealed class ReadAllCheckpointStore(Func<CancellationToken, IReadOnlyList<MigrationCheckpoint>> readAll) : IMigrationCheckpointStore
    {
        public int Reads { get; private set; }

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(readAll(cancellationToken));
        }

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
