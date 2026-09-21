namespace ServiceControl.Persistence.EFCore.DataMigration;

using Abstractions;
using Implementation;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// What a SQL Server or PostgreSQL database has to be before a copy writes to it, and the stamp saying a host
/// has opened on it since. The stamp is a row in the settings table, so it survives a restart and belongs to
/// the database rather than to the instance.
/// </summary>
public class EFCoreMigrationTargetReadiness(
    IServiceScopeFactory scopeFactory,
    IBodyStoragePersistence bodyStorage,
    EFPersisterSettings settings,
    TimeProvider timeProvider) : DataStoreBase(scopeFactory), IMigrationTargetReadiness
{
    // Cheapest first: the setting is already in memory, the schema costs one query, and the body store costs a
    // round trip to a file share or a cloud service.
    public IReadOnlyList<IMigrationStartupCheck> ContributedChecks() =>
    [
        new RetryHistoryDepthIsSafeCheck(settings.RetryHistoryDepth),
        new SchemaIsCurrentCheck(scopeFactory),
        new BodyStorageIsWritableCheck(bodyStorage)
    ];

    public Task RecordHostOpened(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
        {
            if (await dbContext.GetSetting<DateTime?>(SettingKeys.MigrationHostOpenedOnTarget, token) is null)
            {
                await dbContext.StoreSetting(SettingKeys.MigrationHostOpenedOnTarget, timeProvider.GetUtcNow().UtcDateTime, token);
            }
        }, cancellationToken);

    public Task<bool> HasHostOpened(CancellationToken cancellationToken = default) =>
        ExecuteWithDbContext(async (dbContext, token) =>
            await dbContext.GetSetting<DateTime?>(SettingKeys.MigrationHostOpenedOnTarget, token) is not null, cancellationToken);
}
