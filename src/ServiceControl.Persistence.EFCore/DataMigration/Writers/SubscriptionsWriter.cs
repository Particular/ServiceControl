namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Copies the subscriptions. The key leaves out the message type's version, so two versions of one type for one
/// subscriber become one row, and the second is reported as a merge.
/// A subscription that cannot be stored is skipped with a reason, and the rest of the batch still copies.
/// </summary>
sealed class SubscriptionsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<SubscriptionRow>
{
    public override string CategoryId => MigrationCategoryIds.Subscriptions;

    public override int BatchSize(ServiceControlDbContext dbContext) => migrationDialect.RowsPerStatementFor<SubscriptionEntity>(dbContext);

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.Subscriptions.LongCountAsync(cancellationToken);

    protected override async Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, SubscriptionRow Document)> documents, CancellationToken cancellationToken = default)
    {
        var insert = MigrationInsert<SubscriptionEntity>.For(dbContext);
        var rows = new List<(string SourceId, SubscriptionEntity Row)>(documents.Count);
        var skips = new List<(string SourceId, MigrationSkipReason Reason, string Detail)>();

        foreach (var (row, subscription) in documents)
        {
            if (subscription.MessageType is null || subscription.TransportAddress is null)
            {
                var column = subscription.MessageType is null ? nameof(SubscriptionEntity.MessageType) : nameof(SubscriptionEntity.TransportAddress);
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, $"Subscriptions.{column} is NOT NULL and the document has no value for it"));
                continue;
            }

            var entity = SubscriptionRowMapper.Map(subscription);

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
