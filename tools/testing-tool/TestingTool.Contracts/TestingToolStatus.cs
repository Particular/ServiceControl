namespace TestingTool.Contracts;

/// <summary>
/// Live snapshot of testing-tool counters, returned by <c>GET /api/status</c> and used as the
/// container liveness/readiness probe target.
/// </summary>
public sealed class TestingToolStatus
{
    /// <summary>Whether the tool is ready to accept scenario control requests.</summary>
    public bool Ready { get; init; }

    /// <summary>Total errors emitted since process start (handler + bypass paths).</summary>
    public long ErrorsSent { get; init; }

    /// <summary>Total error messages replayed since process start.</summary>
    public long ErrorsReplayed { get; init; }

    /// <summary>Total error messages archived since process start.</summary>
    public long ErrorsArchived { get; init; }

    /// <summary>Total ServiceControl searches executed since process start.</summary>
    public long SearchesExecuted { get; init; }

    /// <summary>
    /// Retention sweeps successfully started (202) since process start. Each corresponds to a
    /// full scan-and-delete of aged rows accepted by the ServiceControl error instance.
    /// </summary>
    public long RetentionSweepsStarted { get; init; }

    /// <summary>
    /// Retention-sweep triggers that found a sweep already running (409) since process start.
    /// Non-zero on intervals shorter than the sweep duration — the job skipped the cycle.
    /// </summary>
    public long RetentionSweepsAlreadyRunning { get; init; }

    /// <summary>
    /// Retention-sweep triggers the persister did not support (501) since process start. A
    /// non-zero value means ServiceControl is backed by a persister with no sweeper (e.g.
    /// RavenDB, whose retention is server-side document expiration) — the job is a no-op there.
    /// </summary>
    public long RetentionSweepsNotSupported { get; init; }

    /// <summary>Total errors emitted via the direct error-queue bypass writer since process start.</summary>
    public long BypassErrorsWritten { get; init; }

    /// <summary>Total bypass sends that failed since process start. Non-zero values indicate
    /// transport/broker issues on the bypass path — the handler path may be unaffected.</summary>
    public long BypassErrorsFailed { get; init; }

    /// <summary>
    /// Total internal-looking custom check failures reported to ServiceControl since process start
    /// (via the custom-check-failures job). Each failure is a <c>ReportCustomCheckResult</c> sent
    /// to the ServiceControl input queue with <c>HasFailed</c> = true.
    /// </summary>
    public long CustomCheckFailures { get; init; }

    /// <summary>The shard id this replica owns, used for disjoint scenario slices when scaled out.</summary>
    public string? ShardId { get; init; }

    /// <summary>Number of scenarios currently running.</summary>
    public int ActiveScenarios { get; init; }

    /// <summary>Number of recoverability/search jobs currently running.</summary>
    public int ActiveJobs { get; init; }

    /// <summary>Aggregate current emission rate across all running scenarios (msgs/sec).</summary>
    public double CurrentRate { get; init; }

    /// <summary>ServiceControl API URL the tool is targeting.</summary>
    public string? ServiceControlUrl { get; init; }

    /// <summary>Uptime since process start (formatted string).</summary>
    public string? Uptime { get; init; }
}