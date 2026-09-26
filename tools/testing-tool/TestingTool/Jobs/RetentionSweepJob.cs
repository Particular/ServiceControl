using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestingTool.Contracts;

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
/// Each cycle posts to <c>POST /api/maintenance/retention/purge</c>. The sweep runs in the
/// background on ServiceControl (the call returns as soon as the run is accepted), so a cycle
/// just kicks off a sweep and records the outcome; it does not wait for the delete work to
/// finish. If a sweep is still running when the next cycle fires, ServiceControl answers
/// <c>alreadyRunning</c> (HTTP 409) and the cycle is counted as a skip rather than a failure.
/// </para>
/// <para>
/// The cutoffs decide how old a row must be to be deleted. When a cutoff timespan is supplied
/// with the start request (the web UI exposes it as a field on the job card), every sweep runs
/// with <c>cutoff = now - timespan</c> for both the failed-message and event-log purges; when it
/// isn't, the request omits the cutoffs and ServiceControl derives them from its configured
/// retention periods, just as the scheduled hourly sweep does.
/// </para>
/// <para>
/// Only persisters that scan-and-delete aged rows register a sweeper (e.g. the EFCore SQL
/// persisters). RavenDB does not — its retention is the server-side <c>@expires</c> bundle
/// stamped per-document at write time — so against a RavenDB-backed instance ServiceControl
/// answers <c>notSupported</c> (HTTP 501). The job logs that once per cycle at debug and moves
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

    // Cutoff timespan supplied when the job was started (null = let ServiceControl derive the
    // cutoffs). Validated by TryConfigure before the run starts, so cycles only ever see null
    // or a positive timespan.
    private TimeSpan? _cutoffTimespan;

    public override string Name => "retention-sweep";
    public override string Description =>
        IsRunning && _cutoffTimespan is { } ts
            ? $"Trigger a manual retention sweep on ServiceControl each cycle (deletes failures and events older than {ts})."
            : "Trigger a manual retention sweep on ServiceControl each cycle (deletes aged failures and events).";
    public override string Category => "Recoverability";
    public override TimeSpan DefaultInterval => options.Value.RetentionSweepInterval;

    // Surfaced in the job snapshot so the UI can echo the active cutoff while the job runs.
    public override string? CutoffTimespan => IsRunning && _cutoffTimespan is { } ts ? ts.ToString() : null;

    protected override bool TryConfigure(StartJobRequest? request, out string? error)
    {
        // Absent/empty = no caller-supplied cutoffs; each sweep then uses the cutoffs
        // ServiceControl derives from its configured retention periods.
        if (request?.CutoffTimespan?.Trim() is not { Length: > 0 } raw)
        {
            _cutoffTimespan = null;
            error = null;
            return true;
        }

        if (!TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var ts) || ts <= TimeSpan.Zero)
        {
            error = $"CutoffTimespan must be a positive .NET timespan such as '14.00:00:00' (14 days) or '00:30:00' (30 minutes), but was '{raw}'";
            return false;
        }

        _cutoffTimespan = ts;
        error = null;
        return true;
    }

    protected override async Task ExecuteCycleAsync(CancellationToken ct)
    {
        // Derive the cutoffs for this cycle. A start-supplied cutoff timespan applies to both
        // the failed-message and event-log purges (cutoff = now - timespan), so the user
        // controls how aggressive every sweep is without touching ServiceControl's retention
        // settings.
        ServiceControlClient.RetentionPurgeRequest? request = null;
        if (_cutoffTimespan is { } ts)
        {
            // UtcNow minus a positive timespan is always UTC and in the past — the two
            // properties the purge API validates cutoffs against.
            var cutoff = DateTime.UtcNow - ts;
            request = new(cutoff, cutoff);
        }

        var response = await sc.PurgeRetentionAsync(request, ct);

        // A null response means the HTTP call itself failed — treat as a transient failure and
        // let the next cycle retry. Logging happens in ServiceControlClient; don't double-count.
        if (response is null)
        {
            logger.LogDebug("Retention sweep trigger failed (HTTP error); will retry next cycle");
            return;
        }

        // The status string is the API contract's outcome discriminator (SystemMaintenanceController
        // maps the RetentionPurgeStatus enum onto it): started / alreadyRunning / notSupported / error.
        switch (response.Status)
        {
            case "started":
                AddItems(1);
                metrics.AddRetentionSweepsStarted(1);
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", "started"));
                logger.LogInformation("Retention sweep started at {StartedAt:O} (errorCutoff={ErrorCutoff:O}, eventsCutoff={EventsCutoff:O})",
                    response.StartedAt, response.ErrorCutoff, response.EventsCutoff);
                break;

            case "alreadyRunning":
                // A sweep from a previous cycle is still in flight — expected when the interval is
                // shorter than the sweep duration. Not a failure; just skip this cycle.
                metrics.AddRetentionSweepsAlreadyRunning(1);
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", "alreadyRunning"));
                logger.LogDebug("Retention sweep already running; skipping cycle");
                break;

            case "notSupported":
                // The persister has no sweeper (e.g. RavenDB). Don't spam the logs every cycle.
                metrics.AddRetentionSweepsNotSupported(1);
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", "notSupported"));
                logger.LogDebug("Retention sweep not supported by the current storage: {Reason}", response.Reason);
                break;

            default:
                // "error" is the refused-cutoff outcome (HTTP 400). It shouldn't happen — the
                // cutoffs this job sends are always UTC and in the past — but surface anything
                // unexpected rather than swallowing it.
                _sweepCounter.Add(1, new KeyValuePair<string, object?>("outcome", response.Status ?? "unknown"));
                logger.LogWarning("Retention sweep returned unexpected status {Status}: {Reason}",
                    response.Status, response.Reason);
                break;
        }
    }

    protected override void LogCycleError(Exception ex) => logger.LogWarning(ex, "Retention sweep cycle failed");
}