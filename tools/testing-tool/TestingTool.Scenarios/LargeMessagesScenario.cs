using System.Runtime.CompilerServices;

namespace TestingTool.Scenarios;

/// <summary>
/// Large message scenario: every body is about 256 KiB, above the default 100 KB external body storage
/// threshold. A deterministic subset of messages (by hash) fails, so both the Audit and Error ingestion
/// paths store large bodies.
/// </summary>
public sealed class LargeMessagesScenario(string shardId) : ScenarioBase(shardId)
{
    public override string Name => "large-messages";
    public override string Description => "Publishes about 256 KiB message bodies and fails 20% of them. Exercises external body storage on the Audit and Error paths.";
    public override string Category => "Body";
    public override double DefaultRate => 3;
    public override int? BodySizeBytes => 256 * 1024;

    private const double FailureRatio = 0.20;

    public override bool ShouldFail(string messageId) => Hash(messageId) < FailureRatio;

    public override Exception CreateException() =>
        Capture(ProcessLargePayload, "large:payload-too-large");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessLargePayload() =>
        throw new InvalidDataException("The large message payload could not be processed.");
}
