namespace ServiceControl.Audit.Persistence.RavenDB
{
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
    using ServiceControl.Audit.Persistence.RavenDB.Indexes;
    using ServiceControl.SagaAudit;

    class DatabaseSetup
    {
        public DatabaseSetup(DatabaseConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public async Task Execute(IDocumentStore documentStore, CancellationToken cancellationToken)
        {
            try
            {
                await documentStore.Maintenance.ForDatabase(configuration.Name).SendAsync(new GetStatisticsOperation(), cancellationToken);
            }
            catch (DatabaseDoesNotExistException)
            {
                try
                {
                    await documentStore.Maintenance.Server
                        .SendAsync(new CreateDatabaseOperation(new DatabaseRecord(configuration.Name)), cancellationToken);
                }
                catch (ConcurrencyException)
                {
                    // The database was already created before calling CreateDatabaseOperation
                }
            }

            var indexList = new List<AbstractIndexCreationTask> {
                new FailedAuditImportIndex(),
                new SagaDetailsIndex()
            };

            // We need to replace the SagaDetailsIndex with a version that has a Take operation.
            // Doing this side-by-side results in the index never swapping if there is constant ingestion.
            // We thus check if the index includes the Take operation or not. If it does not include the Take
            // operation, then we delete the old index so that the new index is created without trying
            // to do a side-by-side upgrade.
            // This needs to stay in place until the next major version as the user could upgrade from an
            // older version which might still have the incorrect index.
            var sagaDetailsIndexOperation = new GetIndexOperation("SagaDetailsIndex");
            var sagaDetailsIndexDefinition = await documentStore.Maintenance.SendAsync(sagaDetailsIndexOperation, cancellationToken);
            if (sagaDetailsIndexDefinition != null && !sagaDetailsIndexDefinition.Reduce.Contains("Take(50000)"))
            {
                await documentStore.Maintenance.SendAsync(new DeleteIndexOperation("SagaDetailsIndex"), cancellationToken);
            }

            if (configuration.EnableFullTextSearch)
            {
                indexList.Add(new MessagesViewIndexWithFullTextSearch());
                await documentStore.Maintenance.SendAsync(new DeleteIndexOperation("MessagesViewIndex"), cancellationToken);
            }
            else
            {
                indexList.Add(new MessagesViewIndex());
                await documentStore.Maintenance
                    .SendAsync(new DeleteIndexOperation("MessagesViewIndexWithFullTextSearch"), cancellationToken);
            }

            await IndexCreation.CreateIndexesAsync(indexList, documentStore, null, null, cancellationToken);

            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = configuration.ExpirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken);
        }

        readonly DatabaseConfiguration configuration;
    }
}
