namespace TestingTool.Contracts;

/// <summary>
/// Request body for <c>POST /api/scenarios/{name}/start</c>.
/// All fields are optional; defaults are taken from the scenario definition.
/// </summary>
public sealed class StartScenarioRequest
{
    /// <summary>Target emission rate in messages/second. Defaults to the scenario's <see cref="ScenarioInfo.DefaultRate"/>.</summary>
    public double? Rate { get; init; }

    /// <summary>Optional auto-stop duration in seconds. Null/0 = run until explicitly stopped.</summary>
    public double? DurationSeconds { get; init; }
}