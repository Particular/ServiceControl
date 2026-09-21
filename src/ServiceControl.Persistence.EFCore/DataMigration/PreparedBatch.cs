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
/// <param name="SkipsReflectTheSource">False once an earlier category dropped rows, because a skip here may then be this migration's own doing rather than something the source never had.</param>
sealed record PreparedBatch(
    Func<ServiceControlDbContext, CancellationToken, Task<int>> Insert,
    int PreparedRowCount,
    IReadOnlyList<(string SourceId, MigrationSkipReason Reason, string Detail)> Skips,
    bool SkipsReflectTheSource = true)
{
    public int BenignSkipCount => SkipsReflectTheSource ? Skips.Count(skip => skip.Reason.IsBenign()) : 0;
}
