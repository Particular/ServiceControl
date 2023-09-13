namespace ServiceControl.Persistence.RavenDb.MessageRedirects
{
    using System.Threading.Tasks;
    using Raven.Client.Documents.Queries;
    using RavenDb5;
    using ServiceControl.Persistence.MessageRedirects;

    class MessageRedirectsDataStore : IMessageRedirectsDataStore
    {
        readonly DocumentStoreProvider storeProvider;

        public MessageRedirectsDataStore(DocumentStoreProvider storeProvider)
        {
            this.storeProvider = storeProvider;
        }

        public async Task<MessageRedirectsCollection> GetOrCreate()
        {
            using (var session = storeProvider.Store.OpenAsyncSession())
            {
                var redirects = await session.LoadAsync<MessageRedirectsCollection>(DefaultId);

                if (redirects != null)
                {
                    redirects.ETag = session.Advanced.GetChangeVectorFor(redirects);
                    redirects.LastModified = RavenQuery.LastModified(redirects);

                    return redirects;
                }

                return new MessageRedirectsCollection();
            }
        }

        public async Task Save(MessageRedirectsCollection redirects)
        {
            using (var session = storeProvider.Store.OpenAsyncSession())
            {
                await session.StoreAsync(redirects, redirects.ETag, DefaultId);
                await session.SaveChangesAsync();
            }
        }

        const string DefaultId = "messageredirects";
    }
}
