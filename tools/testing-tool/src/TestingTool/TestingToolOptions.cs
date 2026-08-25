namespace TestingTool;

/// <summary>
/// Configuration options for the testing tool, bound from the "TestingTool" config section
/// and/or environment variables.
/// </summary>
public sealed class TestingToolOptions
{
    /// <summary>Base URL of the ServiceControl instance under test (e.g. http://servicecontrol:33333).</summary>
    public string ServiceControlApiUrl { get; set; } = "http://localhost:33333";

    /// <summary>Whether the background replay job is enabled.</summary>
    public bool ReplayEnabled { get; set; } = false;

    /// <summary>Interval between replay cycles.</summary>
    public TimeSpan ReplayInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Minimum number of messages in a group before it is replayed.</summary>
    public int ReplayMinGroupSize { get; set; } = 1;

    /// <summary>Whether the background search job is enabled.</summary>
    public bool SearchEnabled { get; set; } = false;

    /// <summary>Interval between search cycles.</summary>
    public TimeSpan SearchInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>NServiceBus error queue name that ServiceControl monitors.</summary>
    public string ErrorQueueName { get; set; } = "error";

    /// <summary>Whether to start the background-noise scenario automatically on startup.</summary>
    public bool AutoStartBackgroundNoise { get; set; } = false;
}