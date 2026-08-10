namespace ServiceControl.Persistence.EFCore.Entities;

/// <summary>
/// Records an in-flight (or completed) edit-and-retry of a failed message.
/// One row per failed message, keyed by the failed message's <see cref="UniqueMessageId"/>,
/// mirroring the RavenDB <c>FailedMessageEdit</c> document which is keyed
/// <c>FailedMessageEdit/{uniqueMessageId}</c>.
/// </summary>
public class FailedMessageEditEntity
{
    /// <summary>
    /// The unique message id of the failed message being edited. Acts as the primary key,
    /// giving 1:1 semantics with the failed message and making
    /// <c>GetCurrentEditingRequestId</c> a point lookup by key.
    /// </summary>
    public Guid UniqueMessageId { get; set; }

    /// <summary>
    /// The NServiceBus message id of the <c>EditAndSend</c> message that initiated the edit.
    /// Used as the concurrency guard / idempotency token: a subsequent <c>EditAndSend</c>
    /// for the same failed message is discarded when its message id differs from this value.
    /// </summary>
    public required string EditId { get; set; }
}