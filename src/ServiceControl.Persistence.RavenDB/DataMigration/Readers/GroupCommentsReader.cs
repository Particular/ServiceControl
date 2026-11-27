#nullable enable

namespace ServiceControl.Persistence.RavenDB.DataMigration.Readers;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB.Recoverability;

/// <summary>
/// Reads the comments operators wrote on failure groups. Each row carries the bare group id, because SQL keys a comment on the group id while RavenDB stores it under its own document id.
/// </summary>
sealed class GroupCommentsReader(RavenReadOnlySourceLifecycle lifecycle) : MigrationCategoryReader<GroupComment>(lifecycle)
{
    public override string CategoryId => MigrationCategoryIds.GroupComments;

    public override IAsyncEnumerable<MigrationBatch> Read(string? resumeAfter, int batchSize, CancellationToken cancellationToken = default) =>
        Documents(GroupsDataStore.CommentIdPrefix, resumeAfter, batchSize,
            static result => new MigrationRow(result.Id, new GroupComment { Id = result.Id[GroupsDataStore.CommentIdPrefix.Length..], Comment = result.Document.Comment }, ReadOnlyDictionary<string, object?>.Empty),
            cancellationToken);
}
