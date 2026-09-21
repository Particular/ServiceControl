namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Client.Exceptions;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;
    using Raven.Client.ServerWide.Operations.Configuration;
    using ServiceControl.Persistence.RavenDB.DataMigration;
    using ServiceControl.RavenDB;

    class DatabaseSetup(RavenPersisterSettings settings, IDocumentStore documentStore)
    {
        public async Task Execute(CancellationToken cancellationToken = default)
        {
            await CreateDatabase(settings.DatabaseName, cancellationToken);
            await CreateDatabase(settings.ThroughputDatabaseName, cancellationToken);

            await UpdateDatabaseSettings(settings.DatabaseName, cancellationToken);
            await UpdateDatabaseSettings(settings.ThroughputDatabaseName, cancellationToken);

            await IndexCreation.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, documentStore, null, null, cancellationToken);

            await StartupChecks.WarnIfIndexesUseCorax(documentStore, settings.DatabaseName, cancellationToken);
            await StartupChecks.WarnIfIndexesUseCorax(documentStore, settings.ThroughputDatabaseName, cancellationToken);

            await LicenseStatusCheck.WaitForLicenseOrThrow(documentStore, cancellationToken);
            await ConfigureExpiration(settings, cancellationToken);
            await StampDataVersion(settings.DatabaseName, cancellationToken);
            await StampDataVersion(settings.ThroughputDatabaseName, cancellationToken);
        }

        // Records which ServiceControl build last wrote this database, so a migration can tell whether it reads the
        // documents the same way. Only ever raised: an older build running again must not hide what a newer one wrote.
        async Task StampDataVersion(string databaseName, CancellationToken cancellationToken)
        {
            using var session = documentStore.OpenAsyncSession(databaseName);
            var stamp = await session.LoadAsync<RavenDataVersion>(RavenDataVersion.DocumentId, cancellationToken);

            // A version that will not parse is replaced rather than kept, because a migration refuses to start on one it cannot compare.
            if (stamp is not null && !string.IsNullOrWhiteSpace(stamp.Version)
                && Version.TryParse(stamp.Version.Split('-')[0], out var stamped)
                && Version.TryParse(RavenDataVersion.Current.Split('-')[0], out var thisBuild)
                && stamped >= thisBuild)
            {
                return;
            }

            if (stamp is null)
            {
                await session.StoreAsync(new RavenDataVersion { Version = RavenDataVersion.Current, StampedAt = DateTime.UtcNow }, RavenDataVersion.DocumentId, cancellationToken);
            }
            else
            {
                stamp.Version = RavenDataVersion.Current;
                stamp.StampedAt = DateTime.UtcNow;
            }

            await session.SaveChangesAsync(cancellationToken);
        }

        async Task CreateDatabase(string databaseName, CancellationToken cancellationToken)
        {
            var dbRecord = await documentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), cancellationToken);

            if (dbRecord is null)
            {
                try
                {
                    var databaseRecord = new DatabaseRecord(databaseName);

                    // New databases use Lucene: smaller indexes, lower memory usage and faster for our index definitions.
                    // Existing databases keep the engine they were created with, see UpdateDatabaseSettings.
                    databaseRecord.Settings.Add("Indexing.Auto.SearchEngineType", "Lucene");
                    databaseRecord.Settings.Add("Indexing.Static.SearchEngineType", "Lucene");

                    await documentStore.Maintenance.Server.SendAsync(new CreateDatabaseOperation(databaseRecord), cancellationToken);
                }
                catch (ConcurrencyException)
                {
                    // The database was already created before calling CreateDatabaseOperation
                }
            }
        }

        async Task UpdateDatabaseSettings(string databaseName, CancellationToken cancellationToken)
        {
            var dbRecord = await documentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), cancellationToken);

            if (dbRecord is null)
            {
                throw new InvalidOperationException($"Database '{databaseName}' does not exist.");
            }

            // Existing databases keep their configured search engine. Changing it would trigger a full rebuild of all
            // indexes, which can take a long time and a lot of resources on large databases. Databases created before the
            // search engine was pinned explicitly get Corax, which was the default at the time.
            var updated = false;

            updated |= dbRecord.Settings.TryAdd("Indexing.Auto.SearchEngineType", "Corax");
            updated |= dbRecord.Settings.TryAdd("Indexing.Static.SearchEngineType", "Corax");

            if (updated)
            {
                await documentStore.Maintenance.ForDatabase(databaseName).SendAsync(new PutDatabaseSettingsOperation(databaseName, dbRecord.Settings), cancellationToken);
                await documentStore.Maintenance.Server.SendAsync(new ToggleDatabasesStateOperation(databaseName, true), cancellationToken);
                await documentStore.Maintenance.Server.SendAsync(new ToggleDatabasesStateOperation(databaseName, false), cancellationToken);
            }
        }

        async Task ConfigureExpiration(RavenPersisterSettings settings, CancellationToken cancellationToken)
        {
            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = settings.ExpirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken);
        }
    }
}