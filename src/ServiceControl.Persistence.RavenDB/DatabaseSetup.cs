namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Indexes;
    using Raven.Client.Documents.Operations.Expiration;
    using Raven.Client.Exceptions;
    using Raven.Client.ServerWide;
    using Raven.Client.ServerWide.Operations;

    class DatabaseSetup(RavenPersisterSettings settings, IDocumentStore documentStore)
    {
        public async Task Execute(CancellationToken cancellationToken)
        {
            await CreateDatabase(settings.DatabaseName, cancellationToken);

            await IndexCreation.CreateIndexesAsync(typeof(DatabaseSetup).Assembly, documentStore, null, null, cancellationToken);

            var expirationConfig = new ExpirationConfiguration
            {
                Disabled = false,
                DeleteFrequencyInSec = settings.ExpirationProcessTimerInSeconds
            };

            await documentStore.Maintenance.SendAsync(new ConfigureExpirationOperation(expirationConfig), cancellationToken);
        }

        async Task CreateDatabase(string databaseName, CancellationToken cancellationToken)
        {
            DatabaseRecordWithEtag dbRecord =
                await documentStore.Maintenance.Server.SendAsync(new GetDatabaseRecordOperation(databaseName),
                    cancellationToken);

            if (dbRecord == null)
            {
                try
                {
                    await documentStore.Maintenance.Server.SendAsync(
                        new CreateDatabaseOperation(new DatabaseRecord(databaseName)), cancellationToken);
                }
                catch (ConcurrencyException)
                {
                    // The database was already created before calling CreateDatabaseOperation
                }
            }
        }
    }
}