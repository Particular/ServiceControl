namespace ServiceControl.Persistence.EFCore.DataMigration.Writers;

using DbContexts;
using Entities;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using ServiceControl.ExternalIntegrations;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Writes the waiting integration events under new keys from the database, so a retry's reset empties the table before they are read again.
/// A request with no context, a type name longer than its column or a character the database refuses is skipped as RequiredValueMissing, ValueTooLong or MalformedValue.
/// The rest of the batch still copies.
/// </summary>
sealed class PendingIntegrationEventsWriter(IMigrationSqlDialect migrationDialect) : MigrationCategoryWriter<ExternalIntegrationDispatchRequest>, IStoreKeyedCategoryWriter
{
    public override string CategoryId => MigrationCategoryIds.PendingIntegrationEvents;

    public override int BatchSize(ServiceControlDbContext dbContext) => StoreKeyedInsert.BatchSize;

    public override Task<long> Count(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.ExternalIntegrationDispatchRequests.LongCountAsync(cancellationToken);

    // The whole table, because the target is refused unless it held no ServiceControl data before the first copy.
    public Task<int> DeleteCopiedRows(ServiceControlDbContext dbContext, CancellationToken cancellationToken = default) =>
        dbContext.ExternalIntegrationDispatchRequests.ExecuteDeleteAsync(cancellationToken);

    protected override Task<PreparedBatch> PrepareDocuments(ServiceControlDbContext dbContext, IReadOnlyList<(MigrationRow Row, ExternalIntegrationDispatchRequest Document)> documents, CancellationToken cancellationToken = default)
    {
        var textColumns = StoreKeyedInsert.TextColumns<ExternalIntegrationDispatchRequestEntity>(dbContext);
        List<ExternalIntegrationDispatchRequest> requests = [];
        List<(string SourceId, MigrationSkipReason Reason, string Detail)> skips = [];

        foreach (var (row, request) in documents)
        {
            if (request.DispatchContext is null)
            {
                skips.Add((row.SourceId, MigrationSkipReason.RequiredValueMissing, "ExternalIntegrationDispatchRequests.DispatchContextTypeName and DispatchContextJson are NOT NULL and the document has no context to serialize"));
            }
            else if (StoreKeyedInsert.UnstorableValueSkip(textColumns, migrationDialect, row, PendingIntegrationEventRowMapper.Map(request)) is { } unstorable)
            {
                skips.Add(unstorable);
            }
            else
            {
                requests.Add(request);
            }
        }

        return Task.FromResult(new PreparedBatch(
            (context, token) => StoreKeyedInsert.AddAll(context, requests, PendingIntegrationEventRowMapper.Map, token),
            requests.Count,
            skips,
            Merges: [])
        {
            RenumberedRows = requests.Count == 0 ? null : "pending integration events"
        });
    }
}
