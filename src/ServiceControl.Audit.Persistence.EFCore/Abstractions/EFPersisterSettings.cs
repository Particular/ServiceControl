namespace ServiceControl.Audit.Persistence.EFCore.Abstractions;

using ServiceControl.Infrastructure;

public class EFPersisterSettings
{
    public static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(40);

    public const int DefaultCommandTimeout = 30;

    public required string ConnectionString { get; init; }

    public string? Schema
    {
        get;
        init => field = value is null ? null : SchemaName.Validate(value);
    }

    public int CommandTimeout { get; init; } = DefaultCommandTimeout;
    public TimeSpan QueryTimeout { get; init; } = QueryTimeLimit.Default;
    public required TimeSpan AuditRetentionPeriod { get; init; }
    public required int MaxBodySizeToStore { get; init; }
    public int MaxRetryCount { get; init; } = 5;
    public int MaxRetryDelayInSeconds { get; init; } = 30;
    public bool EnableRetryOnFailure { get; init; } = true;
    public bool EnableSensitiveDataLogging { get; init; }
}
