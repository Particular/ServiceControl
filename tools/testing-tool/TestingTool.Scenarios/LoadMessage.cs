namespace TestingTool.Scenarios;

/// <summary>
/// A load-generation message processed by <c>FailingMessageHandler</c>. The <c>ScenarioName</c>
/// header (set by the sender) determines which scenario's failure logic applies. The
/// <see cref="TextBody"/> carries a plausible JSON body (≈3–6 KB) seeded with searchable terms so
/// that ServiceControl's full-text search index has real content to exercise — see
/// <see cref="MessageTextGenerator"/>.
/// </summary>
public class LoadMessage
{
    /// <summary>Monotonically increasing sequence number within a generation run.</summary>
    public long Sequence { get; set; }

    /// <summary>
    /// A plausible JSON body (approximately 3–6 KB) containing searchable terms drawn from
    /// <see cref="MessageTextGenerator.SearchableTerms"/>. ServiceControl indexes the message
    /// body for full-text search, so these terms become queryable via <c>/api/errors/search</c>.
    /// </summary>
    public string? TextBody { get; set; }
}