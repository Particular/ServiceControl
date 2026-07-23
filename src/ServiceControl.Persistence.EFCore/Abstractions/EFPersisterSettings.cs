namespace ServiceControl.Persistence.EFCore.Abstractions;

public abstract class EFPersisterSettings : PersistenceSettings
{
    public static readonly TimeSpan MigrationCommandTimeout = TimeSpan.FromMinutes(40);

    public const int DefaultCommandTimeout = 30;
    public const int DefaultMinBodySizeForCompression = 4096;
    public const int DefaultMaxBodySizeToStore = 102400; // 100 kb

    public required string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = DefaultCommandTimeout;
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public string? MessageBodyStoragePath { get; set; }
    public int MinBodySizeForCompression { get; set; } = DefaultMinBodySizeForCompression;
    public int MaxBodySizeToStore { get; set; } = DefaultMaxBodySizeToStore;
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelayInSeconds { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; }
    public bool EnableRetryOnFailure { get; set; } = true;
}
