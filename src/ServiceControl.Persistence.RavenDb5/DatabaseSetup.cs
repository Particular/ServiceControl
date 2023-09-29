namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using System.Linq;
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

            var indexTypes = typeof(DatabaseSetup).Assembly.GetTypes()
                .Where(t => typeof(IAbstractIndexCreationTask).IsAssignableFrom(t))
                .ToList();

            //TODO: Handle full text search - if necessary add Where clause to query above to remove the two variants
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

            var indexList = indexTypes
                .Select(t => Activator.CreateInstance(t))
                .OfType<IAbstractIndexCreationTask>();

            // If no full-text vs not full-text index is required, this can all be simplified using the assembly-based override
            // await IndexCreation.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, documentStore, null, null, cancellationToken);
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
