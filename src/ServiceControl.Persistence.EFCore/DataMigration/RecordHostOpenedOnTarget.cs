namespace ServiceControl.Persistence.EFCore.DataMigration;

using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Calls <see cref="IMigrationTargetReadiness.RecordHostOpened"/> when the host starts, on a database a migration has already written to.
/// </summary>
sealed class RecordHostOpenedOnTarget(IMigrationTargetReadiness readiness, IMigrationCheckpointStore checkpointStore) : IHostedLifecycleService
{
    // StartedAsync, not StartAsync: the web server binds its port during StartAsync, and a start that dies
    // there served nothing. Every command that runs a host reaches it, not only RunCommand.
    public async Task StartedAsync(CancellationToken cancellationToken = default)
    {
        // Only a database a migration has written to gets the stamp, so the marker answers whether a host
        // has opened since the copy began rather than whether one ever ran on this database.
        if ((await checkpointStore.ReadAll(cancellationToken)).Count > 0)
        {
            await readiness.RecordHostOpened(cancellationToken);
        }
    }

    public Task StartingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
