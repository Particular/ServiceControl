namespace ServiceControl.Plugin.CustomChecks.Messages;

using System;
using NServiceBus;

// NOTE: This type intentionally mirrors ServiceControl's internal
// ServiceControl.Plugin.CustomChecks.Messages.ReportCustomCheckResult contract (same namespace
// and type name) so that messages sent from the testing tool are ingested by ServiceControl as
// genuine custom-check reports. The message type's FullName is what NServiceBus puts in the
// NServiceBus.EnclosedMessageTypes header, which ServiceControl uses to route the message to its
// ReportCustomCheckResultHandler. Property names are left in PascalCase to match the contract
// both sides serialize with the default NServiceBus SystemJsonSerializer options.

/// <summary>
/// A custom-check result report, shaped to match ServiceControl's internal
/// <c>ReportCustomCheckResult</c> message so the testing tool can inject custom-check
/// pass/fail reports that ServiceControl ingests as if a real endpoint (or ServiceControl
/// itself) had produced them.
/// </summary>
sealed class ReportCustomCheckResult : ICommand
{
    /// <summary>Stable id of the host that ran the check. ServiceControl groups custom checks
    /// by HostId + CustomCheckId + Category, so a stable value per check gives coherent state
    /// transitions (pass → fail → pass) in ServicePulse.</summary>
    public Guid HostId { get; set; }

    public string CustomCheckId { get; set; } = "";

    public string Category { get; set; } = "";

    /// <summary>Whether the check failed. When true, <see cref="FailureReason"/> must be set.</summary>
    public bool HasFailed { get; set; }

    public string? FailureReason { get; set; }

    public DateTime ReportedAt { get; set; }

    /// <summary>The endpoint that ran the check. Set to the ServiceControl instance name (e.g.
    /// "Particular.ServiceControl") so the report looks like an internal ServiceControl custom
    /// check rather than one coming from an external endpoint.</summary>
    public string EndpointName { get; set; } = "";

    /// <summary>The display name of the host that ran the check.</summary>
    public string Host { get; set; } = "";
}