using System.Diagnostics;

namespace TestingTool.Scenarios;

/// <summary>
/// Intermittent timeout spikes: a configurable percentage of messages fail with
/// <see cref="TimeoutException"/>, correlated by batch id so ServiceControl groups them
/// into timeout-related error groups. The failure rate oscillates to simulate periodic spikes.
/// </summary>
public sealed class TimeoutSpikeScenario(string shardId) : ScenarioBase(shardId)
{
    public override string Name => "timeout-spike";
    public override string Description => "Intermittent timeout exceptions with correlated batch ids. Failure rate oscillates to simulate spikes.";
    public override string Category => "Timeout";
    public override double DefaultRate => 30;

    // Spike every ~60s, lasting ~15s at elevated rate.
    private static readonly TimeSpan SpikeWindow = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan SpikeCycle = TimeSpan.FromSeconds(60);

    public override bool ShouldFail(string messageId)
    {
        var now = DateTimeOffset.UtcNow;
        var phase = now.Ticks % SpikeCycle.Ticks;
        var inSpike = phase < SpikeWindow.Ticks;

        // Base 10% fail rate, spikes to ~70% during spike window.
        var threshold = inSpike ? 0.70 : 0.10;
        return Hash(messageId) < threshold;
    }

    public override Exception CreateException()
    {
        // Correlate by 5-minute bucket so timeouts cluster into time-based groups.
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 300;
        return CreateException(
            "System.TimeoutException",
            "The operation has timed out waiting for a response from the downstream service.",
            $"timeout-batch:{bucket}");
    }
}