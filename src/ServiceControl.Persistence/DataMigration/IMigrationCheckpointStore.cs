namespace ServiceControl.Persistence.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public enum MigrationCategoryState
{
    NotStarted,
    InProgress,
    Complete,
    CompleteWithErrors,
    Halted,
    Abandoned
}

/// <summary>A category's saved progress: where a restart carries on from, and what the status and verify commands report.</summary>
public sealed record MigrationCheckpoint(
    string CategoryId,
    bool Selected,
    MigrationCategoryState State,
    string? Cursor,
    long CopiedCount,
    long SkippedCount,
    long? SourceTotal,
    IReadOnlyDictionary<string, long>? SkipReasons,
    DateTime? StartedAt,
    DateTime? LastProgressAt,
    DateTime? CompletedAt,
    DateTime? AbandonedAt,
    string? LastError,
    long AlreadyPresentCount = 0);

public interface IMigrationCheckpointStore
{
    Task<IReadOnlyList<MigrationCheckpoint>> ReadAll(CancellationToken cancellationToken = default);
    Task<MigrationCheckpoint?> Read(string categoryId, CancellationToken cancellationToken = default);
    Task Upsert(MigrationCheckpoint checkpoint, CancellationToken cancellationToken = default);
}
