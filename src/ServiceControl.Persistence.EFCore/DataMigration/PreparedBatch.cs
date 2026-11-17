namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// A batch a writer has turned into rows, which the target inserts in the same transaction that saves the checkpoint.
/// Skips are rows not written, Merges are rows dropped because another row has the same key, and Changes are rows written with a value that differs from the source's.
/// The target logs each skip, merge and change as a warning after the commit.
/// </summary>
sealed record PreparedBatch(
    Func<ServiceControlDbContext, CancellationToken, Task<int>> Insert,
    int PreparedRowCount,
    IReadOnlyList<(string SourceId, MigrationSkipReason Reason, string Detail)> Skips,
    IReadOnlyList<(string SourceId, string Detail)> Merges)
{
    public IReadOnlyList<(string SourceId, string Detail)> Changes { get; init; } = [];

    /// <summary>
    /// Names the rows the database gives new ids, or is null when it gives none.
    /// </summary>
    public string? RenumberedRows { get; init; }
}
