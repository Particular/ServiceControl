using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NServiceBus;
using ServiceControl.Plugin.CustomChecks.Messages;

namespace TestingTool.Jobs;

/// <summary>
/// Periodic job that injects custom-check reports into ServiceControl so the Custom Checks view
/// in ServicePulse shows failures. Each cycle it emits a <see cref="ReportCustomCheckResult"/>
/// for a pool of plausibly-named internal-looking checks, randomly marking some as failed and
/// the rest as passed. The reports are sent over the transport directly to the ServiceControl
/// input queue with <c>EndpointName</c> set to the ServiceControl instance name and the
/// <c>ServiceControl Health</c> category, so they appear in ServicePulse as genuine internal
/// ServiceControl custom checks rather than checks coming from an external endpoint.
///
/// This complements the error-load scenarios: those exercise ServiceControl's error ingestion
/// and recoverability pipelines, while this job exercises its custom-check ingestion and the
/// ServicePulse Custom Checks dashboard under failure load.
/// </summary>
public sealed class CustomCheckFailureJob(
    IMessageSession session,
    TestingToolMetrics metrics,
    IOptions<TestingToolOptions> options,
    Meter meter,
    ILogger<CustomCheckFailureJob> logger) : JobBase("testing-tool.custom-checks")
{
    private readonly Counter<long> _reportedCounter = meter.CreateCounter<long>("custom_checks_reported_total");

    public override string Name => "custom-check-failures";
    public override string Description => "Randomly report internal-looking ServiceControl custom check failures to ServiceControl each cycle.";
    public override string Category => "Custom Checks";
    public override TimeSpan DefaultInterval => options.Value.CustomCheckInterval;

    // A pool of custom checks that read like ServiceControl's own internal checks: same category
    // ("ServiceControl Health") and plausible ids/reasons. The ids are intentionally distinct from
    // the always-on real internal checks ("Error Message Ingestion Process", "Error Message
    // Ingestion") so the injected failures don't mask or fight the genuine check state, while still
    // looking internal in the ServicePulse Custom Checks view.
    private static readonly InternalCheck[] Checks =
    [
        new("Message Retention", "ServiceControl Health",
        [
            "Retention sweep is falling behind: aged failed messages are accumulating faster than they are being deleted.",
            "Retention sweep skipped a cycle because a previous sweep was still running; backlog growing.",
            "Unable to verify retention progress — the retention store did not respond within the timeout."
        ]),

        new("Database Consistency", "ServiceControl Health",
        [
            "A consistency check of the failed-message document store detected orphaned retry state documents.",
            "Document store reported stale indexes; query results may be inconsistent until indexes catch up.",
            "Database maintenance task did not complete within the expected window."
        ]),

        new("Audit Forwarding", "ServiceControl Health",
        [
            "The audit forwarding queue is not reachable; forwarded audit messages are queueing up locally.",
            "Audit forwarding has been disabled because the forwarding queue could not be verified."
        ]),

        new("Storage Capacity", "ServiceControl Health",
        [
            "Free disk space on the ServiceControl storage volume dropped below the minimum threshold.",
            "Message body storage is nearly full; ingestion will be throttled until space is reclaimed."
        ]),

        new("License Validity", "ServiceControl Health",
        [
            "The Particular platform license is due to expire soon and should be renewed.",
            "License check could not be performed; the license verification endpoint was unreachable."
        ]),

        new("Retry Pipeline", "ServiceControl Health",
        [
            "The retry batch processor has stale in-flight batches from a previous session that were not adopted.",
            "Retry forwarding queue is backing up; forwarded retries are not being acknowledged in time."
        ]),
    ];

    protected override async Task ExecuteCycleAsync(CancellationToken ct)
    {
        var failureProbability = options.Value.CustomCheckFailureProbability;
        // Clamp to a sane range regardless of config so a bad value can't produce 100% failures
        // (which would just look stuck) or always-pass reports.
        if (failureProbability < 0) failureProbability = 0;
        if (failureProbability > 0.95) failureProbability = 0.95;

        var endpointName = options.Value.ServiceControlInputQueue;
        var host = options.Value.CustomCheckHost;
        var destination = options.Value.ServiceControlInputQueue;
        var reportedAt = DateTimeOffset.UtcNow.UtcDateTime;

        foreach (var check in Checks)
        {
            ct.ThrowIfCancellationRequested();

            var failed = Random.Shared.NextDouble() < failureProbability;
            var reason = failed ? check.FailureReasons[Random.Shared.Next(check.FailureReasons.Length)] : null;

            var report = new ReportCustomCheckResult
            {
                HostId = check.HostId,
                CustomCheckId = check.Id,
                Category = check.Category,
                HasFailed = failed,
                FailureReason = reason,
                ReportedAt = reportedAt,
                EndpointName = endpointName,
                Host = host
            };

            var sendOptions = new SendOptions();
            // Route directly to the ServiceControl input queue — the queue ServiceControl itself
            // listens on for custom-check reports (and heartbeats). Defaults to the conventional
            // ServiceControl error instance name.
            sendOptions.SetDestination(destination);

            try
            {
                await session.Send(report, sendOptions, ct);

                AddItems(1);
                _reportedCounter.Add(1,
                    new KeyValuePair<string, object?>("check", check.Id),
                    new KeyValuePair<string, object?>("result", failed ? "fail" : "pass"));

                if (failed)
                {
                    metrics.AddCustomCheckFailures(1);
                    logger.LogInformation("Reported internal custom check failure: {Check} ({Category}) — {Reason}",
                        check.Id, check.Category, reason);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Most likely the ServiceControl input queue is unreachable (e.g. running the tool
                // standalone on the Learning transport with no ServiceControl). Log at debug to avoid
                // spamming; the job is a no-op in that environment.
                logger.LogDebug(ex, "Failed to send custom check report '{Check}' to {Destination}", check.Id, destination);
            }
        }
    }

    protected override void LogCycleError(Exception ex) => logger.LogWarning(ex, "Custom check failure cycle failed");

    /// <summary>An internal-looking custom check definition with a pool of plausible reasons.</summary>
    private sealed class InternalCheck
    {
        internal InternalCheck(string id, string category, string[] failureReasons)
        {
            Id = id;
            Category = category;
            FailureReasons = failureReasons;
            HostId = DeterministicGuid(id);
        }

        internal string Id { get; }
        internal string Category { get; }
        internal string[] FailureReasons { get; }

        // Deterministic, stable HostId per check id so ServiceControl tracks each check's state
        // across cycles (pass/fail transitions) rather than treating every report as a new check.
        internal Guid HostId { get; }

        private static Guid DeterministicGuid(string id)
        {
            // FNV-1a hash of the check id → 16 bytes → Guid. Stable across processes and restarts.
            uint h = 2166136261u;
            foreach (var b in System.Text.Encoding.UTF8.GetBytes("customcheck:" + id))
            {
                h ^= b;
                h *= 16777619u;
            }
            Span<byte> bytes = stackalloc byte[16];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes[..4], h);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..8], h ^ 0x85ebca6bu);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..12], h ^ 0xc2b2ae35u);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes[12..16], h ^ 0x27d4eb2fu);
            return new Guid(bytes);
        }
    }
}