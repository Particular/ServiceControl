namespace ServiceControl.Persistence.EFCore.Entities;

using ServiceControl.Persistence.DataMigration;

public class MigrationCheckpointEntity
{
    public required string CategoryId { get; set; }
    public MigrationCategoryState State { get; set; }
    public string? Cursor { get; set; }
    public long CopiedCount { get; set; }
    public long SkippedCount { get; set; }
    public long? SourceTotal { get; set; }
    public IReadOnlyDictionary<MigrationSkipReason, long>? SkipReasons { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastProgressAt { get; set; }
    public DateTime? SettledAt { get; set; }
    public string? LastError { get; set; }
    public long AlreadyPresentCount { get; set; }
    public long Version { get; set; }
}