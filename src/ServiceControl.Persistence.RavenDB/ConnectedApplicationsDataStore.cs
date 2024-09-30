namespace ServiceControl.Persistence.RavenDB
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    class ConnectedApplicationsDataStore(IRavenSessionProvider sessionProvider) : IConnectedApplicationsDataStore
    {
        public async Task Add(string connectedApplication)
        {
            using var session = await sessionProvider.OpenSession();
            var connectedApplications = await session.LoadAsync<ConnectedApplications>(StorageKey) ?? new ConnectedApplications();

            if (!connectedApplications.Applications.Any(application => string.Equals(application, connectedApplication, System.StringComparison.InvariantCultureIgnoreCase)))
            {
                connectedApplications.Applications.Add(connectedApplication);
            }

            await session.StoreAsync(connectedApplications, StorageKey);
            await session.SaveChangesAsync();
        }

        public async Task<IList<string>> GetConnectedApplications()
        {
            using var session = await sessionProvider.OpenSession();
            var applications = await session.LoadAsync<ConnectedApplications>(StorageKey);

            return applications.Applications;
        }

        class ConnectedApplications
        {
            public List<string> Applications { get; set; } = [];
        }

        const string StorageKey = "ConnectedApplications";
    }
}