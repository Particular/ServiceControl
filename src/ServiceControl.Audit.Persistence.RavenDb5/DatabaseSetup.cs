namespace ServiceControl.Audit.Persistence.RavenDb
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
    using ServiceControl.Audit.Persistence.RavenDb.Indexes;
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
                await documentStore.Maintenance.ForDatabase(configuration.Name).SendAsync(new GetStatisticsOperation(), cancellationToken)
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

            var indexList = new List<AbstractIndexCreationTask> {
                new FailedAuditImportIndex(),
                new SagaDetailsIndex(),
                new AuditCountIndex()
            };

            if (configuration.EnableFullTextSearch)
            {
                indexList.Add(new MessagesViewIndexWithFullTextSearch());
                await documentStore.Maintenance.SendAsync(new DeleteIndexOperation("MessagesViewIndex"), cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                indexList.Add(new MessagesViewIndex());
                await documentStore.Maintenance
                    .SendAsync(new DeleteIndexOperation("MessagesViewIndexWithFullTextSearch"), cancellationToken)
                    .ConfigureAwait(false);
            }

            await IndexCreation.CreateIndexesAsync(indexList, documentStore, null, null, cancellationToken).ConfigureAwait(false);

            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = configuration.ExpirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken)
                .ConfigureAwait(false);
        }

        readonly DatabaseConfiguration configuration;
    }
}
