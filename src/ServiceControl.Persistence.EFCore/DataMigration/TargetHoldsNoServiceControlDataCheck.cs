namespace ServiceControl.Persistence.EFCore.DataMigration;

using DbContexts;
using Implementation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses a copy into a database that already holds ServiceControl data. A copy into a database that ran on SQL
/// mixes the customer's rows with rows SQL already held, and a retry of a category could then delete rows the copy
/// never wrote. The check stands down once a row that is not known to be optional exists, because that means a copy
/// has started and what the tables hold is what it wrote.
/// </summary>
sealed class TargetHoldsNoServiceControlDataCheck(IServiceScopeFactory scopeFactory) : DataStoreBase(scopeFactory), IMigrationStartupCheck
{
    public string Name => "the migration target holds no ServiceControl data";

    // One entry per mapped entity but the checkpoint: its CLR type, and a query asking whether its table holds a row.
    internal static readonly IReadOnlyList<(Type Entity, Func<ServiceControlDbContext, CancellationToken, Task<bool>> HasRows)> Tables =
    [
        Entry(dbContext => dbContext.CustomChecks),
        Entry(dbContext => dbContext.EndpointSettings),
        Entry(dbContext => dbContext.KnownEndpoints),
        Entry(dbContext => dbContext.FailedMessages),
        Entry(dbContext => dbContext.FailedMessageGroups),
        Entry(dbContext => dbContext.GroupComments),
        Entry(dbContext => dbContext.MessageRedirects),
        Entry(dbContext => dbContext.RetryBatches),
        Entry(dbContext => dbContext.RetryBatchNowForwarding),
        Entry(dbContext => dbContext.FailedMessageRetries),
        Entry(dbContext => dbContext.FailedErrorImports),
        Entry(dbContext => dbContext.Settings),
        Entry(dbContext => dbContext.Subscriptions),
        Entry(dbContext => dbContext.EventLogItems),
        Entry(dbContext => dbContext.HistoricRetryOperations),
        Entry(dbContext => dbContext.UnacknowledgedRetryOperations),
        Entry(dbContext => dbContext.ArchiveOperations),
        Entry(dbContext => dbContext.FailedMessageEdits),
        Entry(dbContext => dbContext.LicensingEndpoints),
        Entry(dbContext => dbContext.LicensingEndpointThroughput),
        Entry(dbContext => dbContext.ExternalIntegrationDispatchRequests)
    ];

    // Both halves of an entry come from one DbSet, so the type a test compares and the table the query reads cannot differ.
    static (Type Entity, Func<ServiceControlDbContext, CancellationToken, Task<bool>> HasRows) Entry<T>(Func<ServiceControlDbContext, DbSet<T>> set) where T : class =>
        (typeof(T), (dbContext, cancellationToken) => set(dbContext).AnyAsync(cancellationToken));

    public Task Run(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            var checkpointed = await dbContext.MigrationCheckpoints.Select(checkpoint => checkpoint.CategoryId).ToListAsync(token);

            if (checkpointed.Any(id => MigrationCategoryRegistry.Find(id)?.Kind != MigrationCategoryKind.Optional))
            {
                return;
            }

            var holdingRows = new List<string>();

            foreach (var (entity, hasRows) in Tables)
            {
                if (await hasRows(dbContext, token))
                {
                    holdingRows.Add(dbContext.Model.FindEntityType(entity)!.GetTableName()!);
                }
            }

            if (holdingRows.Count > 0)
            {
                throw new Exception($"The database already holds ServiceControl data in {string.Join(", ", holdingRows)}, so ServiceControl has already run on it, and a migration copies only into an empty database. Create a new database, run ServiceControl with --setup against it, and do not start ServiceControl on it before migrating; then start again with migration on.");
            }
        }, cancellationToken);
}
