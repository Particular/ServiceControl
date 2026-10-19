using NServiceBus;

namespace TestingTool.Auditing;

/// <summary>
/// Carrier message for the direct audit-queue writer. It is never handled by anything: the whole
/// wire message (headers, body and <c>NServiceBus.EnclosedMessageTypes</c>) is replaced by
/// <see cref="RawAuditPayloadBehavior"/>, because an audit envelope has to look like a message some
/// other endpoint processed rather than one the testing tool sent.
/// </summary>
/// <remarks>
/// It implements <see cref="IMessage"/> because nothing handles it: NServiceBus infers message
/// types from handled types, and refuses to send a type it has no metadata for.
/// </remarks>
public class AuditEnvelope : IMessage
{
    /// <summary>Monotonically increasing sequence number within a generation run.</summary>
    public long Sequence { get; set; }
}
