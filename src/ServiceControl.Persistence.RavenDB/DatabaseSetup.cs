namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Client.Exceptions;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.RavenDB;

    class DatabaseSetup(RavenPersisterSettings settings, IDocumentStore documentStore)
    {
        public async Task Execute(CancellationToken cancellationToken = default)
        {
            await CreateDatabase(settings.DatabaseName, cancellationToken);
            await CreateDatabase(settings.ThroughputDatabaseName, cancellationToken);

            await IndexDeployment.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, documentStore, cancellationToken);

            await StartupChecks.WarnIfIndexesUseCorax(documentStore, settings.DatabaseName, cancellationToken);
            await StartupChecks.WarnIfIndexesUseCorax(documentStore, settings.ThroughputDatabaseName, cancellationToken);

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

                    // New databases use Lucene: smaller indexes, lower memory usage and faster for our index definitions.
                    // The settings of an existing database are left to the operator. An index keeps the engine it was
                    // built with until it is rebuilt, so the database default does not change existing indexes.
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