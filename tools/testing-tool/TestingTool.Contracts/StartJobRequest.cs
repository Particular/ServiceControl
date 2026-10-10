namespace TestingTool.Contracts;

/// <summary>
/// Request body for <c>POST /api/jobs/{name}/start</c>.
/// All fields are optional; defaults are taken from the job definition.
/// </summary>
public sealed class StartJobRequest
{
    /// <summary>Cycle interval in seconds. Defaults to the job's default interval.</summary>
    public double? IntervalSeconds { get; init; }

    /// <summary>
    /// Timespan backing the retention-purge cutoffs, consumed by the retention-sweep job (ignored
    /// by the others): every sweep it triggers purges failed messages and event-log rows older
    /// than <c>now - cutoffTimespan</c>, in .NET timespan format (e.g. <c>14.00:00:00</c> for 14
    /// days, <c>00:30:00</c> for 30 minutes). When omitted, each purge request leaves the cutoffs
    /// unset and ServiceControl derives them from its configured retention periods, just as the
    /// scheduled hourly sweep does.
    /// </summary>
    public string? CutoffTimespan { get; init; }
}