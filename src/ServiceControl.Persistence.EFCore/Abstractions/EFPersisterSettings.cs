namespace ServiceControl.Persistence.EFCore.Abstractions;

public abstract class EFPersisterSettings : PersistenceSettings
{
    public required string ConnectionString { get; set; }
    public int CommandTimeout { get; set; } = 30;
    public TimeSpan ErrorRetentionPeriod { get; set; }
    public string? MessageBodyStoragePath { get; set; }
    public int MinBodySizeForCompression { get; set; } = 4096;
}
