namespace TestingTool;

/// <summary>
/// Configuration options for the testing tool, bound from the "TestingTool" config section
/// and/or environment variables.
/// </summary>
public sealed class TestingToolOptions
{
    /// <summary>Base URL of the ServiceControl instance under test (e.g. http://servicecontrol:33333).</summary>
    public string ServiceControlApiUrl { get; set; } = "http://localhost:33333";

    /// <summary>Interval between retry cycles.</summary>
    public TimeSpan ReplayInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Minimum number of messages in a group before it is retried.</summary>
    public int ReplayMinGroupSize { get; set; } = 1;

    /// <summary>Interval between search cycles.</summary>
    public TimeSpan SearchInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Interval between archive cycles for the recoverability archive job.</summary>
    public TimeSpan ArchiveInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Minimum number of messages in a group before it is archived.</summary>
    public int ArchiveMinGroupSize { get; set; } = 1;

    /// <summary>
    /// Default interval between manual retention-sweep cycles. A retention sweep performs a full
    /// scan-and-delete of aged rows on the ServiceControl error instance, so it is heavier than
    /// the retry/archive/search cycles and defaults to a longer interval. Only persisters that
    /// implement the sweeper (e.g. the EFCore SQL persisters) honour it; RavenDB-backed instances
    /// report <c>not-supported</c> and the job logs that and moves on.
    /// </summary>
    public TimeSpan RetentionSweepInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>NServiceBus error queue name that ServiceControl monitors.</summary>
    public string ErrorQueueName { get; set; } = "error";

    /// <summary>Whether to start the background-noise scenario automatically on startup.</summary>
    public bool AutoStartBackgroundNoise { get; set; } = false;

    /// <summary>
    /// The ServiceControl error instance's own input queue — the queue ServiceControl listens on
    /// for custom-check reports (and heartbeats). The custom-check-failures job sends
    /// <c>ReportCustomCheckResult</c> messages here over the transport. Defaults to the
    /// conventional ServiceControl error instance name.
    /// </summary>
    public string ServiceControlInputQueue { get; set; } = "Particular.ServiceControl";

    /// <summary>
    /// Default interval between custom-check-failure job cycles. Each cycle emits a fresh
    /// pass/fail report for every internal-looking check, so the ServicePulse Custom Checks view
    /// shows the checks flipping state. A short default keeps the injected failures responsive.
    /// </summary>
    public TimeSpan CustomCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The display name used for the <c>Host</c> field on injected custom-check reports, so they
    /// read as coming from the ServiceControl host itself rather than an external endpoint.
    /// </summary>
    public string CustomCheckHost { get; set; } = "ServiceControl";

    /// <summary>
    /// Probability (0.0–1.0) that any given internal-looking custom check is reported as failed on
    /// a cycle. The rest are reported as passed, producing a realistic mix of failures. Clamped to
    /// [0, 0.95] at runtime so a misconfiguration can't produce 100% failures.
    /// </summary>
    public double CustomCheckFailureProbability { get; set; } = 0.4;
}