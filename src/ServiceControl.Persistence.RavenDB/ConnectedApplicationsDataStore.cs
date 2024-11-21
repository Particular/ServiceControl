namespace ServiceControl.Persistence.RavenDB
{
    using System.Threading;
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Session;
    using ServiceControl.Persistence.Infrastructure;

    class ConnectedApplicationsDataStore(IRavenSessionProvider sessionProvider) : IConnectedApplicationsDataStore
    {
        public static string MakeDocumentId(string name) => $"{ConnectedApplication.CollectionName}/{DeterministicGuid.MakeId(name)}";

        public async Task<ConnectedApplication[]> GetAllConnectedApplications()
        {
            using IAsyncDocumentSession session = await sessionProvider.OpenSession();
            return await session
                .Query<ConnectedApplication, ConnectedApplicationIndex>()
                .ToArrayAsync();
        }

        public async Task UpdateConnectedApplication(ConnectedApplication connectedApplication, CancellationToken cancellationToken)
        {
            string docId = MakeDocumentId(connectedApplication.Name);

            using IAsyncDocumentSession session = await sessionProvider.OpenSession(cancellationToken: cancellationToken);

            await session.StoreAsync(connectedApplication, docId, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }
}