namespace ServiceControl.Persistence.EFCore.Abstractions;

public abstract class EFPersisterSettings : PersistenceSettings
{
    public static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(40);

    public const int DefaultCommandTimeout = 30;

    public required string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = DefaultCommandTimeout;
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public required BodyStorageSettings BodyStorage { get; set; }
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelayInSeconds { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; }
    public bool EnableRetryOnFailure { get; set; } = true;
}
