#nullable enable
namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.EFCore.DataMigration;

// The only thing that tells an operator who upgraded without --setup that the schema is older than the build.
// Without it they get a raw EF error from whichever component touches the table first.
[TestFixture]
class CheckpointTableIsReadableTests
{
    [Test]
    public void A_checkpoint_table_this_build_cannot_read_stops_the_start_and_names_what_fixes_it()
    {
        var tableMissing = new InvalidOperationException("Invalid object name 'MigrationCheckpoints'.");
        var check = new CheckpointTableIsReadable(new ProbedCheckpointStore(tableMissing));

        var refusal = Assert.ThrowsAsync<InvalidOperationException>(() => check.StartingAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refusal!.Message, Does.Contain("--setup"), "a refusal that does not say what to run leaves the operator with a database they cannot start");
            Assert.That(refusal.Message, Does.Contain("older than this build"), "the raw provider error says the table is missing, not that the schema is behind");
            Assert.That(refusal.InnerException, Is.SameAs(tableMissing), "the provider's own error is the only thing saying which table and which database");
        }
    }

    [Test]
    public void A_checkpoint_table_that_reads_lets_the_start_go_on() =>
        Assert.DoesNotThrowAsync(() => new CheckpointTableIsReadable(new ProbedCheckpointStore()).StartingAsync(),
            "a check that refuses a healthy database stops every instance from starting");

    [Test]
    public async Task A_host_being_shut_down_is_not_reported_as_a_schema_that_is_behind()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var check = new CheckpointTableIsReadable(new ProbedCheckpointStore(new OperationCanceledException(cancellation.Token)));

        Assert.ThrowsAsync<OperationCanceledException>(() => check.StartingAsync(cancellation.Token),
            "a stop during startup would otherwise send the operator to run --setup against a database that is fine");
    }

    [Test]
    public async Task The_table_is_read_before_the_web_server_binds_rather_than_after()
    {
        var store = new ProbedCheckpointStore();
        var check = new CheckpointTableIsReadable(store);

        await check.StartAsync();
        await check.StartedAsync();

        Assert.That(store.ReadAllCalls, Is.Zero, "reading any later than StartingAsync lets a Windows service tell the Service Control Manager it is running on a schema this build cannot read");

        await check.StartingAsync();

        Assert.That(store.ReadAllCalls, Is.EqualTo(1));
    }

    // ReadAll is the only member the check calls, so the other two are here to satisfy the interface.
    sealed class ProbedCheckpointStore(Exception? readAllFailure = null) : IMigrationCheckpointStore
    {
        public int ReadAllCalls { get; private set; }

        public Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default)
        {
            ReadAllCalls++;

            return readAllFailure is null
                ? Task.FromResult<IReadOnlyList<MigrationCheckpoint>>([])
                : Task.FromException<IReadOnlyList<MigrationCheckpoint>>(readAllFailure);
        }

        public Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The startup check reads the whole table and never one category.");

        public Task<MigrationCheckpoint> Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The startup check never writes.");
    }
}
