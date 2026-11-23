namespace ServiceControl.Persistence.EFCore.DataMigration;

using Abstractions;
using Implementation;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using ServiceControl.Configuration;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// What a SQL Server or PostgreSQL database has to be before a copy writes to it, and the stamp saying a host
/// has opened on it since. The stamp is a row in the settings table, so it survives a restart and belongs to
/// the database rather than to the instance.
/// </summary>
public class EFCoreMigrationTargetReadiness(
    IServiceScopeFactory scopeFactory,
    IBodyStoragePersistence bodyStorage,
    TimeProvider timeProvider,
    EFPersisterSettings settings) : DataStoreBase(scopeFactory), IMigrationTargetReadiness
{
    static readonly SettingsRootNamespace SettingsRoot = new("ServiceControl");

    // Cheapest first: the schema costs one query, and the body store costs a round trip to a file share or a
    // cloud service.
    public IReadOnlyList<IMigrationStartupCheck> ContributedChecks() =>
    [
        new SchemaIsCurrentCheck(scopeFactory),
        new TargetHoldsNoServiceControlDataCheck(scopeFactory),
        new BodyStorageIsWritableCheck(bodyStorage)
    ];

    public IReadOnlyList<MigrationSettingValue> Configuration
    {
        get
        {
            var (storageType, locationLabel, location, locationKey) = settings.BodyStorage switch
            {
                FileSystemBodyStorageSettings fileSystem => (BodyStorageType.FileSystem, "path", fileSystem.StoragePath, EFPersistenceConfigurationBase.FileSystemStoragePathKey),
                AzureBlobBodyStorageSettings azureBlob => (BodyStorageType.AzureBlob, "container", azureBlob.ContainerName, EFPersistenceConfigurationBase.AzureContainerNameKey),
                S3BodyStorageSettings s3 => (BodyStorageType.S3, "bucket", s3.BucketName, EFPersistenceConfigurationBase.S3BucketNameKey),
                _ => throw new ArgumentOutOfRangeException(nameof(settings), settings.BodyStorage, "Unknown body storage type.")
            };

            return
            [
                new MigrationSettingValue("Connection string", MigrationSettingValue.MaskSecrets(settings.ConnectionString), Origin(EFPersistenceConfigurationBase.ConnectionStringKey)),
                new MigrationSettingValue("Schema", settings.Schema ?? "provider default", Origin(EFPersistenceConfigurationBase.SchemaKey)),
                new MigrationSettingValue("Body storage", storageType.ToString(), Origin(EFPersistenceConfigurationBase.BodyStorageTypeKey)),
                new MigrationSettingValue($"Body storage {locationLabel}", location, Origin(locationKey))
            ];
        }
    }

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

    static string Origin(string key) => MigrationSettingValue.OriginOf(SettingsRoot, key);
}
