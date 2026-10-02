using System.Diagnostics;

namespace TestingTool.Scenarios;

/// <summary>
/// Base class providing common functionality for scenarios: deterministic hashing for per-shard
/// failure decisions, activity source management, and capturing thrown scenario exceptions.
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

    /// <summary>
    /// Invokes <paramref name="fail"/> (which must throw) and returns the caught exception with a real
    /// stack trace. ServiceControl's default grouping is exception type + first stack frame, so each
    /// distinct failure should throw from its own method. The correlation group is attached as
    /// <see cref="Exception.Data"/> for telemetry only — ServiceControl does not group by it.
    /// </summary>
    protected static Exception Capture(Action fail, string correlationGroup)
    {
        try
        {
            fail();
        }
        catch (Exception ex)
        {
            ex.Data[CorrelationGroupKey] = correlationGroup;
            return ex;
        }

        throw new UnreachableException("Scenario failure method did not throw.");
    }

    /// <summary>Reads the correlation group attached by <see cref="Capture"/>, if any.</summary>
    public static string? GetCorrelationGroup(Exception ex) => ex.Data[CorrelationGroupKey] as string;

    private const string CorrelationGroupKey = "TestingTool.CorrelationGroup";
}