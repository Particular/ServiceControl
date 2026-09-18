namespace ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// A database-wide lock that keeps the retention sweep to one host at a time. In a correct
/// deployment only one host is configured to sweep, so the lock exists for the misconfigured one:
/// two hosts concurrently dropping the same partition is unforgiving in a way two hosts running the
/// same batched delete is not.
/// </summary>
/// <remarks>
/// Implementations hold the lock on a dedicated connection for as long as the returned handle
/// lives. Both providers scope the lock to that session, so a host that crashes mid-sweep releases
/// it when its connection drops rather than wedging retention until someone intervenes.
/// </remarks>
public interface IRetentionLock
{
    /// <summary>
    /// Takes the lock without waiting. Null when another connection holds it. Disposing the handle
    /// releases the lock.
    /// </summary>
    Task<IAsyncDisposable?> TryAcquire(CancellationToken cancellationToken = default);
}

public static class RetentionLock
{
    /// <summary>
    /// Scoped to the schema, so instances that share a database through different schemas do not
    /// take turns sweeping.
    /// </summary>
    public static string ResourceName(string? schema) => schema is null ? "retention_sweep" : $"retention_sweep:{schema}";
}
