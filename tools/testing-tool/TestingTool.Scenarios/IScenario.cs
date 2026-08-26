using System.Diagnostics;

namespace TestingTool.Scenarios;

/// <summary>
/// Defines a real-world error scenario that produces grouped failures against ServiceControl.
/// Each implementation models a distinct failure shape (outage, poison message, timeout spike, etc.)
/// so that ServiceControl naturally groups the resulting errors.
/// </summary>
public interface IScenario
{
    /// <summary>The stable scenario name, matching <see cref="Contracts.ScenarioInfo.Name"/>.</summary>
    string Name { get; }

    /// <summary>Human-readable description of the failure shape.</summary>
    string Description { get; }

    /// <summary>UI grouping category (e.g. "Outage", "Poison", "Noise").</summary>
    string Category { get; }

    /// <summary>Default recommended emission rate in messages/second.</summary>
    double DefaultRate { get; }

    /// <summary>Activity source used to emit OpenTelemetry traces for this scenario's work.</summary>
    ActivitySource ActivitySource { get; }

    /// <summary>Determines whether a given message should fail. Must be deterministic per shard.</summary>
    bool ShouldFail(string messageId);

    /// <summary>Creates the grouped, typed exception emitted when <see cref="ShouldFail"/> returns true.</summary>
    Exception CreateException();

    /// <summary>Burst shape: optional cooldown between failure bursts.</summary>
    TimeSpan? Cooldown { get; }
}