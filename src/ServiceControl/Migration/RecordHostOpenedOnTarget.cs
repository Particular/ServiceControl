namespace ServiceControl.Migration;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Calls <see cref="IMigrationTargetReadiness.RecordHostOpened"/> when a host opens
/// on a database a migration has already written to.
/// </summary>
sealed class RecordHostOpenedOnTarget(IServiceProvider services) : IHostedLifecycleService
{
    // StartedAsync, not StartAsync: the web server binds its port during StartAsync, and a start that dies there
    // served nothing. RavenDB registers no checkpoint store and needs no migration marker.
    public async Task StartedAsync(CancellationToken cancellationToken = default)
    {
        var checkpointStore = services.GetService<IMigrationCheckpointStore>();

        // Only a database a migration has written to gets the stamp, so the marker answers whether a host
        // has opened since the copy began rather than whether one ever ran on this database.
        if (checkpointStore is not null && (await checkpointStore.ReadAll(cancellationToken)).Count > 0)
        {
            await services.GetRequiredService<IMigrationTargetReadiness>().RecordHostOpened(cancellationToken);
        }
    }

    public Task StartingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
