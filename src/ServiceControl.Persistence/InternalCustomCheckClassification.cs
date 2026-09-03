//Nullable enable is explicit here because this file is
//included in audit test projects that do not have it enabled.
#nullable enable
namespace ServiceControl.Contracts.CustomChecks
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Classifies the custom checks ServiceControl ships itself so ServicePulse can tell them apart from
    /// checks reported by monitored endpoints. Feeds the computed <see cref="CustomCheckView.Internal"/>
    /// property, which is the only place the classification is ever rendered.
    ///
    /// Only the primary instance serves /api/customchecks, so only it needs the classification: its own
    /// checks arrive via InternalCustomCheckManager, and the audit instance's checks arrive as
    /// ReportCustomCheckResult messages (a wire contract owned by the NServiceBus.CustomChecks package, so
    /// nothing extra can travel in the message — the id has to be recognized here). Consequence: the audit
    /// section below is a list of string literals. New audit-instance checks MUST be added here; there are
    /// approval tests that enforce this.
    /// </summary>
    public static class InternalCustomCheckClassification
    {
        // Keyed by CustomCheckId only, deliberately not by (id, category):
        //  - "RavenDB dirty memory" is reported by both the primary ("ServiceControl Health")
        //    and the audit instance ("ServiceControl.Audit Health") with the same id;
        //  - "Audit Message Ingestion Process" is reported by the audit instance under the
        //    category "ServiceControl Health" (unlike its siblings).
        // Comparison is ordinal-ignore-case, mirroring CustomChecksMailNotification.IsHealthCheck.
        static readonly HashSet<string> internalIds =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // ----- Primary instance -----
                "ServiceControl Primary Instance",
                "ServiceControl Remotes",
                "Saga Audit Configuration",
                "Error Message Ingestion",
                "Error Message Ingestion Process",
                "Error Database Index Errors",   // RavenDB persister
                "Error Database Index Lag",      // RavenDB persister
                "RavenDB dirty memory",          // primary AND audit
                "ServiceControl database",       // RavenDB persister
                "Message Ingestion Process",     // RavenDB persister
                "ServiceControl body storage",   // EF Core persisters
                "Dead Letter Queue",             // ASBS / IBMMQ / MSMQ

                // ----- Audit instance (forwarded to the primary via ReportCustomCheckResult) -----
                "Audit Message Ingestion",
                "Audit Message Ingestion Process",
                "Audit Database Index Lag",
                "ServiceControl.Audit database",
            };

        /// <summary>
        /// True when the id is one ServiceControl ships itself (primary, audit or transport check),
        /// false when it was reported by a monitored endpoint and has no platform-health semantics.
        /// </summary>
        public static bool IsInternal(string? customCheckId) =>
            customCheckId is not null && internalIds.Contains(customCheckId);
    }
}