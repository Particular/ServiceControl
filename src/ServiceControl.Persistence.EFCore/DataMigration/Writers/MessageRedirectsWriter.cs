namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.MessageRedirects;

/// <summary>
/// Copies the message redirects, one row per redirect. A redirect whose source address is already taken is reported as a merge.
/// A redirect that cannot be stored is skipped with a reason, and the rest of the batch still copies.
/// </summary>
sealed class MessageRedirectsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<MessageRedirect>
{
    public override string CategoryId => MigrationCategoryIds.MessageRedirects;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<MessageRedirectEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.MessageRedirects.LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, MessageRedirect Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<MessageRedirectEntity>.For(dbContext);
        var rows = new List<(string SourceId, MessageRedirectEntity Row)>(documents.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        foreach (var (row, redirect) in documents)
        {
            if (redirect.FromPhysicalAddress is null || redirect.ToPhysicalAddress is null)
            {
                var column = redirect.FromPhysicalAddress is null ? nameof(MessageRedirectEntity.FromPhysicalAddress) : nameof(MessageRedirectEntity.ToPhysicalAddress);
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, $"MessageRedirects.{column} is NOT NULL and the document has no value for it"));
                continue;
            }

            var entity = MessageRedirectRowMapper.Map(redirect);

            if (UnstorableValueSkip(insert, migrationDialect, row, entity) is { } unstorable)
            {
                skips.Add(unstorable);
                continue;
            }

            rows.Add((row.SourceId, entity));
        }

        return new PreparedBatch(
            async (context, token) => (await migrationDialect.InsertMissing(context, [.. rows.Select(row => row.Row)], token)).Count,
            rows.Count,
            skips,
            await KeyMerges.In(dbContext, migrationDialect, rows, cancellationToken));
    }
}
