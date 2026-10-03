namespace ServiceControl.Migration;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses to start a host that writes failed messages into the database, an error ingestion only host or
/// --import-failed-errors, while a copy into that database is unfinished. A refusal throws, which fails the
/// start and stops the host. When AllowIncompleteExit lets the host in anyway, it logs a warning and records
/// that a host has opened on the target.
/// </summary>
/// <param name="hostDescription">Names the host in the refusal and the warning, such as "this error ingestion only host".</param>
sealed class FinishedCopyBeforeAnIngestionNodeOpens(
    IMigrationCheckpointStore checkpointStore,
    IMigrationTargetReadiness readiness,
    Settings settings,
    string hostDescription,
    ILogger<FinishedCopyBeforeAnIngestionNodeOpens> logger) : IHostedLifecycleService
{
    bool letInOverAnUnfinishedCopy;

    // It runs with the migration off too, because a node someone forgot to flag would ingest into a part-copied
    // database and turn abandoning it from a clean rollback into permanent loss. A database that never migrated has
    // nothing unfinished, so its nodes start as before.
    public async Task StartingAsync(CancellationToken cancellationToken = default)
    {
        var unfinished = (await checkpointStore.ReadAll(cancellationToken))
            .Where(checkpoint => !checkpoint.State.IsFinished())
            .ToArray();

        if (unfinished.Length == 0)
        {
            return;
        }

        var detail = string.Join("; ", unfinished.Select(checkpoint => $"{checkpoint.CategoryId} is {checkpoint.State}"));

        if (settings.MigrationAllowIncompleteExit)
        {
            logger.LogWarning(
                "A copy into this database has not finished ({Detail}), and {Setting}=true lets {Host} ingest into it anyway. From now on the copy can no longer be abandoned without loss.",
                detail, $"{Settings.SettingsRootNamespace}/{MigrationSettings.AllowIncompleteExitKey}", hostDescription);
            letInOverAnUnfinishedCopy = true;
            return;
        }

        throw new Exception(
            $"A copy into this database has not finished, so {hostDescription} will not start and nothing has been lost. {detail}. " +
            $"Let the instance running the copy finish it and start this host again, or set {Settings.SettingsRootNamespace}/{MigrationSettings.AllowIncompleteExitKey}=true to ingest into a part-copied database and accept that it can no longer be abandoned without loss.");
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    // StartedAsync, so the marker is written only once the host is really serving, as RecordHostOpenedOnTarget does.
    public Task StartedAsync(CancellationToken cancellationToken = default) =>
        letInOverAnUnfinishedCopy ? readiness.RecordHostOpened(cancellationToken) : Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
