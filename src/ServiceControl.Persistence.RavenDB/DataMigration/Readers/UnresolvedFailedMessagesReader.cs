#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Threading;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Reads every unresolved failed message, whatever its age, for the copy that runs while ServiceControl is open. A message that
/// holds a retry claim was copied with the required category and is already present, which the copy counts as such.
/// </summary>
sealed class UnresolvedFailedMessagesReader(RavenReadOnlySourceLifecycle lifecycle) : FailedMessageStatusReader(lifecycle, MigrationCategoryRegistry.UnresolvedStatuses)
{
    public override string CategoryId => MigrationCategoryIds.UnresolvedFailedMessages;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        ReadStatuses(resumeAfter, batchSize, cancellationToken);
}
