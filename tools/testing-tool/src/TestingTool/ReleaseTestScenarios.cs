using TestingTool.Contracts;
using TestingTool.Scenarios;

namespace TestingTool;

/// <summary>
/// Maps release-test scenario names (from <c>docs/testing-scenarios.md</c>) to testing-tool
/// scenarios so they can be kicked off manually from the web UI or API. This satisfies the
/// optional requirement: "any scenarios from the release tests should be considered to kick off
/// manually."
/// </summary>
public static class ReleaseTestScenarios
{
    /// <summary>
    /// Release-test scenario presets. Each preset maps a release-test checklist item to a
    /// testing-tool scenario with a recommended rate and duration.
    /// </summary>
    public static readonly IReadOnlyList<ReleaseTestPreset> Presets =
    [
        new("retry-single-message", "third-party-outage",
            "Recoverability — Retry single message: generate a small batch of grouped errors for single-message retry testing.",
            5, 10),

        new("retry-message-group", "third-party-outage",
            "Recoverability — Retry message group: generate a larger batch of grouped errors for group retry testing.",
            50, 20),

        new("ingestion-load", "background-noise",
            "Ingestion — High message load: continuous background errors to test ingestion throughput.",
            100, null),

        new("chaos-testing", "background-noise",
            "Chaos testing — Continuous low-level errors while stopping/killing ServiceControl processes.",
            10, null),

        new("performance-clean-db", "third-party-outage",
            "Performance — Clean database: burst of errors to test ingestion with an empty RavenDB.",
            200, 60),

        new("poison-retry-storm", "poison-message",
            "Recoverability — Poison message retry storm: deterministic always-fail messages.",
            20, 30),

        new("timeout-batch", "timeout-spike",
            "Recoverability — Timeout spike: oscillating timeout failures grouped by batch bucket.",
            30, 60),

        new("deserialization-bad-deploy", "deserialization-failure",
            "Recoverability — Bad deployment: 100% deserialization failures grouped by message type.",
            50, 15),
    ];

    /// <summary>Find a preset by its release-test name (case-insensitive).</summary>
    public static ReleaseTestPreset? Find(string name) =>
        Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// A named release-test preset that maps to a testing-tool scenario with recommended parameters.
/// </summary>
public sealed record ReleaseTestPreset(
    string Name,
    string ScenarioName,
    string Description,
    double Rate,
    int? DurationSeconds);