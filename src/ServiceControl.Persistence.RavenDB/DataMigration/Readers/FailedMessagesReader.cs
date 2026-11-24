#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads what is copied while ServiceControl is closed: every retry-issued message, every message that holds a retry claim whatever
/// its status and age, and the recent errors, which are the messages of any status last modified at or after the newest unresolved
/// message's last-modified time less one hour. With no unresolved message there are no recent errors.
/// ServiceControl is stopped during the copy, but RavenDB expiration can remove the newest unresolved message and change the cutoff between reads.
/// A message is read once, and every row says whether RavenDB still holds its retry claim.
/// The older unresolved messages without a claim are <see cref="UnresolvedFailedMessagesReader" />'s.
/// </summary>
sealed class FailedMessagesReader(RavenReadOnlySourceLifecycle lifecycle) : FailedMessageStatusReader(lifecycle, MigrationCategoryRegistry.RetryIssuedStatuses)
{
    static readonly TimeSpan RecentErrorsWindow = TimeSpan.FromHours(1);

    public override string CategoryId => MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages;

    /// <summary>
    /// Streams the retry-issued and recent messages through <see cref="FailedMessageStatusReader.ReadStatuses(bool, string, int, DateTime?, CancellationToken)" />, then the claim holders,
    /// whose cursors start with <see cref="FailedMessageStream.ClaimHolderCursorPrefix" />. A resume from such a cursor skips the first part.
    /// </summary>
    public override async IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = new FailedMessageStream(Lifecycle);
        var throughIndex = await CanQueryIndex(cancellationToken);
        var recentErrorsFrom = await LatestUnresolvedLastModified(throughIndex, cancellationToken) - RecentErrorsWindow;
        var inClaimHolders = resumeAfter?.StartsWith(FailedMessageStream.ClaimHolderCursorPrefix, StringComparison.Ordinal) == true;

        if (!inClaimHolders)
        {
            await foreach (var batch in ReadStatuses(throughIndex, resumeAfter, batchSize, recentErrorsFrom, cancellationToken))
            {
                yield return await stream.ResolveRetryClaims(batch, cancellationToken);
            }
        }

        var claimHoldersAfter = inClaimHolders ? resumeAfter![FailedMessageStream.ClaimHolderCursorPrefix.Length..] : null;

        await foreach (var batch in stream.ClaimHolders(CategoryId, claimHoldersAfter, batchSize, recentErrorsFrom, cancellationToken))
        {
            yield return batch;
        }
    }
}
