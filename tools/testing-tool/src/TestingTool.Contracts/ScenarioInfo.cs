namespace TestingTool.Contracts;

/// <summary>
/// Describes a scenario that can generate error load against ServiceControl.
/// Returned by <c>GET /api/scenarios</c> and rendered in the web UI.
/// </summary>
public sealed class ScenarioInfo
{
    /// <summary>The stable, url-safe scenario name used in API paths.</summary>
    public required string Name { get; init; }

    /// <summary>A short human-readable description of the failure shape this scenario produces.</summary>
    public required string Description { get; init; }

    /// <summary>Human-readable category for grouping in the UI (e.g. "Outage", "Poison", "Noise").</summary>
    public required string Category { get; init; }

    /// <summary>Whether the scenario is currently emitting load.</summary>
    public bool Running { get; init; }

    /// <summary>Current target rate in messages/second (0 if idle).</summary>
    public double CurrentRate { get; init; }

    /// <summary>Errors emitted by this scenario since process start.</summary>
    public long ErrorsSent { get; init; }

    /// <summary>Default recommended rate in messages/second.</summary>
    public double DefaultRate { get; init; }

    /// <summary>Optional cooldown duration between failure bursts (ISO 8601 duration, null = continuous).</summary>
    public string? Cooldown { get; init; }
}