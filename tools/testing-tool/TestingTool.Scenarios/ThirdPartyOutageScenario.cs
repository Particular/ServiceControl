using System.Diagnostics;

namespace TestingTool.Scenarios;

/// <summary>
/// Simulates a third-party service outage: 100% of messages fail for a burst period, then recover.
/// All failures share the same downstream host header so ServiceControl groups them as one
/// "third-party outage" error group. After the burst, messages succeed (cooldown), then the
/// cycle repeats — modelling a flaky external dependency.
/// </summary>
public sealed class ThirdPartyOutageScenario(string shardId) : ScenarioBase(shardId)
{
    public override string Name => "third-party-outage";
    public override string Description => "Simulates a third-party service outage: 100% fail during burst, then recovers. Errors grouped by downstream host.";
    public override string Category => "Outage";
    public override double DefaultRate => 50;
    public override TimeSpan? Cooldown => TimeSpan.FromSeconds(30);

    // Burst window: fail for 20s, then cooldown 30s, repeat.
    private static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CyclePeriod = BurstWindow + TimeSpan.FromSeconds(30);

    public override bool ShouldFail(string messageId)
    {
        var now = DateTimeOffset.UtcNow;
        var phase = now.Ticks % CyclePeriod.Ticks;
        return phase < BurstWindow.Ticks;
    }

    public override Exception CreateException() =>
        CreateException(
            "System.Net.Http.HttpRequestException",
            "The third-party service at https://api.downstream.example.com did not respond within the timeout period.",
            "downstream:api.downstream.example.com");
}