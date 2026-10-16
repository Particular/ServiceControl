using NServiceBus.Pipeline;

namespace TestingTool.Auditing;

/// <summary>
/// Applies a <see cref="RawAuditPayload"/> attached to a send, replacing the serialized body and
/// the headers the outgoing pipeline produced.
/// </summary>
/// <remarks>
/// This runs at the physical stage on purpose. Headers set through <c>SendOptions.SetHeader</c> are
/// applied earlier, so the serializer's <c>NServiceBus.EnclosedMessageTypes</c> and
/// <c>AttachSenderRelatedInfoOnMessageBehavior</c>'s <c>NServiceBus.TimeSent</c> would overwrite
/// them. ServiceControl routes an audit message to its saga branch only when
/// <c>EnclosedMessageTypes</c> is exactly <c>SagaUpdatedMessage</c>'s unqualified full name, which
/// the serializer never writes.
/// </remarks>
public sealed class RawAuditPayloadBehavior : Behavior<IOutgoingPhysicalMessageContext>
{
    public override Task Invoke(IOutgoingPhysicalMessageContext context, Func<Task> next)
    {
        if (context.Extensions.TryGet<RawAuditPayload>(out var payload))
        {
            foreach (var (key, value) in payload.Headers)
            {
                context.Headers[key] = value;
            }

            context.UpdateMessage(payload.Body);
        }

        return next();
    }
}
