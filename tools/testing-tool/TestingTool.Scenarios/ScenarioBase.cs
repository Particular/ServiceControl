using System.Diagnostics;

namespace TestingTool.Scenarios;

/// <summary>
/// Base class providing common functionality for scenarios: deterministic hashing for per-shard
/// failure decisions, activity source management, and exception tagging.
/// </summary>
public abstract class ScenarioBase : IScenario
{
    private readonly string _shardId;

    protected ScenarioBase(string shardId)
    {
        _shardId = shardId;
        ActivitySource = new ActivitySource($"testing-tool.{Name}");
    }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Category { get; }
    public virtual double DefaultRate => 10;
    public ActivitySource ActivitySource { get; }
    public virtual TimeSpan? Cooldown => null;

    public abstract bool ShouldFail(string messageId);
    public abstract Exception CreateException();

    /// <summary>Deterministic hash of a message id + shard id, returning a value in [0, 1).</summary>
    protected double Hash(string messageId)
    {
        var combined = $"{_shardId}:{messageId}";
        // Simple FNV-1a hash — no extra NuGet dependency required.
        uint hash = 2166136261u;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(combined))
        {
            hash ^= b;
            hash *= 16777619u;
        }
        return hash / (double)uint.MaxValue;
    }

    /// <summary>Creates an exception with correlation tags that ServiceControl uses for grouping.</summary>
    protected static Exception CreateException(string type, string message, string correlationGroup)
    {
        return new ScenarioException(type, message, correlationGroup);
    }
}