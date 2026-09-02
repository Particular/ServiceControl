namespace ServiceControl.Api.Contracts;

using System;

/// <summary>
/// Response body for <c>GET /api/retention/sweep/status</c>. On a persister with no sweeper
/// (e.g. RavenDB) the endpoint returns 501 with a <see cref="Reason"/> instead.
/// </summary>
public class RetentionSweepStatus
{
    public bool IsRunning { get; set; }

    public DateTime? LastStartedAt { get; set; }

    public DateTime? LastFinishedAt { get; set; }

    public DateTime? LastErrorCutoff { get; set; }

    public DateTime? LastEventsCutoff { get; set; }

    public string LastError { get; set; }

    /// <summary>Present only on the 501 Not Implemented response.</summary>
    public string Reason { get; set; }
}