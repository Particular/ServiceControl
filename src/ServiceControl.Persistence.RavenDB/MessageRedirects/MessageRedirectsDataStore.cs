namespace ServiceControl.Persistence.RavenDB.MessageRedirects
{
    using System.Threading.Tasks;
    using Raven.Client.Documents;
    using ServiceControl.Persistence.MessageRedirects;

    class MessageRedirectsDataStore : IMessageRedirectsDataStore
    {
        readonly IDocumentStore store;

        public MessageRedirectsDataStore(IDocumentStore store)
        {
            this.store = store;
        }

        public async Task<MessageRedirectsCollection> GetOrCreate()
        {
            using (var session = store.OpenAsyncSession())
            {
                var redirects = await session.LoadAsync<MessageRedirectsCollection>(DefaultId);

                if (redirects != null)
                {
                    redirects.ETag = session.Advanced.GetChangeVectorFor(redirects);
                    redirects.LastModified = session.Advanced.GetLastModifiedFor(redirects).Value;

                    return redirects;
                }

                return new MessageRedirectsCollection();
            }
        }

        public async Task Save(MessageRedirectsCollection redirects)
        {
            using (var session = store.OpenAsyncSession())
            {
                await session.StoreAsync(redirects, redirects.ETag, DefaultId);
                await session.SaveChangesAsync();
            }
        }

        const string DefaultId = "messageredirects";
    }
}
