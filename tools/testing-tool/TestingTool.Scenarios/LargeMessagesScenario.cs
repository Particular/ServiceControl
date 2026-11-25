using System.Runtime.CompilerServices;

namespace TestingTool.Scenarios;

/// <summary>
/// Exercises external body storage on the Audit and Error paths.
/// JSON string escaping expands the generated body; leave room for headers under Service Bus Standard's 256 KiB limit.
/// </summary>
public sealed class LargeMessagesScenario(string shardId) : ScenarioBase(shardId)
{
    public override string Name => "large-messages";
    public override string Description => "Publishes about 190 KiB serialized message bodies and fails 20% of them. Exercises external body storage on the Audit and Error paths.";
    public override string Category => "Body";
    public override double DefaultRate => 3;
    public override int? BodySizeBytes => 150 * 1024;

    private const double FailureRatio = 0.20;

    public override bool ShouldFail(string messageId) => Hash(messageId) < FailureRatio;

    public override Exception CreateException() =>
        Capture(ProcessLargePayload, "large:payload-too-large");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProcessLargePayload() =>
        throw new InvalidDataException("The large message payload could not be processed.");
}
