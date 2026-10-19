namespace TestingTool.Auditing;

/// <summary>
/// The wire message the direct audit-queue writer wants on the queue, attached to the outgoing
/// send through the send options context bag and applied by <see cref="RawAuditPayloadBehavior"/>.
/// </summary>
/// <param name="Headers">Headers to set on the outgoing message, overwriting anything the pipeline produced.</param>
/// <param name="Body">The serialized body to send in place of the carrier message's body.</param>
public sealed record RawAuditPayload(IReadOnlyDictionary<string, string> Headers, ReadOnlyMemory<byte> Body);
