namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

// The Record methods run concurrently across the batch, so they only add to thread safe
// collections. Every database call happens in Complete, on one thread, inside one transaction
// shared by the failed message, known endpoint and audit rows of the batch.
public class EFIngestionUnitOfWork : IIngestionUnitOfWork
{
    readonly ServiceControlDbContext dbContext;
    readonly IAsyncDisposable scope;
    readonly IFailedMessageIngestionSqlDialect dialect;
    readonly IAuditIngestionSqlDialect auditDialect;
    readonly TimeProvider timeProvider;
    readonly EFAuditIngestionUnitOfWork audit;
    readonly ConcurrentQueue<RecordedFailedProcessingAttempt> failedProcessingAttempts = new();
    readonly ConcurrentQueue<Task> bodyWrites = new();
    readonly ConcurrentQueue<KnownEndpoint> knownEndpoints = new();
    readonly ConcurrentQueue<ConfirmedRetry> confirmedRetries = new();

    public EFIngestionUnitOfWork(IAsyncDisposable scope, ServiceControlDbContext dbContext, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings, IFailedMessageIngestionSqlDialect dialect, IAuditIngestionSqlDialect auditDialect, TimeProvider timeProvider)
    {
        this.scope = scope;
        this.dbContext = dbContext;
        this.dialect = dialect;
        this.auditDialect = auditDialect;
        this.timeProvider = timeProvider;
        Recoverability = new EFRecoverabilityIngestionUnitOfWork(this, storagePersistence, settings);
        Monitoring = new EFMonitoringIngestionUnitOfWork(this);
        audit = new EFAuditIngestionUnitOfWork(this, storagePersistence, settings, AuditHours.Truncate(timeProvider.GetUtcNow().UtcDateTime));
    }

    public IMonitoringIngestionUnitOfWork Monitoring { get; }

    public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

    public IAuditIngestionUnitOfWork Audit => audit;

    internal void Record(RecordedFailedProcessingAttempt attempt) => failedProcessingAttempts.Enqueue(attempt);

    internal void RecordBodyWrite(Task bodyWrite) => bodyWrites.Enqueue(bodyWrite);

    internal void Record(KnownEndpoint knownEndpoint) => knownEndpoints.Enqueue(knownEndpoint);

    internal void RecordConfirmedRetry(ConfirmedRetry confirmedRetry) => confirmedRetries.Enqueue(confirmedRetry);

    public async Task Complete(CancellationToken cancellationToken = default)
    {
        // External bodies are written before the rows that point at them
        await Task.WhenAll(bodyWrites);

        if (failedProcessingAttempts.IsEmpty && knownEndpoints.IsEmpty && confirmedRetries.IsEmpty && audit.IsEmpty)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var failedMessageWriter = new FailedMessageBatchWriter(dbContext, dialect);
        var auditWriter = new AuditBatchWriter(dbContext, auditDialect);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            await failedMessageWriter.Write(failedProcessingAttempts, knownEndpoints, confirmedRetries, now, ct);
            await auditWriter.Write(audit.Messages, audit.Snapshots, ct);

            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await scope.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
