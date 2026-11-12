namespace ServiceControl.Persistence.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Client.Documents.Operations.Indexes;
    using Raven.Client.Exceptions;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.Infrastructure;
    using ServiceControl.RavenDB;
    using Throughput.Indexes;

    class DatabaseSetup(RavenPersisterSettings settings, IDocumentStore documentStore)
    {
        public async Task Execute(CancellationToken cancellationToken = default)
        {
            await CreateDatabase(settings.DatabaseName, cancellationToken);
            await CreateDatabase(settings.ThroughputDatabaseName, cancellationToken);

            // The throughput indexes belong to the throughput database, all other indexes to the error database.
            var indexes = IndexDeployment.FindIndexes(typeof(DatabaseSetup).Assembly).ToList();
            var throughputIndexes = indexes.Where(IsThroughputIndex).ToList();
            var errorIndexes = indexes.Except(throughputIndexes).ToList();

            await DeleteThroughputIndexesFromErrorDatabase(throughputIndexes, cancellationToken);

            await IndexDeployment.CreateIndexesAsync(errorIndexes, documentStore, cancellationToken: cancellationToken);
            await IndexDeployment.CreateIndexesAsync(throughputIndexes, documentStore, settings.ThroughputDatabaseName, cancellationToken);

            await StartupChecks.WarnIfIndexesUseCorax(documentStore, settings.DatabaseName, cancellationToken);
            await StartupChecks.WarnIfIndexesUseCorax(documentStore, settings.ThroughputDatabaseName, cancellationToken);

            await LicenseStatusCheck.WaitForLicenseOrThrow(documentStore, cancellationToken);
            await ConfigureExpiration(settings, cancellationToken);
        }

        static bool IsThroughputIndex(AbstractIndexCreationTask index) => index.GetType().Namespace == typeof(EndpointIndex).Namespace;

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

        // Earlier versions deployed the throughput indexes to the error database, where no query uses them.
        async Task DeleteThroughputIndexesFromErrorDatabase(IEnumerable<AbstractIndexCreationTask> throughputIndexes, CancellationToken cancellationToken)
        {
            foreach (var index in throughputIndexes)
            {
                var definition = await documentStore.Maintenance.SendAsync(new GetIndexOperation(index.IndexName), cancellationToken);

                if (definition != null)
                {
                    Logger.LogInformation("Deleting the throughput index {IndexName} from the {DatabaseName} database", index.IndexName, settings.DatabaseName);

                    // RavenDB ignores the deletion of a LockedIgnore index and rejects that of a LockedError index. An
                    // operator might have locked the index after migrating it to Lucene.
                    if (definition.LockMode is not (null or IndexLockMode.Unlock))
                    {
                        await documentStore.Maintenance.SendAsync(new SetIndexesLockOperation(index.IndexName, IndexLockMode.Unlock), cancellationToken);
                    }

                    await documentStore.Maintenance.SendAsync(new DeleteIndexOperation(index.IndexName), cancellationToken);
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

        static readonly ILogger Logger = LoggerUtil.CreateStaticLogger<DatabaseSetup>();
    }
}