namespace ServiceControl.Persistence.DataMigration;

using System.Collections.Generic;

public enum MigrationVerificationVerdict { NotSelected, Ok, Abandoned, NotCounted, RowsNotRead, Incomplete }

public sealed record MigrationVerificationResult(
    string CategoryId,
    long? SourceCount,
    long CopiedCount,
    long SkippedCount,
    long AlreadyPresentCount,
    IReadOnlyDictionary<MigrationSkipReason, long> SkipReasons,
    long? Unaccounted,
    MigrationVerificationVerdict Verdict)
{
    public bool IsFault => Verdict == MigrationVerificationVerdict.Incomplete;
}

public static class MigrationVerification
{
    public static MigrationVerificationResult Evaluate(string categoryId, bool selected, long? sourceCount, MigrationCheckpoint? checkpoint)
    {
        IReadOnlyDictionary<MigrationSkipReason, long> reasons = checkpoint?.SkipReasons ?? new Dictionary<MigrationSkipReason, long>();

        var kind = MigrationCategoryRegistry.KindOf(categoryId);

        // The guard reads every row, so an unfinished one fails the command even when nothing selects it now.
        var outstanding = checkpoint?.IsOutstanding(kind, selected) == true;

        // A selected category nothing has started has no row, and every source row is unaccounted for.
        if (checkpoint is null)
        {
            return selected
                ? new MigrationVerificationResult(categoryId, sourceCount, 0, 0, 0, reasons, sourceCount, MigrationVerificationVerdict.Incomplete)
                : new MigrationVerificationResult(categoryId, sourceCount, 0, 0, 0, reasons, null, MigrationVerificationVerdict.NotSelected);
        }

        // The source's rows that the last complete pass did not account for. Below zero, the source holds fewer rows than the copy read.
        var unaccounted = sourceCount - checkpoint.CopiedCount - checkpoint.SkippedCount - checkpoint.AlreadyPresentCount;

        var result = new MigrationVerificationResult(
            categoryId, sourceCount, checkpoint.CopiedCount, checkpoint.SkippedCount, checkpoint.AlreadyPresentCount, reasons, unaccounted, Verdict(checkpoint, outstanding, unaccounted));

        // A row this selection leaves out keeps its own counts, so the report agrees with status, but nothing judges it.
        // An abandoned row keeps its verdict, because the rows it gave up are lost whatever is selected now.
        return !selected && !outstanding && checkpoint.State != MigrationCategoryState.Abandoned ? result with { Unaccounted = null, Verdict = MigrationVerificationVerdict.NotSelected } : result;
    }

    static MigrationVerificationVerdict Verdict(MigrationCheckpoint checkpoint, bool outstanding, long? unaccounted)
    {
        if (checkpoint.State == MigrationCategoryState.Abandoned)
        {
            return MigrationVerificationVerdict.Abandoned;
        }

        // A Failed or still-copying category can balance on counts alone while the guard still treats it as outstanding.
        // An unselected outstanding row has no source count, so it reaches here with unaccounted null and stops at this check.
        if (outstanding)
        {
            return MigrationVerificationVerdict.Incomplete;
        }

        return unaccounted switch
        {
            // This source cannot read the category, so the question this command exists to answer has no answer here.
            null => MigrationVerificationVerdict.NotCounted,
            // The source holds fewer rows than the copy read, which the command prints rather than judges.
            <= 0 => MigrationVerificationVerdict.Ok,
            _ => MigrationVerificationVerdict.RowsNotRead
        };
    }
}
