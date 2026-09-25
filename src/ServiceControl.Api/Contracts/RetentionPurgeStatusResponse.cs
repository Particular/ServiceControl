namespace ServiceControl.Api.Contracts;

using System;

/// <summary>
/// Response body for <c>GET /api/maintenance/retention/purge/status</c>. On a persister with no sweeper
/// (e.g. RavenDB) the endpoint returns 501 with a <see cref="Reason"/> instead.
/// </summary>
public class RetentionPurgeStatusResponse
{
    public bool IsRunning { get; set; }

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastFinishedAt { get; set; }

    public DateTime? LastErrorCutoff { get; set; }

    public DateTime? LastEventsCutoff { get; set; }

    /// <summary>
    /// How the most recent purge ended. Null while it is still running or before any has run.
    /// </summary>
    public RetentionPurgeOutcome? LastOutcome { get; set; }

    /// <summary>
    /// What went wrong in each pass that failed, or null when none did. 
    /// A cancelled purge keeps the errors from before the cancellation. The full detail is in the log.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>Present only on the 501 Not Implemented response.</summary>
    public string Reason { get; set; }
}