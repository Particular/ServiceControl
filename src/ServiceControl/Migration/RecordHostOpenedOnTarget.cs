namespace ServiceControl.Migration;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Calls <see cref="IMigrationTargetReadiness.RecordHostOpened"/> when a host started with the migration on opens
/// on a database a migration has already written to.
/// </summary>
sealed class RecordHostOpenedOnTarget(IServiceProvider services) : IHostedLifecycleService
{
    // StartedAsync, not StartAsync: the web server binds its port during StartAsync, and a start that dies there
    // served nothing. The stores are resolved here rather than injected, because a RavenDB host registers neither
    // and is refused before it gets this far.
    public async Task StartedAsync(CancellationToken cancellationToken = default)
    {
        var checkpointStore = services.GetRequiredService<IMigrationCheckpointStore>();

        // Only a database a migration has written to gets the stamp, so the marker answers whether a host
        // has opened since the copy began rather than whether one ever ran on this database.
        if ((await checkpointStore.ReadAll(cancellationToken)).Count > 0)
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
