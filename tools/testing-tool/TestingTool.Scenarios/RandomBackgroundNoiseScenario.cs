using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TestingTool.Scenarios;

/// <summary>
/// Low baseline error rate that is always on. Produces a small, steady stream of random
/// exceptions to simulate real-world background noise. Used to keep ServiceControl's
/// ingestion pipeline warm between explicit scenario runs.
/// </summary>
public sealed class RandomBackgroundNoiseScenario(string shardId) : ScenarioBase(shardId)
{
    public override string Name => "background-noise";
    public override string Description => "Always-on low baseline error rate (≈3%). Simulates real-world background noise to keep ingestion warm.";
    public override string Category => "Noise";
    public override double DefaultRate => 15;

    private const double NoiseRate = 0.03;

    public override bool ShouldFail(string messageId) => Hash(messageId) < NoiseRate;

    // Rotate through a few exception types, each thrown from its own method, so we get a handful
    // of small groups.
    private static readonly (Action Fail, string Group)[] Failures =
    [
        (DereferenceNull, "noise:nre"),
        (IndexPastEnd, "noise:oor"),
        (ParseInput, "noise:fmt"),
        (CastValue, "noise:cast"),
    ];

    public override Exception CreateException()
    {
        var idx = (int)(Hash(Guid.NewGuid().ToString("N")) * Failures.Length) % Failures.Length;
        var (fail, group) = Failures[idx];
        return Capture(fail, group);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DereferenceNull() =>
        throw new NullReferenceException("Object reference not set to an instance of an object.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void IndexPastEnd() =>
        throw new IndexOutOfRangeException("Index was outside the bounds of the array.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ParseInput() =>
        throw new FormatException("The input string was not in a correct format.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CastValue() =>
        throw new InvalidCastException("Unable to cast object of type 'System.String' to type 'System.Int32'.");
}