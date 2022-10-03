namespace ServiceControl.Audit.Persistence.RavenDb5
{
    using System.Collections.Generic;
    using System.Linq;
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
    using Raven.Client.ServerWide.Operations.DocumentsCompression;
    using ServiceControl.Audit.Persistence.RavenDb.Indexes;
    using ServiceControl.SagaAudit;

    class DatabaseSetup
    {
        public DatabaseSetup(int expirationProcessTimerInSeconds, bool enableFullTextSearch, AuditDatabaseConfiguration configuration)
        {
            this.expirationProcessTimerInSeconds = expirationProcessTimerInSeconds;
            this.enableFullTextSearch = enableFullTextSearch;
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

            if (configuration.EnableDocumentCompression)
            {
                await documentStore.Maintenance.ForDatabase(configuration.Name).SendAsync(
                    new UpdateDocumentsCompressionConfigurationOperation(new DocumentsCompressionConfiguration(
                        false,
                        configuration.CollectionsToCompress.ToArray()
                    )), cancellationToken).ConfigureAwait(false);
            }

            var indexList =
                  new List<AbstractIndexCreationTask> { new FailedAuditImportIndex(), new SagaDetailsIndex() };

            if (enableFullTextSearch)
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

            // TODO: Check to see if the configuration has changed.
            // If it has, then send an update to the server to change the expires metadata on all documents
            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = expirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken)
                .ConfigureAwait(false);
        }

        readonly int expirationProcessTimerInSeconds;
        readonly bool enableFullTextSearch;
        readonly AuditDatabaseConfiguration configuration;
    }
}
