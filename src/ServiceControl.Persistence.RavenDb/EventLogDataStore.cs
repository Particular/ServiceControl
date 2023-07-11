namespace ServiceControl.Persistence.RavenDb
{
    using System.Threading.Tasks;
    using Raven.Client;

    class EventLogDataStore : IEventLogDataStore
    {
        readonly IDocumentStore documentStore;

        public EventLogDataStore(IDocumentStore documentStore)
        {
            this.documentStore = documentStore;
        }

        public async Task Add(EventLog.EventLogItem logItem)
        {
            using (var session = documentStore.OpenAsyncSession())
            {
                await session.StoreAsync(logItem)
                    .ConfigureAwait(false);
                await session.SaveChangesAsync()
                    .ConfigureAwait(false);
            }
        }
    }
}
