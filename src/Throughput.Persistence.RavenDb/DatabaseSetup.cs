﻿namespace Throughput.Persistence.RavenDb;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Operations.Expiration;
using Raven.Client.Documents.Operations.Indexes;
using Raven.Client.Exceptions;
using Raven.Client.Exceptions.Database;
using Raven.Client.ServerWide;
using Raven.Client.ServerWide.Operations;
//using ServiceControl.Audit.Persistence.RavenDB.Indexes;
//using ServiceControl.SagaAudit;

class DatabaseSetup(DatabaseConfiguration configuration)
{
    public async Task Execute(IDocumentStore documentStore, CancellationToken cancellationToken)
    {
        try
        {
            await documentStore.Maintenance
                .ForDatabase(configuration.Name)
                .SendAsync(new GetStatisticsOperation(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DatabaseDoesNotExistException)
        {
            try
            {
                await documentStore.Maintenance.Server
                    .SendAsync(new CreateDatabaseOperation(new DatabaseRecord(configuration.Name)), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (ConcurrencyException)
            {
                // The database was already created before calling CreateDatabaseOperation
            }
        }

        var indexList = new List<AbstractIndexCreationTask>
        {
            //new FailedAuditImportIndex(),
            //new SagaDetailsIndex()
        };

        await DeleteLegacySagaDetailsIndex(documentStore, cancellationToken).ConfigureAwait(false);

        if (configuration.EnableFullTextSearch)
        {
            //indexList.Add(new MessagesViewIndexWithFullTextSearch());
            await documentStore.Maintenance.SendAsync(new DeleteIndexOperation("MessagesViewIndex"), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            //indexList.Add(new MessagesViewIndex());
            await documentStore.Maintenance
                .SendAsync(new DeleteIndexOperation("MessagesViewIndexWithFullTextSearch"), cancellationToken).ConfigureAwait(false);
        }

        await IndexCreation.CreateIndexesAsync(indexList, documentStore, null, null, cancellationToken).ConfigureAwait(false);

        var expirationConfig = new ExpirationConfiguration
        {
            Disabled = false,
            DeleteFrequencyInSec = configuration.ExpirationProcessTimerInSeconds
        };

        await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken).ConfigureAwait(false);
    }

    public static async Task DeleteLegacySagaDetailsIndex(IDocumentStore documentStore, CancellationToken cancellationToken)
    {
        // If the SagaDetailsIndex exists but does not have a .Take(50000), then we remove the current SagaDetailsIndex and
        // create a new one. If we do not remove the current one, then RavenDB will attempt to do a side-by-side migration.
        // Doing a side-by-side migration results in the index never swapping if there is constant ingestion as RavenDB will wait.
        // for the index to not be stale before swapping to the new index. Constant ingestion means the index will never be not-stale.
        // This needs to stay in place until the next major version as the user could upgrade from an older version of the current
        // Major (v5.x.x) which might still have the incorrect index.
        var sagaDetailsIndexOperation = new GetIndexOperation("SagaDetailsIndex");
        var sagaDetailsIndexDefinition = await documentStore.Maintenance.SendAsync(sagaDetailsIndexOperation, cancellationToken).ConfigureAwait(false);
        if (sagaDetailsIndexDefinition != null && !sagaDetailsIndexDefinition.Reduce.Contains("Take(50000)"))
        {
            await documentStore.Maintenance.SendAsync(new DeleteIndexOperation("SagaDetailsIndex"), cancellationToken).ConfigureAwait(false);
        }
    }
}
