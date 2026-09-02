using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TestingTool.Jobs;

/// <summary>
/// Recoverability job that triggers a manual retention sweep on the ServiceControl error
/// instance on every cycle. This exercises ServiceControl's retention pipeline — the full
/// scan-and-delete of aged failed messages and event-log rows — against the load the other
/// scenarios/jobs are producing. Controllable from the web UI like the retry, archive and
/// search jobs.
/// </summary>
/// <remarks>
/// <para>
/// The sweep runs in the background on ServiceControl (the <c>POST /api/retention/sweep</c>
/// endpoint returns as soon as the run is accepted). Each cycle therefore just kicks off a
/// sweep and records the outcome; it does not wait for the delete work to finish. If a sweep
/// is still running when the next cycle fires, ServiceControl reports
/// <c>already-running</c> (HTTP 409) and the cycle is counted as a skip rather than a failure.
/// </para>
/// <para>
/// Only persisters that scan-and-delete aged rows register a sweeper (e.g. the EFCore SQL
/// persisters). RavenDB does not — its retention is the server-side <c>@expires</c> bundle
/// stamped per-document at write time — so against a RavenDB-backed instance ServiceControl
/// returns <c>not-supported</c> (HTTP 501). The job logs that once per cycle at debug and moves
/// on rather than treating it as an error.
/// </para>
/// </remarks>
public sealed class RetentionSweepJob(
    ServiceControlClient sc,
    TestingToolMetrics metrics,
    IOptions<TestingToolOptions> options,
    Meter meter,
    ILogger<RetentionSweepJob> logger) : JobBase("testing-tool.retention-sweep")
{
    private readonly Counter<long> _sweepCounter = meter.CreateCounter<long>("retention_sweeps_total");

    public override string Name => "retention-sweep";
    public override string Description => "Trigger a manual retention sweep on ServiceControl each cycle (deletes aged failures and events).";
    public override string Category => "Recoverability";
    public override TimeSpan DefaultInterval => options.Value.RetentionSweepInterval;

    protected override async Task ExecuteCycleAsync(CancellationToken ct)
    {
        // Kick off a sweep with default cutoffs (now - retention period), derived inside
        // ServiceControl just as the scheduled hourly sweep derives them.
        var response = await sc.SweepRetentionAsync(ct);

        // A null response means the HTTP call itself failed — treat as a transient failure and
        // let the next cycle retry. Logging happens in ServiceControlClient; don't double-count.
        if (response is null)
        {
            logger.LogDebug("Retention sweep trigger failed (HTTP error); will retry next cycle");
            return;
        }

        // The status string is the API contract's outcome discriminator (see
        // RetentionApi/RetentionController): started / already-running / maintenance /
        // not-supported / invalid-cutoff.
        switch (response.Status)
        {
            case "started":
                AddItems(1);
                metrics.AddRetentionSweepsStarted(1);
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", "started"));
                logger.LogInformation("Retention sweep started at {StartedAt:O} (errorCutoff={ErrorCutoff:O}, eventsCutoff={EventsCutoff:O})",
                    response.StartedAt, response.ErrorCutoff, response.EventsCutoff);
                break;

            case "already-running":
                // A sweep from a previous cycle is still in flight — expected when the interval is
                // shorter than the sweep duration. Not a failure; just skip this cycle.
                metrics.AddRetentionSweepsAlreadyRunning(1);
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", "already-running"));
                logger.LogDebug("Retention sweep already running; skipping cycle");
                break;

            case "not-supported":
                // The persister has no sweeper (e.g. RavenDB). Don't spam the logs every cycle.
                metrics.AddRetentionSweepsNotSupported(1);
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", "not-supported"));
                logger.LogDebug("Retention sweep not supported by the current storage: {Reason}", response.Reason);
                break;

            case "maintenance":
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", "maintenance"));
                logger.LogDebug("Retention sweep refused — ServiceControl is in maintenance mode");
                break;

            default:
                // invalid-cutoff shouldn't happen (we send no cutoffs), but surface anything unexpected.
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", response.Status ?? "unknown"));
                logger.LogWarning("Retention sweep returned unexpected status {Status}: {Reason}",
                    response.Status, response.Reason);
                break;
        }
    }

    protected override void LogCycleError(Exception ex) => logger.LogWarning(ex, "Retention sweep cycle failed");
}