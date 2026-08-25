namespace TestingTool.Scenarios;

/// <summary>
/// A load-generation message processed by <c>FailingMessageHandler</c>. The <c>ScenarioName</c>
/// header (set by the sender) determines which scenario's failure logic applies. The payload
/// is intentionally minimal — the testing tool generates volume, not realistic business data.
/// </summary>
public class LoadMessage
{
    /// <summary>Monotonically increasing sequence number within a generation run.</summary>
    public long Sequence { get; set; }

    /// <summary>Random payload bytes to give messages some size variety.</summary>
    public byte[]? Payload { get; set; }
}