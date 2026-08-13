#nullable enable
namespace ServiceControl.Persistence;

using System.Threading;
using System.Threading.Tasks;
using ServiceControl.MessageFailures;

public interface IEditFailedMessagesDataStore
{
    /// <summary>
    /// Gets the edit request that currently owns the failed message, if any.
    /// This query is advisory; callers must use <see cref="TryBeginEdit"/> to acquire an edit safely.
    /// </summary>
    Task<string?> GetCurrentEditingRequestId(string failedMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims an unresolved failed message and marks the original failure resolved.
    /// An acquired result is committed before this method returns. Non-acquired results do not
    /// modify the failed message.
    /// </summary>
    /// <remarks>
    /// <para><see cref="BeginEditOutcome.Acquired"/> returns the failed-message snapshot and no existing edit ID.</para>
    /// <para><see cref="BeginEditOutcome.AlreadyAcquiredByThisEdit"/> returns the failed-message snapshot and the supplied edit ID.</para>
    /// <para><see cref="BeginEditOutcome.AcquiredByAnotherEdit"/> returns the winning edit ID and no failed-message snapshot.</para>
    /// <para><see cref="BeginEditOutcome.MessageNotFound"/> and <see cref="BeginEditOutcome.MessageNotUnresolved"/> return neither optional value.</para>
    /// </remarks>
    Task<BeginEditResult> TryBeginEdit(string failedMessageId, string editingMessageId, CancellationToken cancellationToken = default);
}

public enum BeginEditOutcome
{
    Acquired,
    AcquiredByAnotherEdit,
    MessageNotFound,
    MessageNotUnresolved
}

/// <summary>
/// The result of attempting to acquire a failed message for editing.
/// </summary>
/// <param name="Outcome">The acquisition outcome.</param>
/// <param name="FailedMessage">The snapshot used to dispatch an acquired or idempotently reacquired edit.</param>
/// <param name="ExistingEditId">The existing claim for an idempotent retry or competing edit.</param>
public sealed record BeginEditResult(BeginEditOutcome Outcome, FailedMessage? FailedMessage = null, string? ExistingEditId = null);
