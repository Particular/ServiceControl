namespace ServiceControl.Migration;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses to start a host that writes failed messages into the database, an error ingestion only host or
/// --import-failed-errors, until every required category is Done or Abandoned. An optional category, which copies
/// in the background once the main host has opened, never holds it back, but a category under an id this build
/// does not know does. A refusal throws, which fails the start and stops the host. Start such a host only after the
/// main host has opened, because a database with no checkpoint row yet reads as one no migration has touched.
/// </summary>
/// <param name="checkpointStore">Where the checkpoint rows are read. A database no migration has touched holds none, so the host starts as before.</param>
/// <param name="hostDescription">Names the host in the refusal, such as "this error ingestion only host".</param>
sealed class FinishedCopyBeforeAnIngestionNodeOpens(
    IMigrationCheckpointStore checkpointStore,
    string hostDescription) : IHostedLifecycleService
{
    // It runs with the migration off too, because a node someone forgot to flag would ingest into a part-copied
    // database and turn abandoning it from a clean rollback into permanent loss. A database that never migrated has
    // nothing unfinished, so its nodes start as before.
    public async Task StartingAsync(CancellationToken cancellationToken = default)
    {
        var unfinished = (await checkpointStore.ReadAll(cancellationToken))
            .Where(checkpoint => MigrationCategoryRegistry.Find(checkpoint.CategoryId)?.Kind != MigrationCategoryKind.Optional)
            .Where(checkpoint => !checkpoint.State.IsFinished())
            .ToArray();

        if (unfinished.Length == 0)
        {
            return;
        }

        var detail = string.Join(". ", unfinished.Select(checkpoint => checkpoint.State.IsFailed()
            ? MigrationStartup.FailedDetail(checkpoint)
            : $"{checkpoint.CategoryId} is Copying ({checkpoint.State})"));

        var advice = unfinished.Any(checkpoint => !checkpoint.State.IsFailed())
            ? "Let the instance running the copy finish it and start this host again."
            : "Start this host again once they are settled.";

        throw new Exception($"A copy into this database has not finished, so {hostDescription} will not start and nothing has been lost. {detail}. {advice}");
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
