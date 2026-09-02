namespace ServiceControl.Api.Contracts;

using System;

/// <summary>
/// Response body for <c>POST /api/retention/sweep</c>. The <c>Status</c> field signals the
/// outcome: <c>started</c> (202), <c>already-running</c> (409), or
/// <c>not-supported</c> (501).
/// </summary>
public class RetentionSweepResponse
{
    public string Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? ErrorCutoff { get; set; }

    public DateTime? EventsCutoff { get; set; }

    /// <summary>A human-readable reason included when the operation is not supported.</summary>
    public string Reason { get; set; }
}