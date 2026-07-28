namespace ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;

using System.Collections.Concurrent;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.DbContexts;
using ServiceControl.Persistence.EFCore.Infrastructure;
using ServiceControl.Persistence.UnitOfWork;

// RecordFailedProcessingAttempt runs concurrently across the batch, so the Record methods only
// add to thread safe collections. Every database call happens in Complete, on one thread.
public class EFIngestionUnitOfWork : IIngestionUnitOfWork
{
    readonly ServiceControlDbContext dbContext;
    readonly IAsyncDisposable scope;
    readonly IIngestionSqlDialect dialect;
    readonly TimeProvider timeProvider;
    readonly ConcurrentQueue<RecordedFailedProcessingAttempt> failedProcessingAttempts = new();
    readonly ConcurrentQueue<Task> bodyWrites = new();
    readonly ConcurrentQueue<KnownEndpoint> knownEndpoints = new();
    readonly ConcurrentQueue<Guid> confirmedRetries = new();

    public EFIngestionUnitOfWork(IAsyncDisposable scope, ServiceControlDbContext dbContext, IBodyStoragePersistence storagePersistence, EFPersisterSettings settings, IIngestionSqlDialect dialect, TimeProvider timeProvider)
    {
        this.scope = scope;
        this.dbContext = dbContext;
        this.dialect = dialect;
        this.timeProvider = timeProvider;
        Recoverability = new EFRecoverabilityIngestionUnitOfWork(this, storagePersistence, settings);
        Monitoring = new EFMonitoringIngestionUnitOfWork(this);
    }

    public IMonitoringIngestionUnitOfWork Monitoring { get; }

    public IRecoverabilityIngestionUnitOfWork Recoverability { get; }

    internal void Record(RecordedFailedProcessingAttempt attempt) => failedProcessingAttempts.Enqueue(attempt);

    internal void RecordBodyWrite(Task bodyWrite) => bodyWrites.Enqueue(bodyWrite);

    internal void Record(KnownEndpoint knownEndpoint) => knownEndpoints.Enqueue(knownEndpoint);

    internal void RecordConfirmedRetry(Guid uniqueMessageId) => confirmedRetries.Enqueue(uniqueMessageId);

    public async Task Complete(CancellationToken cancellationToken)
    {
        // External bodies are written before the rows that point at them
        await Task.WhenAll(bodyWrites);

        var writer = new FailedMessageBatchWriter(dbContext, dialect);

        await writer.Write(failedProcessingAttempts, knownEndpoints, confirmedRetries, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await dbContext.DisposeAsync();
        await scope.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
