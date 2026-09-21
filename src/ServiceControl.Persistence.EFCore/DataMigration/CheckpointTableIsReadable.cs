namespace ServiceControl.Persistence.EFCore.DataMigration;

using Microsoft.Extensions.Hosting;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses the start when the migration checkpoint table cannot be read, which means the database was upgraded to this build without <c>--setup</c>.
/// </summary>
sealed class CheckpointTableIsReadable(IMigrationCheckpointStore checkpointStore) : IHostedLifecycleService
{
    // StartingAsync, so the refusal lands before the web server binds. RecordHostOpenedOnTarget reads the same
    // table for the stamp, but it has to wait until StartedAsync to see what the copy wrote, and by then a
    // Windows service has already told the Service Control Manager it is running.
    public async Task StartingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await checkpointStore.ReadAll(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "ServiceControl could not read its migration checkpoint table, so this database's schema is older than this build. Run ServiceControl with --setup against it before starting, whether or not you intend to migrate.", ex);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
