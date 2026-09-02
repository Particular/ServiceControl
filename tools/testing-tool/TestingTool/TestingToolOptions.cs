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
}