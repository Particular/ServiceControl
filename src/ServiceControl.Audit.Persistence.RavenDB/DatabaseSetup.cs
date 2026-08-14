namespace ServiceControl.Audit.Persistence.RavenDB;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Expiration;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Exceptions;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
using Raven.Client.ServerWide.Operations.Configuration;
using Indexes;
using SagaAudit;

class DatabaseSetup(DatabaseConfiguration configuration)
{
    public async Task Execute(IDocumentStore documentStore, CancellationToken cancellationToken = default)
    {
        await CreateDatabase(documentStore, configuration.Name, cancellationToken);

        await UpdateDatabaseSettings(documentStore, configuration.Name, cancellationToken);

        if (configuration.EnableAuditRetentionBuckets)
        {
            // Fail fast before the bucket-mode query path can become available: bucket mode reads only
            // the dedicated per-bucket collections and indexes, so any ProcessedMessage or SagaSnapshot
            // left in the legacy collections would be silently invisible to every query. There is no
            // migration or legacy-read merge, so bucket mode is only safe on an empty/new database.
            await EnsureNoLegacyAuditData(documentStore, cancellationToken);

            // Bucket mode: only the unbucketed FailedAuditImport index is created here. The dedicated
            // per-bucket indexes are created by AuditRetentionBucketManager when buckets roll over, and
            // the current static index names are deliberately avoided so a side-by-side replacement can
            // never be attempted under constant ingestion. Raven's document expiry is not enabled because
            // bucket cleanup owns retention of the bucketed documents.
            await IndexCreation.CreateIndexesAsync([new FailedAuditImportIndex()], documentStore, null, null, cancellationToken);
            await LicenseStatusCheck.WaitForLicenseOrThrow(documentStore, cancellationToken);
            return;
        }

        await CreateIndexes(documentStore, configuration.EnableFullTextSearch, cancellationToken);

        await LicenseStatusCheck.WaitForLicenseOrThrow(documentStore, cancellationToken);
        await ConfigureExpiration(documentStore, cancellationToken);
    }

    async Task CreateDatabase(IDocumentStore documentStore, string databaseName, CancellationToken cancellationToken)
    {
        var dbRecord = await documentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), cancellationToken);

        if (dbRecord is null)
        {
            try
            {
                var databaseRecord = new DatabaseRecord(databaseName);

                SetSearchEngineType(databaseRecord, SearchEngineType.Corax);

                await documentStore.Maintenance.Server.SendAsync(new CreateDatabaseOperation(databaseRecord), cancellationToken);
            }
            catch (ConcurrencyException)
            {
                // The database was already created before calling CreateDatabaseOperation
            }
        }
    }

    async Task UpdateDatabaseSettings(IDocumentStore documentStore, string databaseName, CancellationToken cancellationToken)
    {
        var databaseRecord = await documentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName), cancellationToken) ?? throw new InvalidOperationException($"Database '{databaseName}' does not exist.");

        if (!SetSearchEngineType(databaseRecord, SearchEngineType.Corax))
        {
            return;
        }

        await documentStore.Maintenance.ForDatabase(databaseName).SendAsync(new PutDatabaseSettingsOperation(databaseName, databaseRecord.Settings), cancellationToken);
        await documentStore.Maintenance.Server.SendAsync(new ToggleDatabasesStateOperation(databaseName, true), cancellationToken);
        await documentStore.Maintenance.Server.SendAsync(new ToggleDatabasesStateOperation(databaseName, false), cancellationToken);
    }

    public static async Task DeleteLegacySagaDetailsIndex(IDocumentStore documentStore, CancellationToken cancellationToken = default)
    {
        // If the SagaDetailsIndex exists but does not have a .Take(50000), then we remove the current SagaDetailsIndex and
        // create a new one. If we do not remove the current one, then RavenDB will attempt to do a side-by-side migration.
        // Doing a side-by-side migration results in the index never swapping if there is constant ingestion as RavenDB will wait.
        // for the index to not be stale before swapping to the new index. Constant ingestion means the index will never be not-stale.
        // This needs to stay in place until the next major version as the user could upgrade from an older version of the current
        // Major (v5.x.x) which might still have the incorrect index.
        var sagaDetailsIndexOperation = new GetIndexOperation(SagaDetailsIndexName);
        var sagaDetailsIndexDefinition = await documentStore.Maintenance.SendAsync(sagaDetailsIndexOperation, cancellationToken);
        if (sagaDetailsIndexDefinition != null && !sagaDetailsIndexDefinition.Reduce.Contains("Take(50000)"))
        {
            await documentStore.Maintenance.SendAsync(new DeleteIndexOperation(SagaDetailsIndexName), cancellationToken);
        }
    }

    internal static async Task CreateIndexes(IDocumentStore documentStore, bool enableFreeTextSearch, CancellationToken cancellationToken = default)
    {
        await DeleteLegacySagaDetailsIndex(documentStore, cancellationToken);

        List<AbstractIndexCreationTask> indexList = [new FailedAuditImportIndex(), new SagaDetailsIndex()];

        if (enableFreeTextSearch)
        {
            indexList.Add(new MessagesViewIndexWithFullTextSearch());
            await documentStore.Maintenance.SendAsync(new DeleteIndexOperation(MessagesViewIndexName), cancellationToken);
        }
        else
        {
            indexList.Add(new MessagesViewIndex());
            await documentStore.Maintenance.SendAsync(new DeleteIndexOperation(MessagesViewIndexWithFulltextSearchName), cancellationToken);
        }

        await IndexCreation.CreateIndexesAsync(indexList, documentStore, null, null, cancellationToken);
    }

    async Task ConfigureExpiration(IDocumentStore documentStore, CancellationToken cancellationToken)
    {
        var expirationConfig = new ExpirationConfiguration
        {
            Disabled = false,
            DeleteFrequencyInSec = configuration.ExpirationProcessTimerInSeconds
        };

        await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken);
    }

    async Task EnsureNoLegacyAuditData(IDocumentStore documentStore, CancellationToken cancellationToken)
    {
        var statistics = await documentStore.Maintenance.SendAsync(new GetCollectionStatisticsOperation(), cancellationToken);

        statistics.Collections.TryGetValue("ProcessedMessages", out var processedMessageCount);
        statistics.Collections.TryGetValue("SagaSnapshots", out var sagaSnapshotCount);

        if (processedMessageCount > 0 || sagaSnapshotCount > 0)
        {
            throw new InvalidOperationException(
                $"Audit retention bucket mode (RavenDB/EnableAuditRetentionBuckets) requires an empty or new database. " +
                $"The database '{configuration.Name}' contains {processedMessageCount} legacy ProcessedMessage document(s) " +
                $"and {sagaSnapshotCount} legacy SagaSnapshot document(s). Bucket mode reads only the dedicated per-bucket " +
                $"collections, so enabling it on a populated legacy Audit database would silently hide all existing audit data. " +
                $"Start bucket mode on an empty/new database; a migration of existing audit data is not supported.");
        }
    }

    bool SetSearchEngineType(DatabaseRecord database, SearchEngineType searchEngineType)
    {
        var updated = false;

        updated |= database.Settings.TryAdd("Indexing.Auto.SearchEngineType", searchEngineType.ToString());
        updated |= database.Settings.TryAdd("Indexing.Static.SearchEngineType", searchEngineType.ToString());

        return updated;
    }

    internal const string MessagesViewIndexWithFulltextSearchName = "MessagesViewIndexWithFullTextSearch";
    internal const string SagaDetailsIndexName = "SagaDetailsIndex";
    internal const string MessagesViewIndexName = "MessagesViewIndex";
}