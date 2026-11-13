namespace ServiceControl.Migration;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.DataMigration;

sealed class MigrationBeforeTheHostOpens(IServiceProvider services) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken = default)
    {
        var checkpoints = services.GetService<IMigrationCheckpointStore>();
        if (checkpoints is null)
        {
            return;
        }

        await new FinishedCopyBeforeAnIngestionNodeOpens(checkpoints, "ServiceControl").StartingAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
