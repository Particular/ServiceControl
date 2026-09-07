namespace ServiceControl.Api.Contracts;

using System;

/// <summary>
/// Request body for <c>POST /api/maintenance/retention/purge</c>. Both cutoffs are optional; when omitted
/// the corresponding purge derives its cutoff from the configured retention period, as the
/// scheduled hourly sweep does. A bare future-dated cutoff is rejected.
/// </summary>
public class RetentionPurgeRequest
{
    /// <summary>
    /// Cutoff applied to the failed-message purge. <c>null</c> means
    /// <c>now - ErrorRetentionPeriod</c>.
    /// </summary>
    public DateTime? ErrorCutoff { get; set; }

    /// <summary>
    /// Cutoff applied to the event-log purge. <c>null</c> means
    /// <c>now - EventsRetentionPeriod</c>.
    /// </summary>
    public DateTime? EventsCutoff { get; set; }
}