#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads the retry-issued messages and the messages that hold a retry claim, which are copied while ServiceControl is closed: every
/// retry-issued message, whether or not its retry claim remains, and then every other message that still holds a retry claim,
/// whatever its status. The unresolved messages without a claim are <see cref="UnresolvedFailedMessagesReader" />'s.
/// The required copy's stall watchdog bounds its stale-index wait; it does not switch to a full scan on an index-readiness timeout.
/// </summary>
sealed class FailedMessagesReader(RavenReadOnlySourceLifecycle lifecycle) : FailedMessageStatusReader(lifecycle, MigrationCategoryRegistry.RetryIssuedStatuses, indexBudget: null)
{
    public override string CategoryId => MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages;

    /// <summary>
    /// Streams the retry-issued messages through <see cref="FailedMessageStatusReader.ReadStatuses" />, then the claim holders,
    /// whose cursors start with <see cref="FailedMessageStream.ClaimHolderCursorPrefix" />. A resume from such a cursor skips the first part.
    /// </summary>
    public override async IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = new FailedMessageStream(Lifecycle);
        var inClaimHolders = resumeAfter?.StartsWith(FailedMessageStream.ClaimHolderCursorPrefix, StringComparison.Ordinal) == true;

        if (!inClaimHolders)
        {
            await foreach (var batch in ReadStatuses(resumeAfter, batchSize, cancellationToken))
            {
                yield return await stream.ResolveRetryClaims(batch, cancellationToken);
            }
        }

        var claimHoldersAfter = inClaimHolders ? resumeAfter![FailedMessageStream.ClaimHolderCursorPrefix.Length..] : null;

        await foreach (var batch in stream.ClaimHolders(CategoryId, claimHoldersAfter, batchSize, cancellationToken))
        {
            yield return batch;
        }
    }
}
