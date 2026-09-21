namespace ServiceControl.Migration;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses to start an error ingestion only host while a copy into its database is unfinished.
/// A refusal throws, which fails the start and stops the host.
/// </summary>
sealed class FinishedCopyBeforeAnIngestionNodeOpens(IMigrationCheckpointStore checkpointStore, Settings settings) : IHostedLifecycleService
{
    // An ingestion node never runs the copy: one host does that, and a second copier would race it. It stays out
    // until that copy finishes because ingesting writes to the target and stamps it as opened, which is what
    // turns abandoning a part-copied database from a clean rollback into permanent loss.
    public async Task StartingAsync(CancellationToken cancellationToken = default)
    {
        if (settings.MigrationAllowIncompleteExit)
        {
            return;
        }

        var unfinished = (await checkpointStore.ReadAll(cancellationToken))
            .Where(checkpoint => !checkpoint.State.IsFinished())
            .ToArray();

        if (unfinished.Length == 0)
        {
            return;
        }

        var detail = string.Join("; ", unfinished.Select(checkpoint => $"{checkpoint.CategoryId} is {checkpoint.State}"));

        throw new Exception(
            $"A copy into this database has not finished, so this error ingestion only host will not start and nothing has been lost. {detail}. " +
            $"Let the instance running the copy finish it and start this host again, or set {Settings.SettingsRootNamespace}/{MigrationSettings.AllowIncompleteExitKey}=true to ingest into a part-copied database and accept that it can no longer be abandoned without loss.");
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
