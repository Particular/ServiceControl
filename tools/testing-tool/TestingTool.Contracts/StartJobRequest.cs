namespace TestingTool.Contracts;

/// <summary>
/// Request body for <c>POST /api/jobs/{name}/start</c>.
/// All fields are optional; defaults are taken from the job definition.
/// </summary>
public sealed class StartJobRequest
{
    /// <summary>Cycle interval in seconds. Defaults to the job's default interval.</summary>
    public double? IntervalSeconds { get; init; }
}