namespace ServiceControl.Migration.Guided;

using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Shows how far the running category has got. In a console window it redraws one line in place.
/// When the output is redirected it prints a new line each time another ten percent of the category is done.
/// </summary>
sealed class CopyProgress(MigrationConsole console)
{
    string categoryId;
    long tenthsPrinted;
    bool lineOpen;

    public void Update(MigrationCheckpoint checkpoint)
    {
        if (checkpoint.CategoryId != categoryId)
        {
            categoryId = checkpoint.CategoryId;
            tenthsPrinted = 0;
        }

        if (console.CanRedraw)
        {
            console.Write("\r" + Line(checkpoint));
            lineOpen = true;
            return;
        }

        if (Percent(checkpoint) / 10 is { } tenths && tenths > tenthsPrinted)
        {
            console.WriteLine(Line(checkpoint));
            tenthsPrinted = tenths;
        }
    }

    public void Finish(MigrationCheckpoint checkpoint)
    {
        console.WriteLine((console.CanRedraw ? "\r" : "") + Line(checkpoint));
        categoryId = null;
        lineOpen = false;
    }

    /// <summary>
    /// Ends a redrawn line that no <see cref="Finish" /> ended, so the next text starts on a line of its own.
    /// </summary>
    public void EndOpenLine()
    {
        if (lineOpen)
        {
            console.WriteLine();
            lineOpen = false;
        }
    }

    /// <summary>
    /// The share of the source's rows the copy is finished with, or null when the source was not counted.
    /// </summary>
    public static long? Percent(MigrationCheckpoint checkpoint) =>
        checkpoint.SourceTotal is { } total ? Percent(Done(checkpoint), total) : null;

    /// <summary>
    /// One progress line, padded to 79 characters so a redraw covers a longer line drawn before it.
    /// </summary>
    internal static string Line(string categoryId, long done, long? total, string state)
    {
        var count = total is { } sourceTotal ? $"{done,9:N0} / {sourceTotal,-9:N0}{Percent(done, sourceTotal),4}%" : $"{done,9:N0}{"",17}";
        return $"   {categoryId,-39}{count}  {state}".PadRight(79);
    }

    // An abandoned category stops for good, so a share of its rows would read as a copy still to finish.
    // A Blocked category has not started, so Copying would read as a copy still running after the pass has ended.
    static string Line(MigrationCheckpoint checkpoint) =>
        Line(checkpoint.CategoryId, Done(checkpoint), checkpoint.State == MigrationCategoryState.Abandoned ? null : checkpoint.SourceTotal,
            checkpoint.State == MigrationCategoryState.Blocked ? "Waiting" : checkpoint.State.CustomerState());

    // Skipped and already present rows count as done, because the source count includes them.
    static long Done(MigrationCheckpoint checkpoint) => checkpoint.CopiedCount + checkpoint.SkippedCount + checkpoint.AlreadyPresentCount;

    static long Percent(long done, long total) => total == 0 ? 100 : done * 100 / total;
}
