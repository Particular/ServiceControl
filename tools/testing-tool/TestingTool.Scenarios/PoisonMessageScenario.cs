using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace TestingTool.Scenarios;

/// <summary>
/// Poison message scenario: a deterministic subset of messages (by hash) always fails.
/// These messages will never succeed on retry, creating a persistent error group that
/// exercises ServiceControl's retry-storm handling and message archival flows.
/// </summary>
public sealed class PoisonMessageScenario(string shardId) : ScenarioBase(shardId)
{
    public override string Name => "poison-message";
    public override string Description => "Deterministic poison messages that always fail on retry. Exercises retry-storm and archival handling.";
    public override string Category => "Poison";
    public override double DefaultRate => 5;

    // ~15% of messages are poison (always fail).
    private const double PoisonRatio = 0.15;

    public override bool ShouldFail(string messageId) => Hash(messageId) < PoisonRatio;

    public override Exception CreateException() =>
        Capture(ProcessPayload, "poison:invalid-payload");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessPayload() =>
        throw new InvalidOperationException("The message payload is corrupt and cannot be processed. This message will always fail.");
}