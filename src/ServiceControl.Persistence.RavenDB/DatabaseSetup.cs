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

    class DatabaseSetup(RavenPersisterSettings settings, IDocumentStore documentStore)
    {
        public async Task Execute(CancellationToken cancellationToken)
        {
            await CreateDatabase(settings.DatabaseName, cancellationToken);
            await CreateDatabase(settings.ThroughputDatabaseName, cancellationToken);

            await UpdateDatabaseSettings(settings.DatabaseName, cancellationToken);
            await UpdateDatabaseSettings(settings.ThroughputDatabaseName, cancellationToken);

            await IndexCreation.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, documentStore, null, null, cancellationToken);

            await LicenseStatusCheck.WaitForLicenseOrThrow(documentStore, cancellationToken);
            await ConfigureExpiration(settings, cancellationToken);
        }

        async Task CreateDatabase(string databaseName, CancellationToken cancellationToken)
        {
            var dbRecord = await documentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), cancellationToken);

            if (dbRecord is null)
            {
                try
                {
                    var databaseRecord = new DatabaseRecord(databaseName);
                    databaseRecord.Settings.Add("Indexing.Auto.SearchEngineType", "Corax");
                    databaseRecord.Settings.Add("Indexing.Static.SearchEngineType", "Corax");

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