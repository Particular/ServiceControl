namespace ServiceControl.Persistence.RavenDb5
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Client.Exceptions;
    using Raven.Client.Exceptions.Database;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Recoverability;

    class DatabaseSetup
    {
        public DatabaseSetup(RavenDBPersisterSettings settings)
        {
            this.settings = settings;
        }

        public async Task Execute(IDocumentStore documentStore, CancellationToken cancellationToken)
        {
            try
            {
                await documentStore.Maintenance.ForDatabase(settings.DatabaseName).SendAsync(new GetStatisticsOperation(), cancellationToken);
            }
            catch (DatabaseDoesNotExistException)
            {
                try
                {
                    await documentStore.Maintenance.Server
                        .SendAsync(new CreateDatabaseOperation(new DatabaseRecord(settings.DatabaseName)), cancellationToken);
                }
                catch (ConcurrencyException)
                {
                    // The database was already created before calling CreateDatabaseOperation
                }
            }

            var indexList = new List<AbstractIndexCreationTask> {
                new ArchivedGroupsViewIndex(),
                new CustomChecksIndex(),
                new FailedErrorImportIndex(),
                new FailedMessageFacetsIndex(),
                new FailedMessageRetries_ByBatch(),
                new FailedMessageViewIndex(),
                new FailureGroupsViewIndex(),
                new GroupCommentIndex(),
                new KnownEndpointIndex(),
                new MessagesViewIndex(),
                new QueueAddressIndex(),
                new RetryBatches_ByStatusAndSession(),
                new RetryBatches_ByStatus_ReduceInitialBatchSize()

            };

            //TODO: Handle full text search
            //if (settings.EnableFullTextSearch)
            //{
            //    indexList.Add(new MessagesViewIndexWithFullTextSearch());
            //    await documentStore.Maintenance.SendAsync(new DeleteIndexOperation("MessagesViewIndex"), cancellationToken);
            //}
            //else
            //{
            //    indexList.Add(new MessagesViewIndex());
            //    await documentStore.Maintenance
            //        .SendAsync(new DeleteIndexOperation("MessagesViewIndexWithFullTextSearch"), cancellationToken);
            //}

            await IndexCreation.CreateIndexesAsync(indexList, documentStore, null, null, cancellationToken);

            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = settings.ExpirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken);
        }

        readonly RavenDBPersisterSettings settings;
    }
}
