namespace ServiceControl.Migration;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Throws, which stops the start, when a host that writes failed messages finds a migration whose required copy is not finished.
/// That is a required or unknown row that is not Done or Abandoned.
/// A database with no checkpoint row has never been migrated, and an optional category never holds a host back.
/// </summary>
sealed class FinishedCopyBeforeAnIngestionNodeOpens(
    IMigrationCheckpointStore checkpointStore,
    string hostDescription) : IHostedLifecycleService
{
    // Every writer checks persisted progress, because ingesting into a partial copy ends the free abort.
    public async Task StartingAsync(CancellationToken cancellationToken = default)
    {
        var unfinished = (await checkpointStore.ReadAll(cancellationToken))
            .Where(checkpoint => MigrationCategoryRegistry.KindOf(checkpoint.CategoryId) == MigrationCategoryKind.Required)
            .Where(checkpoint => !checkpoint.State.IsFinished())
            .ToArray();

        if (unfinished.Length == 0)
        {
            return;
        }

        var detail = string.Join(". ", unfinished.Select(checkpoint => checkpoint switch
        {
            _ when checkpoint.State.IsFailed() => MigrationStartup.FailedDetail(checkpoint),
            { State: MigrationCategoryState.Blocked } when MigrationCategoryRegistry.Find(checkpoint.CategoryId)?.MustFollow is { } ahead => $"{checkpoint.CategoryId} is waiting for {ahead} ({checkpoint.State})",
            _ => $"{checkpoint.CategoryId} is Copying ({checkpoint.State})"
        }));

        const string advice = "With ServiceControl and all ingestion workers stopped, resolve any Failed categories and run --start-data-migration again before starting this host.";

        throw new Exception($"A copy into this database has not finished, so {hostDescription} will not start and nothing has been lost. {detail}. {advice}");
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
