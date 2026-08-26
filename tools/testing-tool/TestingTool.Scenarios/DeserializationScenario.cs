using System.Diagnostics;

namespace TestingTool.Scenarios;

/// <summary>
/// Deserialization failure scenario: messages with malformed payloads fail during deserialization.
/// Grouped by message type, simulating a deployment that shipped an incompatible schema version.
/// All messages fail (100%) since deserialization happens before the handler runs.
/// </summary>
public sealed class DeserializationScenario(string shardId) : ScenarioBase(shardId)
{
    public override string Name => "deserialization-failure";
    public override string Description => "Messages fail during deserialization due to incompatible schema. Grouped by message type — simulates a bad deployment.";
    public override string Category => "Deserialization";
    public override double DefaultRate => 20;

    public override bool ShouldFail(string messageId) => true;

    public override Exception CreateException() =>
        CreateException(
            "NServiceBus.MessageDeserializationException",
            "Unable to deserialize message: unexpected token at position 0. Expected a valid message envelope.",
            "deser:SampleCommand:v2-incompatible");
}