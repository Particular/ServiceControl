namespace ServiceControl.Persistence.RavenDB
{
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

    class DatabaseSetup(RavenPersisterSettings settings)
    {
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

            await IndexCreation.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, documentStore, null, null, cancellationToken);

            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = settings.ExpirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken);
        }
    }
}
