namespace ServiceControl.Audit.Persistence.Sql.Core.Infrastructure;

using Abstractions;
using Microsoft.Extensions.Hosting;

public class BatchIdGenerator(TimeProvider timeProvider, AuditSqlPersisterSettings settings) : BackgroundService
{
    readonly PeriodicTimer rotationTimer = new(settings.BatchIdRotationInterval, timeProvider);

    public Guid CurrentBatchId { get; private set; } = Guid.CreateVersion7();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await rotationTimer.WaitForNextTickAsync(stoppingToken))
        {
            CurrentBatchId = Guid.CreateVersion7();
        }
    }

    public override void Dispose()
    {
        rotationTimer.Dispose();
        base.Dispose();
    }
}
