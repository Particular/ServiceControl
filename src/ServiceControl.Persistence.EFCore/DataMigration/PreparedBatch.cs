namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// One batch turned into rows, ready for the target to insert inside its own transaction. Nothing here has
/// touched the database yet.
/// </summary>
/// <param name="Insert">Runs the insert and returns how many rows it added. The target calls it inside the transaction that also saves the checkpoint.</param>
/// <param name="PreparedRowCount">How many rows the writer built, which is more than the insert adds when the table already holds some of their keys.</param>
/// <param name="Skips">One entry per row the writer will not insert, with the reason and a detail line for the log.</param>
/// <param name="Merges">One entry per prepared row whose key the target treats as the same key as another row's, either earlier in the batch or already in the table, so the insert keeps the other row and drops this one. Each carries a detail line naming both keys and the one kept, which the target logs as a warning after the commit. The dropped row is counted as already present.</param>
sealed record PreparedBatch(
    Func<ServiceControlDbContext, CancellationToken, Task<int>> Insert,
    int PreparedRowCount,
    IReadOnlyList<(string SourceId, MigrationSkipReason Reason, string Detail)> Skips,
    IReadOnlyList<(string SourceId, string Detail)> Merges);
