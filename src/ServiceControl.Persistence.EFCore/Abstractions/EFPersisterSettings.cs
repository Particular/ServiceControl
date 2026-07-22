namespace ServiceControl.Persistence.EFCore.Abstractions;

public abstract class EFPersisterSettings : PersistenceSettings
{
    public static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(40);

    public required string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = 30;
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public string? MessageBodyStoragePath { get; set; }
    public int MinBodySizeForCompression { get; set; } = 4096;
    public int MaxBodySizeToStore { get; set; } = 102400;
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelayInSeconds { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; }
    public bool EnableRetryOnFailure { get; set; } = true;
}
