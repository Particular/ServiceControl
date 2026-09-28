namespace ServiceControl.Audit.Persistence.EFCore.Infrastructure;

interface IRetentionLock
{
    Task<IAsyncDisposable?> TryAcquire(CancellationToken cancellationToken = default);
}

static class RetentionLock
{
    public static string ResourceName(string? schema) => schema is null ? "audit_retention_sweep" : $"audit_retention_sweep:{schema}";
}
