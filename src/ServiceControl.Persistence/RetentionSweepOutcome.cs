namespace ServiceControl.Persistence;

/// <summary>
/// How a sweep that has ended turned out.
/// </summary>
public enum RetentionSweepOutcome
{
    /// <summary>
    /// Every pass ran to the end without an error.
    /// </summary>
    Succeeded,
    /// <summary>
    /// At least one pass threw. The other passes still ran.
    /// </summary>
    Failed,
    /// <summary>
    /// The sweep was cancelled before it finished, for example by shutdown.
    /// </summary>
    Cancelled
}