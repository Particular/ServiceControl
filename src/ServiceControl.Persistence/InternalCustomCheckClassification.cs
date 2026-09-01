namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Classifies the custom checks ServiceControl ships itself so ServicePulse can tell them apart from
    /// checks reported by monitored endpoints and grade platform health accordingly. Feeds the computed
    /// <see cref="CustomCheckView.Internal"/> and <see cref="CustomCheckView.Severity"/> properties, which
    /// is the only place the classification is ever rendered.
    ///
    /// Only the primary instance serves /api/customchecks, so only it needs the classification: its own
    /// checks arrive via InternalCustomCheckManager, and the audit instance's checks arrive as
    /// ReportCustomCheckResult messages (a wire contract owned by the NServiceBus.CustomChecks package, so
    /// severity cannot travel in the message — it has to be re-derived here). Consequence: the audit section
    /// below is a list of string literals. New audit-instance checks MUST be added here; the tests listed in
    /// .plans/internal-customchecks.md §8 exist to catch omissions.
    /// </summary>
    public static class InternalCustomCheckClassification
    {
        // Keyed by CustomCheckId only, deliberately not by (id, category):
        //  - "RavenDB dirty memory" is reported by both the primary ("ServiceControl Health")
        //    and the audit instance ("ServiceControl.Audit Health") with the same severity;
        //  - "Audit Message Ingestion Process" is reported by the audit instance under the
        //    category "ServiceControl Health" (unlike its siblings).
        // Comparison is ordinal-ignore-case, mirroring CustomChecksMailNotification.IsHealthCheck.
        static readonly Dictionary<string, CustomCheckSeverity> severityById =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // ----- Primary instance -----
                ["ServiceControl Primary Instance"] = CustomCheckSeverity.Unavailable,
                ["ServiceControl Remotes"] = CustomCheckSeverity.Unavailable,
                ["Saga Audit Configuration"] = CustomCheckSeverity.Ignore,
                ["Error Message Ingestion"] = CustomCheckSeverity.Degraded,
                ["Error Message Ingestion Process"] = CustomCheckSeverity.Degraded,
                ["Error Database Index Errors"] = CustomCheckSeverity.Degraded,   // RavenDB persister
                ["Error Database Index Lag"] = CustomCheckSeverity.Degraded,     // RavenDB persister
                ["RavenDB dirty memory"] = CustomCheckSeverity.Degraded,         // primary AND audit
                ["ServiceControl database"] = CustomCheckSeverity.Degraded,      // RavenDB persister
                ["Message Ingestion Process"] = CustomCheckSeverity.Degraded,     // RavenDB persister
                ["ServiceControl body storage"] = CustomCheckSeverity.Degraded,  // EF Core persisters
                ["Dead Letter Queue"] = CustomCheckSeverity.Degraded,             // ASBS / IBMMQ / MSMQ

                // ----- Audit instance (forwarded to the primary via ReportCustomCheckResult) -----
                ["Audit Message Ingestion"] = CustomCheckSeverity.Degraded,
                ["Audit Message Ingestion Process"] = CustomCheckSeverity.Degraded,
                ["Audit Database Index Lag"] = CustomCheckSeverity.Degraded,
                ["ServiceControl.Audit database"] = CustomCheckSeverity.Degraded,
            };

        /// <summary>
        /// The severity of a shipped check, or null when the id is not one ServiceControl knows — which
        /// means the check was reported by a monitored endpoint and has no platform-health semantics.
        /// </summary>
        public static CustomCheckSeverity? SeverityFor(string? customCheckId) =>
            customCheckId is not null && severityById.TryGetValue(customCheckId, out var severity)
                ? severity
                : null;
    }
}