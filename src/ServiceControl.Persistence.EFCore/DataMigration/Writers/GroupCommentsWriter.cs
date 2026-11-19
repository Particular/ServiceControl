namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Copies the group comments, one row per comment keyed on its group id, and never replaces a comment the target already holds.
/// A null or blank comment is skipped as BlankGroupComment, because the product keeps no row for one.
/// A comment whose group id the key column treats as another's is reported as a merge.
/// A copied comment on a group the target holds no failed message in is reported as a change, because the retention sweep deletes it once ServiceControl opens.
/// </summary>
sealed class GroupCommentsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<GroupComment>
{
    public override string CategoryId => MigrationCategoryIds.GroupComments;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<GroupCommentEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.GroupComments.LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, GroupComment Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<GroupCommentEntity>.For(dbContext);
        var rows = new List<(string SourceId, GroupCommentEntity Row)>(documents.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        foreach (var (row, comment) in documents)
        {
            if (string.IsNullOrWhiteSpace(comment.Comment))
            {
                skips.Add((row.SourceId, MigrationSkipReason.BlankGroupComment, $"the comment on group '{comment.Id}' is blank, and the product keeps no row for a blank comment"));
                continue;
            }

            var entity = GroupCommentRowMapper.Map(comment);

            if (UnstorableValueSkip(insert, migrationDialect, row, entity) is { } unstorable)
            {
                skips.Add(unstorable);
                continue;
            }

            rows.Add((row.SourceId, entity));
        }

        var (merges, inserted) = await KeyMerges.Split(dbContext, migrationDialect, rows, cancellationToken: cancellationToken);

        return new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, [.. rows.Select(row => row.Row)], token)).Count,
            rows.Count,
            skips,
            merges)
        {
            Changes = await CommentsTheSweepWillDelete(dbContext, inserted, cancellationToken)
        };
    }

    // Group comments copy after the required failed messages. The unresolved messages without a retry claim arrive after ServiceControl opens, so a group that only they belong to is missing here.
    async Task<IReadOnlyList<(string SourceId, string Detail)>> CommentsTheSweepWillDelete(ServiceControlDbContext dbContext, IReadOnlyList<(string SourceId, GroupCommentEntity Row)> inserted, CancellationToken cancellationToken)
    {
        string[] groupIds = [.. inserted.Select(comment => comment.Row.GroupId).Distinct(StringComparer.Ordinal)];
        var held = (await dbContext.FailedMessageGroups
            .AsNoTracking()
            .Where(group => groupIds.Contains(group.GroupId))
            .Select(group => group.GroupId)
            .ToListAsync(cancellationToken))
            .ToHashSet(migrationDialect.KeyComparer(typeof(FailedMessageGroupEntity), nameof(FailedMessageGroupEntity.GroupId)));

        return [.. inserted
            .Where(comment => !held.Contains(comment.Row.GroupId))
            .Select(comment => (comment.SourceId, $"The group '{comment.Row.GroupId}' has no copied failed message, so the retention sweep removes this comment shortly after ServiceControl opens. " +
                "Its messages have not been copied yet, are archived or resolved, are gone from the source, or were skipped by the copy. " +
                $"The comment was: '{comment.Row.Comment}'"))];
    }
}
