namespace ServiceControl.Api.Contracts;

using System;

/// <summary>
/// Request body for <c>POST /api/retention/sweep</c>. Both cutoffs are optional; when omitted
/// the corresponding sub-sweep derives its cutoff from the configured retention period, as the
/// scheduled hourly sweep does. A bare future-dated cutoff is rejected.
/// </summary>
public class RetentionSweepRequest
{
    /// <summary>
    /// Cutoff applied to the failed-message sweep. <c>null</c> means
    /// <c>now - ErrorRetentionPeriod</c>.
    /// </summary>
    public DateTime? ErrorCutoff { get; set; }

    /// <summary>
    /// Cutoff applied to the event-log sweep. <c>null</c> means
    /// <c>now - EventsRetentionPeriod</c>.
    /// </summary>
    public DateTime? EventsCutoff { get; set; }
}