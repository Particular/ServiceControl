using System.Diagnostics;

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

    public override Exception CreateException()
    {
        // Rotate through a few exception types so we get a handful of small groups.
        var types = new[]
        {
            ("System.NullReferenceException", "Object reference not set to an instance of an object.", "noise:nre"),
            ("System.IndexOutOfRangeException", "Index was outside the bounds of the array.", "noise:oor"),
            ("System.FormatException", "The input string was not in a correct format.", "noise:fmt"),
            ("System.InvalidCastException", "Unable to cast object of type 'System.String' to type 'System.Int32'.", "noise:cast"),
        };

        var idx = (int)(Hash(Guid.NewGuid().ToString("N")) * types.Length) % types.Length;
        var (type, msg, group) = types[idx];
        return CreateException(type, msg, group);
    }
}